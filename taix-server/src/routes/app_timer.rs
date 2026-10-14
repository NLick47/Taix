use axum::{
    extract::State,
    routing::{get, post},
    Extension, Json, Router,
};
use serde_json::Value;
use sqlx::SqlitePool;
use std::sync::Arc;

use crate::models::request::UpdateAppDurationRequest;
use crate::plugins::current;
use crate::plugins::PluginState;
use crate::response::ApiResponse;
use crate::services::app_timer::AppTimerService;

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/apptimer/duration", post(update_app_duration))
        .route("/api/apptimer/current", get(current_app))
}

async fn update_app_duration(
    State(pool): State<SqlitePool>,
    Json(req): Json<UpdateAppDurationRequest>,
) -> Json<ApiResponse<()>> {
    match AppTimerService::update_app_duration(&pool, req).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn current_app(
    State(pool): State<SqlitePool>,
    Extension(plugins): Extension<Arc<PluginState>>,
) -> Json<ApiResponse<Value>> {
    Json(ApiResponse::ok(current::snapshot(&plugins, &pool).await))
}
