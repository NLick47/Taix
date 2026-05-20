#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod app_manager;
mod app_observer;
mod app_timer;
mod models;
mod runner;
mod sleep_detector;
mod transport;
mod win32;

use std::path::PathBuf;

fn parse_data_dir(args: &[String]) -> Option<PathBuf> {
    for i in 0..args.len() {
        if args[i] == "--data-dir" && i + 1 < args.len() {
            return Some(PathBuf::from(&args[i + 1]));
        }
    }
    if let Ok(dir) = std::env::var("TAIX_DATA_DIR") {
        return Some(PathBuf::from(dir));
    }
    dirs::data_dir().map(|d| d.join("Taix"))
}

fn print_usage(program: &str) {
    eprintln!("Usage:");
    eprintln!("  {} [run] [--data-dir <path>]", program);
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let program = args.get(0).map(|s| s.as_str()).unwrap_or("taix-monitor-windows.exe");

    let data_dir = parse_data_dir(&args);

    let cmd = args.get(1).map(|s| s.as_str()).unwrap_or("run");
    let is_flag = cmd.starts_with('-');

    match cmd {
        "run" | _ => {
            if !is_flag && cmd != "run" {
                eprintln!("Unknown command: {}", cmd);
                print_usage(program);
                std::process::exit(1);
            }

            // 控制台模式：创建 Tokio runtime 并运行业务逻辑
            let rt = tokio::runtime::Runtime::new().expect("Failed to create Tokio runtime");
            rt.block_on(async {
                let (tx, rx) = tokio::sync::watch::channel(());
                tokio::spawn(async move {
                    let _ = tokio::signal::ctrl_c().await;
                    let _ = tx.send(());
                });
                runner::run(data_dir, rx).await;
            });
        }
    }
}
