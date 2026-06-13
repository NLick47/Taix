pub const SECS_PER_MIN: i64 = 60;
pub const MINS_PER_HOUR: i64 = 60;
pub const SECS_PER_HOUR: i64 = 3_600;
pub const SECS_PER_DAY: i64 = 86_400;

pub const DEFAULT_HTTP_PORT: u16 = 37_091;
pub const DEFAULT_WEBSOCKET_PORT: u16 = 8_908;
pub const BROADCAST_CHANNEL_CAPACITY: usize = 128;
pub const MAX_CONCURRENT_PIPE_CLIENTS: usize = 16;

pub const PIPE_CONNECT_RETRY_INTERVAL_SECS: u64 = 1;
#[cfg(target_os = "windows")]
pub const PIPE_CREATE_RETRY_INTERVAL_SECS: u64 = 3;

pub const MIN_VALID_TIMESTAMP: i64 = 1_577_836_800;
pub const TIMESTAMP_FUTURE_TOLERANCE_SECS: i64 = 60;
pub const MIN_VALID_YEAR: i32 = 2_000;
pub const FUTURE_TIMESTAMP_TOLERANCE_MINUTES: i64 = 5;

pub const MAX_WEB_DURATION_SECS: i64 = SECS_PER_HOUR * 4;
pub const MAX_SITE_DURATION_SECS: i64 = 3_153_600_000;
pub const MAX_ADD_URL_ITERATIONS: usize = 48;
pub const DAY_VIEW_THRESHOLD: i64 = 31;

pub const ICON_DOWNLOAD_TIMEOUT_SECS: u64 = 10;
pub const SVG_PREFIX_CHECK_LEN: usize = 256;
pub const MAX_ICON_SIZE_PX: u32 = 128;

pub const MIGRATION_BATCH_SIZE: usize = 1_000;

pub const DEFAULT_FREQUENT_USE_NUM: i32 = 2;
pub const DEFAULT_MORE_NUM: i32 = 11;

#[cfg(target_os = "macos")]
pub fn default_data_dir() -> std::path::PathBuf {
    dirs::data_local_dir()
        .unwrap_or_else(|| std::path::PathBuf::from("/tmp"))
        .join("Taix")
}

#[cfg(target_os = "windows")]
pub fn default_data_dir() -> std::path::PathBuf {
    std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf().join("Data")))
        .unwrap_or_else(|| std::path::PathBuf::from("Data"))
}
