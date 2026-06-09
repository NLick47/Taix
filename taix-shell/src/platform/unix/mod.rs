pub mod scheduler;

pub use scheduler::{install, uninstall};

use crate::config::{Language, Theme};

/// macOS 不需要单实例锁（LaunchAgent 保证只有一个实例运行）
pub fn try_acquire_single_instance() -> Option<Box<dyn std::any::Any>> {
    // macOS 通过 LaunchAgent 的 KeepAlive 保证单实例
    // 这里简单返回一个标记
    Some(Box::new(true))
}

/// macOS 托盘实现（暂不支持）
pub fn run_tray() -> Result<(), String> {
    Err("Tray is not supported on macOS yet".to_string())
}

pub fn apply_menu_theme(_theme: Theme) {
    // macOS 自动处理菜单主题
}

pub fn is_process_alive(pid: u32) -> bool {
    use std::process::Command;
    Command::new("ps")
        .args(["-p", &pid.to_string()])
        .output()
        .map(|o| o.status.success())
        .unwrap_or(false)
}

pub fn detect_system_language() -> Language {
    use std::process::Command;

    let output = Command::new("sh")
        .args(["-c", "defaults read -g AppleLocale"])
        .output();

    if let Ok(output) = output {
        if output.status.success() {
            let locale = String::from_utf8_lossy(&output.stdout).trim().to_lowercase();
            if locale.starts_with("zh") || locale.contains("zh_cn") || locale.contains("zh-hans") {
                return Language::ZhCn;
            }
        }
    }

    Language::EnUs
}