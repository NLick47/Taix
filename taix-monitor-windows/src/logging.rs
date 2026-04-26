use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt, Layer};

pub fn init() {
    let exe_dir = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
        .unwrap_or_else(|| std::env::current_dir().unwrap());

    let log_dir = exe_dir.join("Logs");
    std::fs::create_dir_all(&log_dir).ok();

    let file_appender = tracing_appender::rolling::RollingFileAppender::builder()
        .rotation(tracing_appender::rolling::Rotation::DAILY)
        .filename_prefix("taix-monitor")
        .filename_suffix("log")
        .max_log_files(31)
        .build(&log_dir)
        .expect("Failed to create rolling file appender");
    let (non_blocking, guard) = tracing_appender::non_blocking(file_appender);
    let _guard = Box::leak(Box::new(guard));

    let filter = tracing_subscriber::EnvFilter::try_from_default_env()
        .unwrap_or_else(|_| "taix_monitor_windows=info".into());

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
}
