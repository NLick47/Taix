use axum::{http::StatusCode, routing::get, Router};
use sqlx::SqlitePool;

pub fn router() -> Router<SqlitePool> {
    Router::new().route("/api/health", get(health_check))
}

async fn health_check() -> StatusCode {
    StatusCode::OK
}
