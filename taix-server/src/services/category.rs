use sqlx::SqlitePool;
use tokio::sync::RwLock;
use tracing::{debug, info};

use crate::error::AppError;
use crate::models::category::CategoryModel;
use crate::models::request::{CreateCategoryRequest, UpdateCategoryRequest};

static CATEGORY_CACHE: std::sync::OnceLock<RwLock<Option<Vec<CategoryModel>>>> = std::sync::OnceLock::new();
static DIR_MATCH_CACHE: std::sync::OnceLock<RwLock<Option<Vec<(i64, Vec<String>)>>>> = std::sync::OnceLock::new();

fn category_cache() -> &'static RwLock<Option<Vec<CategoryModel>>> {
    CATEGORY_CACHE.get_or_init(|| RwLock::new(None))
}

fn dir_match_cache() -> &'static RwLock<Option<Vec<(i64, Vec<String>)>>> {
    DIR_MATCH_CACHE.get_or_init(|| RwLock::new(None))
}

async fn invalidate_category_cache() {
    let mut cache = category_cache().write().await;
    *cache = None;
    let mut dir_cache = dir_match_cache().write().await;
    *dir_cache = None;
}

pub struct CategoryService;

impl CategoryService {
    pub async fn get_system_category_id<'e, E>(executor: E) -> Result<i64, AppError>
    where
        E: sqlx::Executor<'e, Database = sqlx::Sqlite>,
    {
        let id: Option<(i64,)> =
            sqlx::query_as("SELECT ID FROM CategoryModels WHERE IsSystem = 1 LIMIT 1")
                .fetch_optional(executor)
                .await?;

        match id {
            Some((id,)) => Ok(id),
            None => Err(AppError::Internal(
                "System category not found".to_string(),
            )),
        }
    }

    pub async fn get_categories(pool: &SqlitePool) -> Result<Vec<CategoryModel>, AppError> {
        debug!("get_categories");

        {
            let cache = category_cache().read().await;
            if let Some(cached) = cache.as_ref() {
                debug!("get_categories from cache");
                return Ok(cached.clone());
            }
        }

        let mut categories: Vec<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE IsSystem = 0")
                .fetch_all(pool)
                .await?;

        let system: Option<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE IsSystem = 1 LIMIT 1")
                .fetch_optional(pool)
                .await?;

        let result = if let Some(sys) = system {
            let mut all = vec![sys];
            all.append(&mut categories);
            all
        } else {
            categories
        };

        let mut cache = category_cache().write().await;
        *cache = Some(result.clone());

        Ok(result)
    }

    pub async fn get_category(pool: &SqlitePool, id: i64) -> Result<Option<CategoryModel>, AppError> {
        debug!("get_category: id={}", id);
        let category: Option<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE ID = ?")
                .bind(id)
                .fetch_optional(pool)
                .await?;

        Ok(category)
    }

    pub async fn create_category(
        pool: &SqlitePool,
        req: CreateCategoryRequest,
    ) -> Result<CategoryModel, AppError> {
        info!("create_category: name={}", req.name);
        let id = sqlx::query(
            r#"
            INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories)
            VALUES (?, ?, ?, ?, ?)
            "#,
        )
        .bind(&req.name)
        .bind(&req.icon_file)
        .bind(&req.color)
        .bind(req.is_directory_match)
        .bind(&req.directories)
        .execute(pool)
        .await?
        .last_insert_rowid();

        invalidate_category_cache().await;

