use sqlx::SqlitePool;
use tracing::{debug, info, warn};

use crate::error::AppError;
use crate::models::app::{AppJoinCols, AppModel, APP_JOIN_COLS_SQL};
use crate::models::request::{CreateAppRequest, UpdateAppRequest};
use crate::services::category::CategoryService;

pub struct AppDataService;

impl AppDataService {
    pub async fn get_all_apps(pool: &SqlitePool) -> Result<Vec<AppModel>, AppError> {
        debug!("get_all_apps");
        let sql = format!(
            "SELECT {cols} FROM AppModels a \
             LEFT JOIN CategoryModels c ON a.CategoryID = c.ID \
             ORDER BY a.ID",
            cols = APP_JOIN_COLS_SQL
        );
        let rows: Vec<AppJoinCols> = sqlx::query_as(&sql).fetch_all(pool).await?;

        Ok(rows
            .iter()
            .filter_map(AppJoinCols::to_app_model)
            .collect())
    }

    pub async fn get_app(pool: &SqlitePool, id: i64) -> Result<Option<AppModel>, AppError> {
        debug!("get_app: id={}", id);
        let sql = format!(
            "SELECT {cols} FROM AppModels a \
             LEFT JOIN CategoryModels c ON a.CategoryID = c.ID \
             WHERE a.ID = ?",
            cols = APP_JOIN_COLS_SQL
        );
        let row: Option<AppJoinCols> = sqlx::query_as(&sql)
            .bind(id)
            .fetch_optional(pool)
            .await?;

        Ok(row.and_then(|r| r.to_app_model()))
    }

    pub async fn get_app_by_name(
        pool: &SqlitePool,
        name: &str,
    ) -> Result<Option<AppModel>, AppError> {
        debug!("get_app_by_name: name={}", name);
        let sql = format!(
            "SELECT {cols} FROM AppModels a \
             LEFT JOIN CategoryModels c ON a.CategoryID = c.ID \
             WHERE a.Name = ?",
            cols = APP_JOIN_COLS_SQL
        );
        let row: Option<AppJoinCols> = sqlx::query_as(&sql)
            .bind(name)
            .fetch_optional(pool)
            .await?;

        Ok(row.and_then(|r| r.to_app_model()))
    }

    pub async fn create_app(
        pool: &SqlitePool,
        req: CreateAppRequest,
    ) -> Result<AppModel, AppError> {
        info!("create_app: name={}", req.name);
        let mut category_id = req.category_id;
        if category_id > 0 {
            let cat_exists: Option<(i64,)> = sqlx::query_as("SELECT ID FROM CategoryModels WHERE ID = ?")
                .bind(category_id)
                .fetch_optional(pool)
                .await?;
            if cat_exists.is_none() {
                warn!("create_app: category_id={} not found, fallback to system category", category_id);
                category_id = 0;
            }
        }
        let category_id = if category_id > 0 {
            category_id
        } else {
            CategoryService::get_system_category_id(pool).await?
        };
        let id = sqlx::query(
            r#"
            INSERT INTO AppModels (Name, Description, File, IconFile, CategoryID, TotalTime)
            VALUES (?, ?, ?, ?, ?, 0)
            "#,
        )
        .bind(&req.name)
        .bind(&req.description)
        .bind(&req.file)
        .bind(&req.icon_file)
        .bind(category_id)
        .execute(pool)
        .await?
        .last_insert_rowid();

        Self::get_app(pool, id).await?.ok_or_else(|| {
            AppError::Internal("Failed to create app".to_string())
        })
    }

