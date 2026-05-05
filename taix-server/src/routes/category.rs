use axum::{
    extract::{Path, Query, State},
    routing::{get},
    Json, Router,
};
use serde::Deserialize;
use sqlx::SqlitePool;

use crate::models::category::CategoryModel;
use crate::models::request::{CreateCategoryRequest, UpdateCategoryRequest};
use crate::response::ApiResponse;
use crate::services::category::CategoryService;
use axum::routing::post;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CategoryQuery {
    #[serde(default, deserialize_with = "crate::models::request::deserialize_bool_insensitive")]
    contain_system_category: bool,
}

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/category", get(get_categories).post(create_category))
        .route("/api/category/:id", get(get_category).put(update_category).delete(delete_category))
        .route("/api/category/:id/restore", post(restore_system_category))
}

async fn get_categories(
    State(pool): State<SqlitePool>,
    Query(query): Query<CategoryQuery>,
) -> Json<ApiResponse<Vec<CategoryModel>>> {
    match CategoryService::get_categories(&pool, query.contain_system_category).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_category(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
) -> Json<ApiResponse<Option<CategoryModel>>> {
    match CategoryService::get_category(&pool, id).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn create_category(
    State(pool): State<SqlitePool>,
    Json(req): Json<CreateCategoryRequest>,
) -> Json<ApiResponse<CategoryModel>> {
    match CategoryService::create_category(&pool, req).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn update_category(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
    Json(req): Json<UpdateCategoryRequest>,
) -> Json<ApiResponse<()>> {
    match CategoryService::update_category(&pool, id, req).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(crate::error::AppError::Business(msg)) => Json(ApiResponse {
            code: 400,
            message: msg,
            data: None,
        }),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn restore_system_category(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
) -> Json<ApiResponse<CategoryModel>> {
    match CategoryService::restore_system_category(&pool, id).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(crate::error::AppError::Business(msg)) => Json(ApiResponse {
            code: 400,
            message: msg,
            data: None,
        }),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn delete_category(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
) -> Json<ApiResponse<()>> {
    match CategoryService::delete_category(&pool, id).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(crate::error::AppError::Business(msg)) => Json(ApiResponse {
            code: 400,
            message: msg,
            data: None,
        }),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}
