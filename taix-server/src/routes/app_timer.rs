use axum::{
    extract::{Extension, State},
    routing::post,
    Json, Router,
};
use sqlx::SqlitePool;
use std::sync::Arc;

use crate::models::request::UpdateAppDurationRequest;
use crate::response::ApiResponse;
use crate::services::app_timer::AppTimerService;
use crate::services::config::ConfigService;

pub fn router() -> Router<SqlitePool> {
    Router::new().route("/api/apptimer/duration", post(update_app_duration))
}

async fn update_app_duration(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Json(req): Json<UpdateAppDurationRequest>,
) -> Json<ApiResponse<()>> {
    match AppTimerService::update_app_duration(&pool, req, &config_service).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}
