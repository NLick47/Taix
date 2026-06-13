#![cfg_attr(all(windows, not(debug_assertions)), windows_subsystem = "windows")]

mod constants;
mod db;
mod error;
mod models;
mod pipe;
mod response;
mod routes;
mod services;
mod single_instance;
mod utils;
mod websocket_manager;

use anyhow::Context;
use axum::Router;
use sqlx::SqlitePool;
use std::net::SocketAddr;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use tokio::sync::broadcast;
use tower_http::cors::CorsLayer;
use tower_http::trace::TraceLayer;
use routes::web_sentry::SentryState;
use services::config::ConfigService;

#[tokio::main]
async fn main() {
    let exe_dir = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_else(|| std::env::current_dir().unwrap());

    let log_dir = exe_dir.join("Logs");

    let log_filter = if cfg!(debug_assertions) {
        "taix_server=debug,tower_http=debug"
    } else {
        "taix_server=info,tower_http=warn"
    };

    let _logging_guard =
        taix_logging::init("taix-server", log_filter, taix_logging::PanicMode::SyncFile);

    if let Err(e) = run(&exe_dir).await {
        tracing::error!("FATAL: {}", e);

        let now = chrono::Utc::now().format("%Y-%m-%dT%H:%M:%S%.3fZ");
        let message = format!("[{}] ERROR taix_server: FATAL: {}\n", now, e);

        let today = chrono::Utc::now().format("%Y-%m-%d");
        let log_path = log_dir.join(format!("taix-server.{}.log", today));
        if let Ok(mut file) = std::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(&log_path)
        {
            use std::io::Write;
            let _ = file.write_all(message.as_bytes());
            let _ = file.flush();
        }

        eprintln!("{}", message);
    }
}

async fn run(exe_dir: &Path) -> anyhow::Result<()> {
    let args: Vec<String> = std::env::args().collect();

    let data_dir = parse_data_dir(&args, exe_dir);

    let _single_instance = single_instance::try_acquire("Global\\TaixServerSingleInstance")
        .ok_or_else(|| anyhow::anyhow!("Another instance of taix-server is already running"))?;

    let web_favicons_dir = exe_dir.join("WebFavicons");

    let db_path = std::env::var("TAIX_DB_PATH")
        .map(PathBuf::from)
        .unwrap_or_else(|_| data_dir.join("data.db"));

    tracing::info!("Using database: {}", db_path.display());
    tracing::info!("Using data dir: {}", data_dir.display());
    tracing::info!("Using web favicons dir: {}", web_favicons_dir.display());

    let pool = db::init_db(
        db_path.to_str().ok_or_else(|| anyhow::anyhow!("Database path contains invalid UTF-8"))?,
        "Asia/Shanghai",
    ).await?;
    let config_service = Arc::new(ConfigService::new(&data_dir));

    // 读取初始配置，决定 WebSocket 是否启动
    let initial_web_enabled = match config_service.load().await {
        Ok(config) => config.general.is_web_enabled,
        Err(e) => {
            tracing::warn!("Failed to load initial config, defaulting web_enabled to false: {}", e);
            false
        }
    };

    let (web_enabled_tx, web_enabled_rx) = tokio::sync::watch::channel(initial_web_enabled);

    let (tx, _rx) = broadcast::channel(constants::BROADCAST_CHANNEL_CAPACITY);
    let sentry_state = Arc::new(SentryState {
        pool: pool.clone(),
        is_sleep: tokio::sync::RwLock::new(false),
        tx,
        web_favicons_dir,
        semaphore: Arc::new(tokio::sync::Semaphore::new(constants::MAX_CONCURRENT_PIPE_CLIENTS)),
    });

    let app = create_app(pool.clone(), config_service, web_enabled_tx.clone());

    let addr: SocketAddr = parse_server_addr()?;

    tracing::info!("Server listening on http://{}", addr);

    let listener = tokio::net::TcpListener::bind(addr).await?;
    let http_server = axum::serve(listener, app);

    tokio::spawn(websocket_manager::run(web_enabled_rx, sentry_state.clone()));

    tokio::spawn(pipe::start(sentry_state, pool.clone()));

    tokio::spawn(services::app_timer::AppTimerService::run_cleanup_task(
        pool.clone(),
    ));

    http_server.await?;

    Ok(())
}

fn parse_data_dir(args: &[String], exe_dir: &std::path::Path) -> PathBuf {
    for i in 0..args.len() {
        if args[i] == "--data-dir" && i + 1 < args.len() {
            return PathBuf::from(&args[i + 1]);
        }
    }
    std::env::var("TAIX_DATA_DIR")
        .map(PathBuf::from)
        .unwrap_or_else(|_| constants::default_data_dir())
}

fn parse_server_addr() -> anyhow::Result<SocketAddr> {
    let raw = std::env::var("TAIX_SERVER")
        .unwrap_or_else(|_| format!("http://127.0.0.1:{}", constants::DEFAULT_HTTP_PORT));
    let stripped = raw
        .trim_start_matches("http://")
        .trim_start_matches("https://");
    stripped.parse().context("Failed to parse server address")
}

fn create_app(
    pool: SqlitePool,
    config_service: Arc<ConfigService>,
    web_enabled_tx: tokio::sync::watch::Sender<bool>,
) -> Router<()> {
    Router::new()
        .merge(routes::app_data::router())
        .merge(routes::category::router())
        .merge(routes::app_timer::router())
        .merge(routes::data::router())
        .merge(routes::web_data::router())
        .merge(routes::config::router())
        .merge(routes::health::router())
        .layer(TraceLayer::new_for_http())
        .layer(CorsLayer::permissive())
        .layer(axum::extract::Extension(config_service))
        .layer(axum::extract::Extension(web_enabled_tx))
        .with_state(pool)
}
