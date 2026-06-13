#[cfg(target_os = "windows")]
pub const CLIENT_PIPE_NAME: &str = r"\\.\pipe\TaixClient";

#[cfg(target_os = "windows")]
pub const CLIENT_EXE_NAME: &str = "Taix.exe";

#[cfg(target_os = "macos")]
pub const CLIENT_EXE_NAME: &str = "Taix";

#[cfg(target_os = "windows")]
pub const MONITOR_EXE_NAME: &str = "taix-monitor-windows.exe";

#[cfg(target_os = "macos")]
pub const MONITOR_EXE_NAME: &str = "taix-monitor-macos";

#[cfg(target_os = "windows")]
pub const SERVER_EXE_NAME: &str = "taix-server.exe";

#[cfg(target_os = "macos")]
pub const SERVER_EXE_NAME: &str = "taix-server";

#[cfg(target_os = "windows")]
pub const SHELL_MUTEX_NAME: &str = r"Global\TaixShellSingleInstance";

#[cfg(target_os = "macos")]
pub const SHELL_MUTEX_NAME: &str = "taix-shell";

pub const TASK_NAME: &str = "TaixShell";

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
