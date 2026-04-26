use axum::{
    extract::{Path, State},
    routing::{get},
    Json, Router,
};
use sqlx::SqlitePool;

use crate::models::app::AppModel;
use crate::models::request::{CreateAppRequest, UpdateAppRequest};
use crate::response::ApiResponse;
use crate::services::app_data::AppDataService;

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/appdata", get(get_all_apps).post(create_app))
        .route("/api/appdata/{id}", get(get_app).put(update_app))
        .route("/api/appdata/by-name/{name}", get(get_app_by_name))
        .route("/api/appdata/by-category/{categoryId}", get(get_apps_by_category))
}

async fn get_all_apps(State(pool): State<SqlitePool>) -> Json<ApiResponse<Vec<AppModel>>> {
    match AppDataService::get_all_apps(&pool).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_app(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
) -> Json<ApiResponse<Option<AppModel>>> {
    match AppDataService::get_app(&pool, id).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_app_by_name(
    State(pool): State<SqlitePool>,
    Path(name): Path<String>,
) -> Json<ApiResponse<Option<AppModel>>> {
    match AppDataService::get_app_by_name(&pool, &name).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn create_app(
    State(pool): State<SqlitePool>,
    Json(req): Json<CreateAppRequest>,
) -> Json<ApiResponse<AppModel>> {
    match AppDataService::create_app(&pool, req).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn update_app(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
    Json(req): Json<UpdateAppRequest>,
) -> Json<ApiResponse<()>> {
    match AppDataService::update_app(&pool, id, req).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_apps_by_category(
    State(pool): State<SqlitePool>,
    Path(category_id): Path<i64>,
) -> Json<ApiResponse<Vec<AppModel>>> {
    match AppDataService::get_apps_by_category(&pool, category_id).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}
