mod install;
mod platform;
mod sfx;
mod ui;
mod update;

use anyhow::Result;
use clap::Parser;
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

fn main() -> Result<()> {
    let _guard = taix_logging::init("taix-installer", "info", taix_logging::PanicMode::LogOnly);

    let args = Args::parse();

    if let Some(dest) = args.extract_only {
        return sfx::extract_to(&dest);
    }

    if args.update {
        return update::run_update(args.install_dir, args.silent);
    }

    if args.uninstall {
        return install::run_uninstall(args.install_dir, args.silent);
    }

    install::run_install(
        args.install_dir,
        args.silent,
        args.desktop_shortcut,
        args.defender_exclusion,
    )
}
