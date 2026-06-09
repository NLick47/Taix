mod install;
mod platform;
mod sfx;
mod ui;
mod update;

use anyhow::Result;
use clap::Parser;
use platform::read_install_location;
use std::path::PathBuf;

#[derive(Parser, Debug)]
#[command(name = "taix-installer")]
#[command(about = "Taix 安装器/更新器", long_about = None)]
struct Args {
    #[arg(short, long, value_name = "PATH")]
    install_dir: Option<PathBuf>,

    #[arg(short, long)]
    update: bool,

    #[arg(short, long)]
    uninstall: bool,

    #[arg(short, long)]
    silent: bool,

    #[arg(long)]
    desktop_shortcut: bool,

    #[arg(long)]
    defender_exclusion: bool,

    #[arg(long)]
    extract_only: Option<PathBuf>,
}

fn main() {
    let _guard = taix_logging::init("taix-installer", "info", taix_logging::PanicMode::LogOnly);

    let args = Args::parse();

    let result = run(args);

    if let Err(e) = result {
        eprintln!("\n[错误] {}", e);

        for cause in e.chain().skip(1) {
            eprintln!("  原因: {}", cause);
        }

        tracing::error!("Installer failed: {}", e);

        println!("\n按 Enter 键退出...");
        let _ = std::io::stdin().read_line(&mut String::new());
        std::process::exit(1);
    }
}

fn run(args: Args) -> Result<()> {
    if let Some(dest) = args.extract_only {
        return sfx::extract_to(&dest);
    }

    if args.update {
        return update::run_update(args.install_dir, args.silent);
    }

    if args.uninstall {
        return install::run_uninstall(args.install_dir, args.silent);
    }

    if !sfx::has_payload() {
        let existing_install = read_install_location();
        if existing_install.is_some() {
            println!("检测到已安装的 Taix。");
            println!("请使用 --update 进行更新，或 --uninstall 进行卸载。");
        } else {
            println!("未检测到 Taix 安装。");
            println!("此安装器无内嵌文件，无法进行安装。");
        }
        println!("\n按 Enter 键退出...");
        let _ = std::io::stdin().read_line(&mut String::new());
        return Ok(());
    }

    let existing_install = read_install_location();
    if let Some(existing_dir) = existing_install {
        println!("检测到已安装的 Taix: {}", existing_dir.display());

        let choice = ui::cli::prompt_install_action()?;
        match choice {
            InstallAction::Update => {
                return update::run_update(Some(existing_dir), args.silent);
            }
            InstallAction::Reinstall => {}
            InstallAction::Uninstall => {
                return install::run_uninstall(Some(existing_dir), args.silent);
            }
            InstallAction::Cancel => {
                println!("操作已取消。");
                println!("\n按 Enter 键退出...");
                let _ = std::io::stdin().read_line(&mut String::new());
                return Ok(());
            }
        }
    }

    install::run_install(
        args.install_dir,
        args.silent,
        args.desktop_shortcut,
        args.defender_exclusion,
    )
}

#[derive(Debug, Clone, Copy, PartialEq)]
pub enum InstallAction {
    Update,
    Reinstall,
    Uninstall,
    Cancel,
}
