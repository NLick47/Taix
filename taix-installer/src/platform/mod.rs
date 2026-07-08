#[cfg(target_os = "windows")]
pub mod windows;

use std::path::PathBuf;

pub const PROCESSES: &[&str] = &[
    "taix-shell",
    "taix-server",
    "taix-monitor-windows",
    "Taix",
];

pub const TASK_NAME: &str = "TaixShell";

/// 安装路径记录文件名
const INSTALL_LOCATION_FILE: &str = "install-location.txt";

/// 获取安装路径记录文件路径
pub fn install_location_file() -> PathBuf {
    dirs::data_local_dir()
        .unwrap_or_else(|| PathBuf::from(r"C:\Users\Default\AppData\Local"))
        .join("Taix")
        .join(INSTALL_LOCATION_FILE)
}

/// 保存安装路径
pub fn save_install_location(install_dir: &std::path::Path) -> anyhow::Result<()> {
    let location_file = install_location_file();

    if let Some(parent) = location_file.parent() {
        std::fs::create_dir_all(parent)?;
    }

    std::fs::write(&location_file, install_dir.to_string_lossy().as_bytes())?;
    Ok(())
}

/// 读取安装路径
pub fn read_install_location() -> Option<PathBuf> {
    let location_file = install_location_file();

    if !location_file.exists() {
        return None;
    }

    let content = std::fs::read_to_string(&location_file).ok()?;
    let path = PathBuf::from(content.trim());

    // 验证路径有效性
    if path.join("taix-shell.exe").exists() {
        Some(path)
    } else {
        None
    }
}

/// 删除安装路径记录
pub fn remove_install_location() -> anyhow::Result<()> {
    let location_file = install_location_file();

    if location_file.exists() {
        std::fs::remove_file(&location_file)?;
    }

    // 如果父目录为空，也删除
    if let Some(parent) = location_file.parent() {
        if parent.exists() && parent.read_dir().map_or(false, |mut d| d.next().is_none()) {
            std::fs::remove_dir(parent)?;
        }
    }

    Ok(())
}

pub trait Platform {
    fn create_shortcut(
        target: &std::path::Path,
        shortcut: &std::path::Path,
        name: &str,
    ) -> anyhow::Result<()>;

    fn remove_shortcut(shortcut: &std::path::Path) -> anyhow::Result<()>;

    fn register_startup(exe_path: &std::path::Path, name: &str) -> anyhow::Result<()>;

    fn unregister_startup(name: &str) -> anyhow::Result<()>;

    /// 停止计划任务/启动项的看门狗，避免在重新注册期间它再次拉起进程。
    /// 不删除任务定义（删除由 unregister_startup 负责）。
    fn stop_scheduled_task(name: &str);

    fn stop_process(name: &str) -> anyhow::Result<bool>;

    fn start_process(exe_path: &std::path::Path) -> anyhow::Result<()>;

    fn is_process_running(name: &str) -> bool;

    fn default_install_dir() -> PathBuf;

    fn start_menu_dir() -> PathBuf;

    fn desktop_dir() -> PathBuf;
}

pub fn restore_backup(backup_dir: &std::path::Path, install_dir: &std::path::Path) -> anyhow::Result<()> {
    for entry in std::fs::read_dir(backup_dir)? {
        let entry = entry?;
        let src = entry.path();
        let dst = install_dir.join(entry.file_name());
        std::fs::copy(&src, &dst)?;
    }
    Ok(())
}

/// 检测安装目录（优先使用记录文件，其次使用其他方式）
pub fn detect_install_dir() -> PathBuf {
    // 优先从安装路径记录文件读取
    if let Some(path) = read_install_location() {
        return path;
    }

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
