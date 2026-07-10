#![cfg_attr(feature = "gui", windows_subsystem = "windows")]

mod gui;
mod i18n;
mod install;
mod platform;
mod sfx;
mod ui;
mod update;

use anyhow::Result;
use platform::read_install_location;

fn main() {
    let args: Vec<String> = std::env::args().collect();

    // 命令行参数解析
    // 无参数            → 图形界面 (wry)
    // /cli 或 --cli     → CLI 交互模式
    let cli_mode = args.iter().any(|a| a == "/cli" || a == "--cli");

    if cli_mode || !cfg!(target_os = "windows") {
        let result = run();
        if let Err(e) = result {
            println!("\n{}", format_error(&e));
            ui::cli::wait_exit();
            std::process::exit(1);
        }
        return;
    }

    // 无参数 → WebView 图形界面
    gui::run_gui();
}

fn format_error(e: &anyhow::Error) -> String {
    let msg = e.to_string();

    // 友好化常见错误
    if msg.contains("disk") || msg.contains("space") || msg.contains("磁盘") {
        return "安装失败：磁盘空间不足".to_string();
    }
    if msg.contains("permission") || msg.contains("denied") || msg.contains("权限") {
        return "安装失败：权限不足，请以管理员身份运行".to_string();
    }
    if msg.contains("network") || msg.contains("网络") {
        return "安装失败：网络连接错误".to_string();
    }

    format!("安装失败：{}", msg)
}

fn run() -> Result<()> {
    show_header();

    if !sfx::has_payload() {
        let existing_install = read_install_location();
        if let Some(existing_dir) = existing_install {
            // 无 payload 但有已安装 → 进入卸载菜单
            println!("  检测到已安装的 Taix（安装程序无有效负载）\n");
            let choice = ui::cli::select_action();
            match choice {
                Ok(2) => return install::run_uninstall(Some(existing_dir), false),
                _ => {
                    println!("  已取消");
                    ui::cli::wait_exit();
                    return Ok(());
                }
            }
        } else {
            println!("  未检测到 Taix 安装");
            println!("  此安装程序无效，请重新下载");
            ui::cli::wait_exit();
            return Ok(());
        }
    }

    let existing_install = read_install_location();
    if let Some(existing_dir) = existing_install {
        println!("  检测到已安装的 Taix\n");

        let choice = ui::cli::select_action()?;
        match choice {
            0 => return update::run_update(Some(existing_dir), false),
            1 => {} // 重新安装，继续
            2 => return install::run_uninstall(Some(existing_dir), false),
            3 => {
                println!("  已取消");
                ui::cli::wait_exit();
                return Ok(());
            }
            _ => return Ok(()),
        }
    }

    install::run_install(None, false, false, false)
}

fn show_header() {
    println!();
    println!("  ╔════════════════════════════════════╗");
    println!("  ║        Taix 安装程序               ║");
    println!("  ╚════════════════════════════════════╝");
    println!();
}