use sqlx::SqlitePool;
use tracing::{debug, info, warn};

use crate::error::AppError;
use crate::models::app::{AppModel, AppModelRow};
use crate::models::category::CategoryModel;
use crate::models::request::{CreateAppRequest, UpdateAppRequest};
use crate::services::category::CategoryService;

pub struct AppDataService;

impl AppDataService {
    pub async fn get_all_apps(pool: &SqlitePool) -> Result<Vec<AppModel>, AppError> {
        debug!("get_all_apps");
        let rows: Vec<AppModelRow> = sqlx::query_as(
            r#"
            SELECT a.* FROM AppModels a
            ORDER BY a.ID
            "#,
        )
        .fetch_all(pool)
        .await?;

        let categories: Vec<CategoryModel> = sqlx::query_as("SELECT * FROM CategoryModels")
            .fetch_all(pool)
            .await?;
        let category_map: std::collections::HashMap<i64, CategoryModel> =
            categories.into_iter().map(|c| (c.id, c)).collect();

        let mut apps = Vec::with_capacity(rows.len());
        for row in rows {
            let category = if row.category_id > 0 {
                category_map.get(&row.category_id).cloned()
            } else {
                None
            };

            apps.push(AppModel {
                id: row.id,
                name: row.name,
                alias: row.alias,
                description: row.description,
                file: row.file,
                category_id: row.category_id,
                icon_file: row.icon_file,
                total_time: row.total_time,
                category,
            });
        }

        Ok(apps)
    }

    pub async fn get_app(pool: &SqlitePool, id: i64) -> Result<Option<AppModel>, AppError> {
        debug!("get_app: id={}", id);
        let row: Option<AppModelRow> =
            sqlx::query_as("SELECT * FROM AppModels WHERE ID = ?")
                .bind(id)
                .fetch_optional(pool)
                .await?;

        match row {
            Some(row) => {
                let category = if row.category_id > 0 {
                    sqlx::query_as::<_, CategoryModel>(
                        "SELECT * FROM CategoryModels WHERE ID = ?",
                    )
                    .bind(row.category_id)
                    .fetch_optional(pool)
                    .await?
                } else {
                    None
                };

                Ok(Some(AppModel {
                    id: row.id,
                    name: row.name,
                    alias: row.alias,
                    description: row.description,
                    file: row.file,
                    category_id: row.category_id,
                    icon_file: row.icon_file,
                    total_time: row.total_time,
                    category,
                }))
            }
            None => Ok(None),
        }
    }

    pub async fn get_app_by_name(
        pool: &SqlitePool,
        name: &str,
    ) -> Result<Option<AppModel>, AppError> {
        debug!("get_app_by_name: name={}", name);
        let row: Option<AppModelRow> =
            sqlx::query_as("SELECT * FROM AppModels WHERE Name = ?")
                .bind(name)
                .fetch_optional(pool)
                .await?;

        match row {
            Some(row) => {
                let category = if row.category_id > 0 {
                    sqlx::query_as::<_, CategoryModel>(
                        "SELECT * FROM CategoryModels WHERE ID = ?",
                    )
                    .bind(row.category_id)
                    .fetch_optional(pool)
                    .await?
                } else {
                    None
                };

                Ok(Some(AppModel {
                    id: row.id,
                    name: row.name,
                    alias: row.alias,
                    description: row.description,
                    file: row.file,
                    category_id: row.category_id,
                    icon_file: row.icon_file,
                    total_time: row.total_time,
                    category,
                }))
            }
            None => Ok(None),
        }
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
            SET Name = ?, Alias = ?, Description = ?, File = ?, IconFile = ?, CategoryID = ?, TotalTime = ?
            WHERE ID = ?
            "#,
        )
        .bind(&req.name)
        .bind(&req.alias)
        .bind(&req.description)
        .bind(&req.file)
        .bind(&req.icon_file)
        .bind(category_id)
        .bind(req.total_time)
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
        let rows: Vec<AppModelRow> =
            sqlx::query_as("SELECT * FROM AppModels WHERE CategoryID = ?")
                .bind(category_id)
                .fetch_all(pool)
                .await?;

        let mut apps = Vec::new();
        for row in rows {
            apps.push(AppModel {
                id: row.id,
                name: row.name,
                alias: row.alias,
                description: row.description,
                file: row.file,
                category_id: row.category_id,
                icon_file: row.icon_file,
                total_time: row.total_time,
                category: None,
            });
        }

        Ok(apps)
    }
}
