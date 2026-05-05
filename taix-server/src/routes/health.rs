use axum::{extract::State, http::StatusCode, routing::get, Router};
use sqlx::SqlitePool;

pub fn router() -> Router<SqlitePool> {
    Router::new().route("/api/health", get(health_check))
}

async fn health_check(State(pool): State<SqlitePool>) -> StatusCode {
    match sqlx::query_scalar::<_, i32>("SELECT 1").fetch_one(&pool).await {
        Ok(_) => StatusCode::OK,
        Err(_) => StatusCode::SERVICE_UNAVAILABLE,
    }
}
