use std::path::PathBuf;

use axum::{http::StatusCode, response::IntoResponse, routing::any, Router};
use sqlx::SqlitePool;
use tower_http::services::{ServeDir, ServeFile};

/// TAIX_WEBUI_DIR 优先，其次 <exe_dir>/webui；SPA 路由回退 index.html
pub fn router(webui_dir: PathBuf) -> Router<SqlitePool> {
    if webui_dir.is_dir() {
        Router::new().nest_service(
            "/ui",
            ServeDir::new(&webui_dir).fallback(ServeFile::new(webui_dir.join("index.html"))),
        )
    } else {
        tracing::info!(
            target: "taix_server::plugins",
            "webui dir {} not found; /ui returns a hint (set TAIX_WEBUI_DIR in dev)",
            webui_dir.display()
        );
        async fn hint() -> impl IntoResponse {
            (
                StatusCode::NOT_FOUND,
                "webui assets not found. Build taix-webui or set TAIX_WEBUI_DIR.",
            )
        }
        Router::new()
            .route("/ui", any(hint))
            .route("/ui/", any(hint))
            .route("/ui/*path", any(hint))
    }
}
