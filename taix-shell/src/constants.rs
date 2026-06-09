#[cfg(target_os = "windows")]
pub const CLIENT_PIPE_NAME: &str = r"\\.\pipe\TaixClient";

#[cfg(not(target_os = "windows"))]
pub const CLIENT_PIPE_NAME: &str = "/tmp/taix-client.sock";

pub const CLIENT_EXE_NAME: &str = "Taix.exe";

#[cfg(target_os = "windows")]
pub const MONITOR_EXE_NAME: &str = "taix-monitor-windows.exe";
#[cfg(not(target_os = "windows"))]
pub const MONITOR_EXE_NAME: &str = "taix-monitor";

#[cfg(target_os = "windows")]
pub const SERVER_EXE_NAME: &str = "taix-server.exe";
#[cfg(not(target_os = "windows"))]
pub const SERVER_EXE_NAME: &str = "taix-server";

pub const SHELL_MUTEX_NAME: &str = r"Global\TaixShellSingleInstance";
pub const TASK_NAME: &str = "TaixShell";
