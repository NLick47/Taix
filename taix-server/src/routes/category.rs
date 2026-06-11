use axum::{
    extract::{Path, State},
    routing::{get},
    Json, Router,
};
use sqlx::SqlitePool;

use crate::models::category::CategoryModel;
use crate::models::request::{CreateCategoryRequest, UpdateCategoryRequest};
use crate::response::ApiResponse;
use crate::services::category::CategoryService;
use axum::routing::post;

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/category", get(get_categories).post(create_category))
        .route("/api/category/:id", get(get_category).put(update_category).delete(delete_category))
        .route("/api/category/:id/restore", post(restore_system_category))
        .route("/api/category/apply-directory-match", post(apply_directory_match))
}

async fn get_categories(
    State(pool): State<SqlitePool>,
) -> Json<ApiResponse<Vec<CategoryModel>>> {
    match CategoryService::get_categories(&pool).await {
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

async fn apply_directory_match(
    State(pool): State<SqlitePool>,
) -> Json<ApiResponse<usize>> {
    match CategoryService::apply_directory_match(&pool).await {
        Ok(count) => Json(ApiResponse::ok(count)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}
