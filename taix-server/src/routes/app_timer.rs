use axum::{
    extract::State,
    routing::post,
    Json, Router,
};
use sqlx::SqlitePool;

use crate::models::request::UpdateAppDurationRequest;
use crate::response::ApiResponse;
use crate::services::app_timer::AppTimerService;

pub fn router() -> Router<SqlitePool> {
    Router::new().route("/api/apptimer/duration", post(update_app_duration))
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