    pub async fn update_app(
        pool: &SqlitePool,
        id: i64,
        req: UpdateAppRequest,
    ) -> Result<(), AppError> {
        info!("update_app: id={}", id);
        let mut category_id = req.category_id;
        if category_id > 0 {
            let cat_exists: Option<(i64,)> = sqlx::query_as("SELECT ID FROM CategoryModels WHERE ID = ?")
                .bind(category_id)
                .fetch_optional(pool)
                .await?;
            if cat_exists.is_none() {
                warn!("update_app: category_id={} not found, fallback to system category", category_id);
                category_id = 0;
            }
        }
        let category_id = if category_id > 0 {
            category_id
        } else {
            CategoryService::get_system_category_id(pool).await?
        };
        let result = sqlx::query(
            r#"
            UPDATE AppModels
            SET Name = ?, Alias = ?, Description = ?, File = ?, IconFile = ?, CategoryID = ?
            WHERE ID = ?
            "#,
        )
        .bind(&req.name)
        .bind(&req.alias)
        .bind(&req.description)
        .bind(&req.file)
        .bind(&req.icon_file)
        .bind(category_id)
        .bind(id)
        .execute(pool)
        .await?;

        if result.rows_affected() == 0 {
            return Err(AppError::Business("应用不存在".to_string()));
        }

        Ok(())
    }

    pub async fn get_apps_by_category(
        pool: &SqlitePool,
        category_id: i64,
    ) -> Result<Vec<AppModel>, AppError> {
        debug!("get_apps_by_category: category_id={}", category_id);
        let sql = format!(
            "SELECT {cols} FROM AppModels a \
             LEFT JOIN CategoryModels c ON a.CategoryID = c.ID \
             WHERE a.CategoryID = ?",
            cols = APP_JOIN_COLS_SQL
        );
        let rows: Vec<AppJoinCols> = sqlx::query_as(&sql)
            .bind(category_id)
            .fetch_all(pool)
            .await?;

        Ok(rows
            .iter()
            .filter_map(AppJoinCols::to_app_model)
            .collect())
    }

    pub async fn batch_update_category(
        pool: &SqlitePool,
        req: crate::models::bundle::BatchCategoryRequest,
    ) -> Result<Vec<AppModel>, AppError> {
        debug!("batch_update_category: {} app(s) -> {}", req.app_ids.len(), req.category_id);
        if req.app_ids.is_empty() {
            return Ok(Vec::new());
        }

        let mut category_id = req.category_id;
        if category_id > 0 {
            let cat_exists: Option<(i64,)> = sqlx::query_as("SELECT ID FROM CategoryModels WHERE ID = ?")
                .bind(category_id)
                .fetch_optional(pool)
                .await?;
            if cat_exists.is_none() {
                warn!("batch_update_category: category_id={} not found, fallback to system category", category_id);
                category_id = 0;
            }
        }
        let category_id = if category_id > 0 {
            category_id
        } else {
            CategoryService::get_system_category_id(pool).await?
        };

        // 分块避开 SQLite 变量数上限；UPDATE 单条完成
        const CHUNK: usize = 500;
        for chunk in req.app_ids.chunks(CHUNK) {
            let mut qb = sqlx::QueryBuilder::new("UPDATE AppModels SET CategoryID = ");
            qb.push_bind(category_id);
            qb.push(" WHERE ID IN (");
            let mut sep = qb.separated(", ");
            for id in chunk {
                sep.push_bind(id);
            }
            sep.push_unseparated(")");
            qb.build().execute(pool).await?;
        }

        let mut apps = Vec::with_capacity(req.app_ids.len());
        for chunk in req.app_ids.chunks(CHUNK) {
            apps.extend(Self::get_apps_by_ids(pool, chunk).await?);
        }
        Ok(apps)
    }

    /// 按主键批量取应用（含分类联表），保持与 by-category 一致的行结构
    pub(crate) async fn get_apps_by_ids(
        pool: &SqlitePool,
        ids: &[i64],
    ) -> Result<Vec<AppModel>, AppError> {
        if ids.is_empty() {
            return Ok(Vec::new());
        }
        let sql = format!(
            "SELECT {cols} FROM AppModels a \
             LEFT JOIN CategoryModels c ON a.CategoryID = c.ID \
             WHERE a.ID IN ({})",
            crate::utils::in_clause(ids),
            cols = APP_JOIN_COLS_SQL
        );
        let mut q = sqlx::query_as::<_, AppJoinCols>(&sql);
        for id in ids {
            q = q.bind(*id);
        }
        let rows = q.fetch_all(pool).await?;
        Ok(rows.iter().filter_map(AppJoinCols::to_app_model).collect())
    }
}
