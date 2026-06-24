mod install;
mod platform;
mod sfx;
mod ui;
mod update;

use anyhow::Result;
use platform::read_install_location;

fn main() {
    let result = run();

    if let Err(e) = result {
        println!("\n{}", format_error(&e));
        ui::cli::wait_exit();
        std::process::exit(1);
    }
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
        if existing_install.is_some() {
            println!("  检测到已安装的 Taix");
            println!("  请使用安装程序进行更新或卸载");
        } else {
            println!("  未检测到 Taix 安装");
            println!("  此安装程序无效，请重新下载");
        }
        ui::cli::wait_exit();
        return Ok(());
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
