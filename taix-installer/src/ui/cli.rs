use anyhow::Result;
use dialoguer::{Confirm, Input};
use std::path::PathBuf;

pub fn prompt_install_dir(default: PathBuf) -> Result<PathBuf> {
    let default_str = default.to_str().unwrap_or("").to_string();

    let input: String = Input::new()
        .with_prompt("安装目录")
        .default(default_str)
        .interact_text()
        .map_err(|e| anyhow::anyhow!("Input error: {}", e))?;

    let path = PathBuf::from(input);
    println!("将安装到: {}", path.display());

    Ok(path)
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

pub fn show_complete_and_wait(message: &str) {
    println!("\n{}", message);
    println!("按 Enter 键退出...");

    let _ = std::io::stdin().read_line(&mut String::new());
}
