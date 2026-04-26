use sqlx::SqlitePool;
use tracing::{debug, info, warn};

use crate::error::AppError;
use crate::models::category::CategoryModel;
use crate::models::request::{CreateCategoryRequest, UpdateCategoryRequest};

pub struct CategoryService;

impl CategoryService {
    pub async fn get_system_category_id(pool: &SqlitePool) -> Result<i64, AppError> {
        let id: Option<(i64,)> =
            sqlx::query_as("SELECT ID FROM CategoryModels WHERE IsSystem = 1 LIMIT 1")
                .fetch_optional(pool)
                .await?;

        match id {
            Some((id,)) => Ok(id),
            None => Err(AppError::Internal(
                "System category not found".to_string(),
            )),
        }
    }
    pub async fn get_categories(
        pool: &SqlitePool,
        contain_system_category: bool,
    ) -> Result<Vec<CategoryModel>, AppError> {
        debug!("get_categories: contain_system={}", contain_system_category);
        let mut categories: Vec<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE IsSystem = 0")
                .fetch_all(pool)
                .await?;

        if contain_system_category {
            let system: Option<CategoryModel> =
                sqlx::query_as("SELECT * FROM CategoryModels WHERE IsSystem = 1 LIMIT 1")
                    .fetch_optional(pool)
                    .await?;

            if let Some(sys) = system {
                let mut result = vec![sys];
                result.append(&mut categories);
                return Ok(result);
            }
        }

        Ok(categories)
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
            INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMath, Directories)
            VALUES (?, ?, ?, ?, ?)
            "#,
        )
        .bind(&req.name)
        .bind(&req.icon_file)
        .bind(&req.color)
        .bind(req.is_directory_math)
        .bind(&req.directories)
        .execute(pool)
        .await?
        .last_insert_rowid();

        Ok(CategoryModel {
            id,
            name: Some(req.name),
            icon_file: req.icon_file,
            color: req.color,
            is_directory_math: req.is_directory_math,
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
            warn!("update_category: id={} not found", id);
            return Ok(());
        };

        if existing.is_system {
            sqlx::query(
                "UPDATE CategoryModels SET IconFile = ?, Color = ? WHERE ID = ?",
            )
            .bind(&req.icon_file)
            .bind(&req.color)
            .bind(id)
            .execute(pool)
            .await?;
        } else {
            sqlx::query(
                r#"
                UPDATE CategoryModels
                SET Name = ?, IconFile = ?, Color = ?, IsDirectoryMath = ?, Directories = ?
                WHERE ID = ?
                "#,
            )
            .bind(&req.name)
            .bind(&req.icon_file)
            .bind(&req.color)
            .bind(req.is_directory_math)
            .bind(&req.directories)
            .bind(id)
            .execute(pool)
            .await?;
        }

        Ok(())
    }

    pub async fn delete_category(pool: &SqlitePool, id: i64) -> Result<(), AppError> {
        info!("delete_category: id={}", id);
        let existing: Option<CategoryModel> =
            sqlx::query_as("SELECT * FROM CategoryModels WHERE ID = ?")
                .bind(id)
                .fetch_optional(pool)
                .await?;

        match existing {
            Some(cat) if cat.is_system => {
                warn!("delete_category: id={} is system category, skip", id);
                return Ok(());
            }
            None => {
                warn!("delete_category: id={} not found, skip", id);
                return Ok(());
            }
            _ => {}
        }

        let system_id = Self::get_system_category_id(pool).await?;
        sqlx::query("UPDATE AppModels SET CategoryID = ? WHERE CategoryID = ?")
            .bind(system_id)
            .bind(id)
            .execute(pool)
            .await?;

        sqlx::query("DELETE FROM CategoryModels WHERE ID = ?")
            .bind(id)
            .execute(pool)
            .await?;

        Ok(())
    }
}