        Ok(CategoryModel {
            id,
            name: Some(req.name),
            icon_file: req.icon_file,
            color: req.color,
            is_directory_match: req.is_directory_match,
            directories: req.directories,
            is_system: false,
        })
    }

    pub async fn update_category(
        pool: &SqlitePool,
        id: i64,
        req: UpdateCategoryRequest,
    ) -> Result<(), AppError> {
        info!("update_category: id={}", id);
        let existing: Option<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE ID = ?")
                .bind(id)
                .fetch_optional(pool)
                .await?;

        let Some(existing) = existing else {
            return Err(AppError::Business("分类不存在".to_string()));
        };

        if existing.is_system {
            let req_dirs = req.directories.as_deref().filter(|s| !s.is_empty() && *s != "[]");
            let existing_dirs = existing.directories.as_deref().filter(|s| !s.is_empty() && *s != "[]");
            if req.is_directory_match != existing.is_directory_match || req_dirs != existing_dirs {
                return Err(AppError::Business(
                    "系统分类不能修改目录匹配规则".to_string(),
                ));
            }

            sqlx::query(
                "UPDATE CategoryModels SET Name = ?, IconFile = ?, Color = ? WHERE ID = ?",
            )
            .bind(&req.name)
            .bind(&req.icon_file)
            .bind(&req.color)
            .bind(id)
            .execute(pool)
            .await?;
        } else {
            sqlx::query(
                r#"
                UPDATE CategoryModels
                SET Name = ?, IconFile = ?, Color = ?, IsDirectoryMatch = ?, Directories = ?
                WHERE ID = ?
                "#,
            )
            .bind(&req.name)
            .bind(&req.icon_file)
            .bind(&req.color)
            .bind(req.is_directory_match)
            .bind(&req.directories)
            .bind(id)
            .execute(pool)
            .await?;
        }

        invalidate_category_cache().await;

        Ok(())
    }

    pub async fn restore_system_category(
        pool: &SqlitePool,
        id: i64,
    ) -> Result<CategoryModel, AppError> {
        info!("restore_system_category: id={}", id);

        let existing: Option<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE ID = ?")
                .bind(id)
                .fetch_optional(pool)
                .await?;

        let Some(existing) = existing else {
            return Err(AppError::Business("分类不存在".to_string()));
        };

        if !existing.is_system {
            return Err(AppError::Business("不是系统分类".to_string()));
        }

        sqlx::query(
            "UPDATE CategoryModels SET Name = ?, IconFile = ?, Color = ? WHERE ID = ?",
        )
        .bind(crate::db::DEFAULT_CATEGORY)
        .bind(crate::db::DEFAULT_ICON)
        .bind(crate::db::DEFAULT_COLOR)
        .bind(id)
        .execute(pool)
        .await?;

        invalidate_category_cache().await;

        Ok(CategoryModel {
            id,
            name: Some(crate::db::DEFAULT_CATEGORY.to_string()),
            icon_file: Some(crate::db::DEFAULT_ICON.to_string()),
            color: Some(crate::db::DEFAULT_COLOR.to_string()),
            is_directory_match: false,
            directories: Some("[]".to_string()),
            is_system: true,
        })
    }

    pub async fn delete_category(pool: &SqlitePool, id: i64) -> Result<(), AppError> {
        info!("delete_category: id={}", id);

        let mut tx = pool.begin().await?;

        let existing: Option<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE ID = ?")
                .bind(id)
                .fetch_optional(&mut *tx)
                .await?;

        let Some(existing) = existing else {
            return Err(AppError::Business("分类不存在".to_string()));
        };

        if existing.is_system {
            return Err(AppError::Business("系统分类不能删除".to_string()));
        }

        let app_count: i64 =
            sqlx::query_scalar("SELECT COUNT(*) FROM AppModels WHERE CategoryID = ?")
                .bind(id)
                .fetch_one(&mut *tx)
                .await?;

        if app_count > 0 {
            let system_category_id: i64 =
                sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE IsSystem = 1 LIMIT 1")
                    .fetch_one(&mut *tx)
                    .await?;

            sqlx::query("UPDATE AppModels SET CategoryID = ? WHERE CategoryID = ?")
                .bind(system_category_id)
                .bind(id)
                .execute(&mut *tx)
                .await?;
        }

        sqlx::query("DELETE FROM CategoryModels WHERE ID = ?")
            .bind(id)
            .execute(&mut *tx)
            .await?;

        tx.commit().await?;
        invalidate_category_cache().await;

        Ok(())
    }

    pub async fn match_category_by_path(pool: &SqlitePool, file_path: Option<&str>) -> Option<i64> {
        let file_path = file_path?;
        if file_path.is_empty() {
            return None;
        }

        {
            let cache = dir_match_cache().read().await;
            if let Some(cached) = cache.as_ref() {
                return match_against_rules(cached, file_path);
            }
        }

        let categories = Self::get_categories(pool).await.ok()?;
        let rules = build_match_rules(&categories);
        let result = match_against_rules(&rules, file_path);

        let mut cache = dir_match_cache().write().await;
        *cache = Some(rules);

        result
    }

   
    pub async fn apply_directory_match(pool: &SqlitePool) -> Result<usize, AppError> {
        let categories = Self::get_categories(pool).await?;

        let rules = build_match_rules(&categories);
        if rules.is_empty() {
            return Ok(0);
        }

        let apps: Vec<(i64, Option<String>)> = sqlx::query_as(
            "SELECT ID, File FROM AppModels WHERE File IS NOT NULL"
        )
        .fetch_all(pool)
        .await?;

        // Hot path: pure in-memory matching — zero allocations per iteration beyond the vec
        let mut to_update: Vec<(i64, i64)> = Vec::new();
        for (app_id, file) in &apps {
            if let Some(path) = file {
                if let Some(cat_id) = match_against_rules(&rules, path) {
                    to_update.push((*app_id, cat_id));
                }
            }
        }

        if to_update.is_empty() {
            return Ok(0);
        }

        let mut tx = pool.begin().await?;
        let mut updated = 0usize;
        for (app_id, category_id) in &to_update {
            let result = sqlx::query(
                "UPDATE AppModels SET CategoryID = ? WHERE ID = ? AND CategoryID != ?"
            )
            .bind(category_id)
            .bind(app_id)
            .bind(category_id)
            .execute(&mut *tx)
            .await?;
            if result.rows_affected() > 0 {
                updated += 1;
            }
        }
        tx.commit().await?;

        Ok(updated)
    }
}

fn build_match_rules(categories: &[CategoryModel]) -> Vec<(i64, Vec<String>)> {
    categories
        .iter()
        .filter(|c| c.is_directory_match && !c.is_system)
        .filter_map(|c| {
            let dirs: Vec<String> = c.directories.as_ref()
                .and_then(|d| serde_json::from_str::<Vec<String>>(d).ok())
                .unwrap_or_default()
                .into_iter()
                .map(|d| d.trim().trim_end_matches(['/', '\\']).to_lowercase())
                .filter(|d| !d.is_empty())
                .collect();
            if dirs.is_empty() {
                None
            } else {
                Some((c.id, dirs))
            }
        })
        .collect()
}

#[inline]
fn match_against_rules(rules: &[(i64, Vec<String>)], file_path: &str) -> Option<i64> {
    let path_lower = file_path.to_lowercase();
    for (category_id, dirs) in rules {
        for dir in dirs {
            if path_lower.starts_with(dir) {
                let after = &path_lower[dir.len()..];
                if after.is_empty() || after.starts_with('\\') || after.starts_with('/') {
                    return Some(*category_id);
                }
            }
        }
    }
    None
}
