pub mod constants;
pub mod db;
pub mod error;
pub mod migrations;
pub mod models;
pub mod origin_check;
pub mod pipe;
pub mod plugins;
pub mod response;
pub mod routes;
pub mod services;
pub mod single_instance;
pub mod utils;
pub mod websocket_manager;

use anyhow::Context;
use axum::http::{header, Method, HeaderValue};
use axum::Router;
use sqlx::SqlitePool;
use std::net::SocketAddr;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tower_http::cors::CorsLayer;
use tower_http::trace::TraceLayer;

use services::config::ConfigService;

pub fn parse_data_dir(args: &[String]) -> PathBuf {
    for i in 0..args.len() {
        if args[i] == "--data-dir" && i + 1 < args.len() {
            return PathBuf::from(&args[i + 1]);
        }
    }
    std::env::var("TAIX_DATA_DIR")
        .map(PathBuf::from)
        .unwrap_or_else(|_| constants::default_data_dir())
}

pub fn parse_server_addr() -> anyhow::Result<SocketAddr> {
    let raw = std::env::var("TAIX_SERVER")
        .unwrap_or_else(|_| format!("http://127.0.0.1:{}", constants::DEFAULT_HTTP_PORT));
    let stripped = raw
        .trim_start_matches("http://")
        .trim_start_matches("https://");
    stripped.parse().context("Failed to parse server address")
}

pub fn create_app(
    pool: SqlitePool,
    config_service: Arc<ConfigService>,
    web_enabled_tx: tokio::sync::watch::Sender<bool>,
    plugin_state: Arc<plugins::PluginState>,
    exe_dir: &Path,
) -> Router<()> {
    // Origin 校验用；与实际监听地址一致
    let http_port = parse_server_addr()
        .map(|addr| addr.port())
        .unwrap_or(constants::DEFAULT_HTTP_PORT);
    let allowed_origins: Vec<String> = ["127.0.0.1", "localhost"]
        .iter()
        .map(|host| format!("http://{}:{}", host, http_port))
        .collect();

    // CORS 与 Origin 校验共用本地源；跨站 GET 响应体由浏览器拒读
    let cors = CorsLayer::new()
        .allow_origin(
            allowed_origins
                .iter()
                .filter_map(|o| HeaderValue::from_str(o).ok())
                .collect::<Vec<_>>(),
        )
        .allow_methods([Method::GET, Method::POST, Method::PUT, Method::DELETE])
        .allow_headers([header::CONTENT_TYPE]);

    let webui_dir = std::env::var("TAIX_WEBUI_DIR")
        .map(PathBuf::from)
        .unwrap_or_else(|_| exe_dir.join("webui"));

    Router::new()
        .merge(routes::app_data::router())
        .merge(routes::category::router())
        .merge(routes::category_summary::router())
        .merge(routes::app_timer::router())
        .merge(routes::data::router())
        .merge(routes::web_data::router())
        .merge(routes::config::router())
        .merge(routes::health::router())
        .merge(routes::plugins::router())
        .merge(routes::events::router())
        .merge(routes::ui::router(webui_dir))
        .layer(TraceLayer::new_for_http())
        .layer(cors)
        .layer(axum::middleware::from_fn_with_state(
            allowed_origins,
            origin_check::check,
        ))
        .layer(axum::extract::Extension(config_service))
        .layer(axum::extract::Extension(web_enabled_tx))
        .layer(axum::extract::Extension(plugin_state))
        .with_state(pool)
}
