#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod db;
mod error;
mod models;
mod pipe;
mod response;
mod routes;
mod services;
mod websocket_manager;

use axum::Router;
use sqlx::SqlitePool;
use std::net::SocketAddr;
use std::path::PathBuf;
use std::sync::Arc;
use tokio::sync::broadcast;
use tower_http::cors::CorsLayer;
use tower_http::trace::TraceLayer;
use tracing_subscriber::Layer;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt};

use routes::web_sentry::SentryState;
use services::config::ConfigService;

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    let args: Vec<String> = std::env::args().collect();

    let exe_dir = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_else(|| std::env::current_dir().unwrap());

    let data_dir = parse_data_dir(&args, &exe_dir);

    let log_dir = exe_dir.join("Logs");
    tokio::fs::create_dir_all(&log_dir).await?;
    let file_appender = tracing_appender::rolling::daily(&log_dir, "taix-server.log");
    let (non_blocking, _guard) = tracing_appender::non_blocking(file_appender);

    let filter = tracing_subscriber::EnvFilter::try_from_default_env()
        .unwrap_or_else(|_| "taix_server=debug,tower_http=debug".into());

    let stdout_layer = tracing_subscriber::fmt::layer().boxed();
    let file_layer = tracing_subscriber::fmt::layer()
        .with_writer(non_blocking)
        .with_ansi(false)
        .boxed();

    tracing_subscriber::registry()
        .with(filter)
        .with(stdout_layer)
        .with(file_layer)
        .init();

    // 捕获 panic 并记录到日志
    let panic_log_dir = log_dir.clone();
    std::panic::set_hook(Box::new(move |info| {
        let payload = if let Some(s) = info.payload().downcast_ref::<&str>() {
            s.to_string()
        } else if let Some(s) = info.payload().downcast_ref::<String>() {
            s.clone()
        } else {
            "Unknown panic payload".to_string()
        };

        let location = if let Some(loc) = info.location() {
            format!("{}:{}:{}", loc.file(), loc.line(), loc.column())
        } else {
            "unknown location".to_string()
        };

        let now = chrono::Local::now().format("%Y-%m-%dT%H:%M:%S%.3f%:z");
        let message = format!(
            "[{}] ERROR taix_server: PANIC occurred at {}\n    payload: {}\n",
            now, location, payload
        );

        // 同步写入 panic 日志文件，确保即使程序立即终止也能落盘
        let panic_log_path = panic_log_dir.join("taix-server-panic.log");
        if let Ok(mut file) = std::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(&panic_log_path)
        {
            use std::io::Write;
            let _ = file.write_all(message.as_bytes());
            let _ = file.flush();
        }

        // 同时通过 tracing 输出
        tracing::error!("PANIC occurred at {}: {}", location, payload);

        // 输出到 stderr，方便调试时直接看到
        eprintln!("{}", message);
    }));

    let web_favicons_dir = data_dir.join("WebFavicons");

    let db_path = std::env::var("TAIX_DB_PATH")
        .map(PathBuf::from)
        .unwrap_or_else(|_| data_dir.join("data.db"));

    tracing::info!("Using database: {}", db_path.display());
    tracing::info!("Using data dir: {}", data_dir.display());
    tracing::info!("Using web favicons dir: {}", web_favicons_dir.display());

    let pool = db::init_db(db_path.to_str().unwrap()).await?;
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

    let (tx, _rx) = broadcast::channel(128);
    let sentry_state = Arc::new(SentryState {
        pool: pool.clone(),
        is_sleep: tokio::sync::RwLock::new(false),
        tx,
        web_favicons_dir,
        semaphore: Arc::new(tokio::sync::Semaphore::new(16)),
    });

    let app = create_app(pool.clone(), config_service, web_enabled_tx.clone());

    let addr: SocketAddr = std::env::var("TAIX_BIND")
        .unwrap_or_else(|_| "0.0.0.0:5000".to_string())
        .parse()?;

    tracing::info!("Server listening on http://{}", addr);

    let listener = tokio::net::TcpListener::bind(addr).await?;
    let http_server = axum::serve(listener, app);

    tokio::spawn(websocket_manager::run(web_enabled_rx, sentry_state.clone()));

    tokio::spawn(pipe::start(sentry_state, pool.clone()));

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
        .unwrap_or_else(|_| exe_dir.join("Data"))
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
