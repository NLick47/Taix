#[cfg(target_os = "windows")]
pub mod windows;

#[cfg(target_os = "macos")]
mod macos;

#[cfg(target_os = "macos")]
pub use macos::*;

use std::path::PathBuf;

pub const PROCESSES: &[&str] = &[
    "taix-shell",
    "taix-server",
    "taix-monitor-windows",
    "Taix",
];

pub const TASK_NAME: &str = "TaixShell";

pub trait Platform {
    fn create_shortcut(
        target: &std::path::Path,
        shortcut: &std::path::Path,
        name: &str,
    ) -> anyhow::Result<()>;

    fn remove_shortcut(shortcut: &std::path::Path) -> anyhow::Result<()>;

    fn register_startup(exe_path: &std::path::Path, name: &str) -> anyhow::Result<()>;

    fn unregister_startup(name: &str) -> anyhow::Result<()>;

    fn stop_process(name: &str) -> anyhow::Result<bool>;

    fn start_process(exe_path: &std::path::Path) -> anyhow::Result<()>;

    fn is_process_running(name: &str) -> bool;

    fn default_install_dir() -> PathBuf;

    fn start_menu_dir() -> PathBuf;

    fn desktop_dir() -> PathBuf;
}

pub fn detect_install_dir() -> PathBuf {
    let default = <() as Platform>::default_install_dir();

    if default.join("taix-shell.exe").exists() {
        return default;
    }

    if let Ok(exe_path) = std::env::current_exe() {
        if let Some(dir) = exe_path.parent() {
            if dir.join("taix-shell.exe").exists() {
                return dir.to_path_buf();
            }
        }
    }

    let common_paths = [
        r"C:\Program Files\Taix",
        r"C:\Program Files (x86)\Taix",
        r"C:\Taix",
    ];

    for path in &common_paths {
        let p = PathBuf::from(path);
        if p.join("taix-shell.exe").exists() {
            return p;
        }
    }

    default
}
