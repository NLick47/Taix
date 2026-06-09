use crate::InstallAction;
use anyhow::Result;
use dialoguer::{Confirm, Select};
use std::path::PathBuf;

pub fn prompt_install_dir(default: PathBuf) -> Result<PathBuf> {
    println!("\n安装目录: {}", default.display());

    let choices = ["使用默认目录", "选择其他目录"];

    let selection = Select::new()
        .with_prompt("请选择")
        .default(0)
        .items(&choices)
        .interact()
        .map_err(|e| anyhow::anyhow!("Select error: {}", e))?;

    if selection == 0 {
        return Ok(default);
    }

    // 打开文件夹选择对话框
    println!("\n正在打开文件夹选择对话框...");
    if let Some(path) = browse_folder() {
        println!("已选择: {}", path.display());
        return Ok(path);
    }

    // 对话框取消
    println!("\n已取消选择，使用默认目录: {}", default.display());
    Ok(default)
}

/// 调用系统对话框选择文件夹 (Windows)
fn browse_folder() -> Option<PathBuf> {
    let script = r#"
Add-Type -AssemblyName System.Windows.Forms
$dialog = New-Object System.Windows.Forms.FolderBrowserDialog
$dialog.Description = "选择 Taix 安装目录"
$dialog.ShowNewFolderButton = $true
if ($dialog.ShowDialog() -eq 'OK') { $dialog.SelectedPath } else { '' }
"#;

    let output = std::process::Command::new("powershell")
        .args(["-NoProfile", "-NonInteractive", "-Command", script])
        .output()
        .ok()?;

    if output.status.success() {
        let path = String::from_utf8_lossy(&output.stdout).trim().to_string();
        if !path.is_empty() {
            return Some(PathBuf::from(path));
        }
    }

    None
}

pub fn confirm_install() -> Result<bool> {
    Confirm::new()
        .with_prompt("开始安装?")
        .default(true)
        .interact()
        .map_err(|e| anyhow::anyhow!("Confirm error: {}", e))
}

pub fn confirm_update() -> Result<bool> {
    Confirm::new()
        .with_prompt("开始更新?")
        .default(true)
        .interact()
        .map_err(|e| anyhow::anyhow!("Confirm error: {}", e))
}

pub fn confirm_uninstall() -> Result<bool> {
    Confirm::new()
        .with_prompt("确认卸载 Taix?")
        .default(false)
        .interact()
        .map_err(|e| anyhow::anyhow!("Confirm error: {}", e))
}

pub fn show_step(step: usize, total: usize, message: &str) {
    println!("[{}/{}] {}", step, total, message);
}

pub fn show_success(message: &str) {
    println!("\n========================================");
    println!("  {}", message);
    println!("========================================\n");
}

#[allow(dead_code)]
pub fn show_error(message: &str) {
    println!("\n[错误] {}", message);
}

pub fn show_warning(message: &str) {
    println!("[警告] {}", message);
}

pub fn show_install_complete(install_dir: &std::path::Path) {
    println!("\n========================================");
    println!("  Taix 安装完成!");
    println!("========================================");
    println!("\n安装目录: {}", install_dir.display());
    println!("\n按 Enter 键退出...");

    let _ = std::io::stdin().read_line(&mut String::new());
}

pub fn show_update_complete(install_dir: &std::path::Path) {
    println!("\n========================================");
    println!("  Taix 更新完成!");
    println!("========================================");
    println!("\n安装目录: {}", install_dir.display());
    println!("\n按 Enter 键退出...");

    let _ = std::io::stdin().read_line(&mut String::new());
}

pub fn show_uninstall_complete(install_dir: &std::path::Path) {
    println!("\n========================================");
    println!("  Taix 卸载完成!");
    println!("========================================");
    println!("\n注意: 用户数据 (配置、数据库、日志等) 已保留。");
    println!("如需完全删除，请手动删除: {}", install_dir.display());
    println!("\n按 Enter 键退出...");

    let _ = std::io::stdin().read_line(&mut String::new());
}

pub fn prompt_install_action() -> Result<InstallAction> {
    let choices = ["更新", "重新安装", "卸载", "取消"];

    let selection = Select::new()
        .with_prompt("请选择操作")
        .default(0)
        .items(&choices)
        .interact()
        .map_err(|e| anyhow::anyhow!("Select error: {}", e))?;

    Ok(match selection {
        0 => InstallAction::Update,
        1 => InstallAction::Reinstall,
        2 => InstallAction::Uninstall,
        3 => InstallAction::Cancel,
        _ => InstallAction::Cancel,
    })
}
