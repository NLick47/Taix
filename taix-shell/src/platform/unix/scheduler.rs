use std::path::{Path, PathBuf};
use std::process::Command;

const BUNDLE_ID: &str = "com.taix.shell";

fn build_plist_content(exe_path: &Path, data_dir: Option<&PathBuf>) -> String {
    let exe_str = exe_path.to_string_lossy();

    let program_arguments = if let Some(dir) = data_dir {
        format!(
            r#"    <string>{}</string>
        <string>run</string>
        <string>--data-dir</string>
        <string>{}</string>"#,
            exe_str,
            dir.to_string_lossy()
        )
    } else {
        format!(
            r#"    <string>{}</string>
        <string>run</string>"#,
            exe_str
        )
    };

    format!(
        r#"<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>Label</key>
    <string>{bundle_id}</string>
    <key>ProgramArguments</key>
    <array>
{program_arguments}
    </array>
    <key>RunAtLoad</key>
    <true/>
    <key>KeepAlive</key>
    <true/>
    <key>StandardOutPath</key>
    <string>/tmp/taix-shell.log</string>
    <key>StandardErrorPath</key>
    <string>/tmp/taix-shell.log</string>
</dict>
</plist>"#
    )
}

fn get_plist_path() -> PathBuf {
    dirs::home_dir()
        .unwrap_or_else(|| PathBuf::from("/tmp"))
        .join("Library")
        .join("LaunchAgents")
        .join(format!("{}.plist", BUNDLE_ID))
}

pub fn install(exe_path: &Path, data_dir: Option<&PathBuf>, _task_name: &str) -> Result<(), String> {
    let plist_path = get_plist_path();

    // 检查是否已存在
    if plist_path.exists() {
        return Err(format!(
            "LaunchAgent '{}' already exists. Run 'uninstall' first if you want to reconfigure.",
            BUNDLE_ID
        ));
    }

    // 确保 LaunchAgents 目录存在
    if let Some(parent) = plist_path.parent() {
        std::fs::create_dir_all(parent)
            .map_err(|e| format!("Failed to create LaunchAgents directory: {}", e))?;
    }

    // 写入 plist 文件
    let content = build_plist_content(exe_path, data_dir);
    std::fs::write(&plist_path, content)
        .map_err(|e| format!("Failed to write plist file: {}", e))?;

    // 加载 LaunchAgent
    let output = Command::new("launchctl")
        .args(["load", plist_path.to_str().unwrap_or("")])
        .output()
        .map_err(|e| format!("Failed to run launchctl load: {}", e))?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        // 删除已创建的 plist 文件
        let _ = std::fs::remove_file(&plist_path);
        return Err(format!("launchctl load failed: {}", stderr.trim()));
    }

    // 立即启动服务
    let _ = Command::new("launchctl")
        .args(["start", BUNDLE_ID])
        .status();

    println!("LaunchAgent installed at: {}", plist_path.display());
    Ok(())
}

pub fn uninstall(_task_name: &str) -> Result<(), String> {
    let plist_path = get_plist_path();

    if !plist_path.exists() {
        println!("LaunchAgent is not installed.");
        return Ok(());
    }

    // 停止服务
    let _ = Command::new("launchctl")
        .args(["stop", BUNDLE_ID])
        .status();

    // 卸载服务
    let output = Command::new("launchctl")
        .args(["unload", plist_path.to_str().unwrap_or("")])
        .output()
        .map_err(|e| format!("Failed to run launchctl unload: {}", e))?;

    if !output.status.success() {
        let stderr = String::from_utf8_lossy(&output.stderr);
        // 继续尝试删除文件，即使 unload 失败
        eprintln!("Warning: launchctl unload failed: {}", stderr.trim());
    }

    // 删除 plist 文件
    std::fs::remove_file(&plist_path)
        .map_err(|e| format!("Failed to remove plist file: {}", e))?;

    println!("LaunchAgent removed successfully.");
    Ok(())
}
