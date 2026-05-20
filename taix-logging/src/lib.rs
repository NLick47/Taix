use tracing_appender::non_blocking::WorkerGuard;
use tracing_subscriber::{layer::SubscriberExt, util::SubscriberInitExt, Layer};

pub enum PanicMode {
    /// 同步落盘，适合日志量低的场景。
    LogOnly,
    /// 额外同步写入当天的日志文件，防止 `panic = "abort"` 时日志来不及刷盘。
    SyncFile,
}

pub struct LoggingGuard(pub Option<WorkerGuard>);

impl Drop for LoggingGuard {
    fn drop(&mut self) {
        if let Some(guard) = self.0.take() {
            drop(guard);
        }
    }
}

pub fn init(name: &str, default_filter: &str, panic_mode: PanicMode) -> LoggingGuard {
    let log_dir = dirs::data_dir()
        .map(|d| d.join("Taix").join("Logs"))
        .unwrap_or_else(|| {
            std::env::current_exe()
                .ok()
                .and_then(|p| p.parent().map(|p| p.to_path_buf().join("Logs")))
                .unwrap_or_else(|| std::path::PathBuf::from(".").join("Logs"))
        });
    let _ = std::fs::create_dir_all(&log_dir);

    let file_appender = tracing_appender::rolling::RollingFileAppender::builder()
        .rotation(tracing_appender::rolling::Rotation::DAILY)
        .filename_prefix(name)
        .filename_suffix("log")
        .max_log_files(31)
        .build(&log_dir)
        .expect("failed to create rolling file appender");

    let filter = tracing_subscriber::EnvFilter::try_from_default_env()
        .unwrap_or_else(|_| default_filter.into());

    match panic_mode {
        PanicMode::LogOnly => {
            let file_layer = tracing_subscriber::fmt::layer()
                .with_writer(file_appender)
                .with_ansi(false)
                .with_file(true)
                .with_line_number(true)
                .boxed();

            #[cfg(debug_assertions)]
            {
                let stdout_layer = tracing_subscriber::fmt::layer()
                    .with_file(true)
                    .with_line_number(true)
                    .boxed();
                tracing_subscriber::registry()
                    .with(filter)
                    .with(stdout_layer)
                    .with(file_layer)
                    .init();
            }
            #[cfg(not(debug_assertions))]
            {
                tracing_subscriber::registry()
                    .with(filter)
                    .with(file_layer)
                    .init();
            }

            install_panic_hook(name, &log_dir, panic_mode);
            LoggingGuard(None)
        }
        PanicMode::SyncFile => {
            let (non_blocking, guard) = tracing_appender::non_blocking(file_appender);

            let stdout_layer = tracing_subscriber::fmt::layer()
                .with_file(true)
                .with_line_number(true)
                .boxed();
            let file_layer = tracing_subscriber::fmt::layer()
                .with_writer(non_blocking)
                .with_ansi(false)
                .with_file(true)
                .with_line_number(true)
                .boxed();

            tracing_subscriber::registry()
                .with(filter)
                .with(stdout_layer)
                .with(file_layer)
                .init();

            install_panic_hook(name, &log_dir, panic_mode);
            LoggingGuard(Some(guard))
        }
    }
}

fn install_panic_hook(name: &str, log_dir: &std::path::Path, mode: PanicMode) {
    let name = name.to_owned();
    let log_dir = log_dir.to_path_buf();

    match mode {
        PanicMode::LogOnly => {
            let default = std::panic::take_hook();
            std::panic::set_hook(Box::new(move |info| {
                tracing::error!(target: "panic", "{}", info);
                default(info);
            }));
        }
        PanicMode::SyncFile => {
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

                let now = chrono::Utc::now().format("%Y-%m-%dT%H:%M:%S%.3fZ");
                let message = format!(
                    "[{}] ERROR {}: PANIC occurred at {}\n    payload: {}\n",
                    now, name, location, payload
                );

                let today = chrono::Utc::now().format("%Y-%m-%d");
                let log_path = log_dir.join(format!("{}.{}.log", name, today));
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
            }));
        }
    }
}
