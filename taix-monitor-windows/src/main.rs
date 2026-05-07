#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod app_manager;
mod app_observer;
mod app_timer;
mod config;
mod logging;
mod models;
mod runner;
mod scheduler;
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
    // 默认：exe 同级目录，与客户端 Taix.Client 路径对齐
    std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
}

fn parse_task_name(args: &[String]) -> String {
    for i in 0..args.len() {
        if args[i] == "--task-name" && i + 1 < args.len() {
            return args[i + 1].clone();
        }
    }
    "TaixMonitor".to_string()
}

fn print_usage(program: &str) {
    eprintln!("Usage:");
    eprintln!("  {} [run] [--data-dir <path>]", program);
    eprintln!("  {} install [--data-dir <path>] [--task-name <name>]", program);
    eprintln!("  {} uninstall [--task-name <name>]", program);
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let program = args.get(0).map(|s| s.as_str()).unwrap_or("taix-monitor-windows.exe");

    let data_dir = parse_data_dir(&args);

    let cmd = args.get(1).map(|s| s.as_str()).unwrap_or("run");
    let is_flag = cmd.starts_with('-');

    match cmd {
        "install" if !is_flag => {
            let task_name = parse_task_name(&args);
            let exe_path = std::env::current_exe().expect("Failed to get current executable path");

            // install 时 data-dir 策略与 run 模式完全一致
            let install_data_dir = parse_data_dir(&args);

            match scheduler::install(&exe_path, install_data_dir.as_ref(), &task_name) {
                Ok(()) => {
                    println!("'{}' task installed successfully.", task_name);
                    if let Some(ref dir) = install_data_dir {
                        println!("Data directory: {}", dir.display());
                    }
                    println!("The monitor will start automatically on next logon.");
                }
                Err(e) => {
                    eprintln!("Failed to install task: {}", e);
                    std::process::exit(1);
                }
            }
        }
        "uninstall" if !is_flag => {
            let task_name = parse_task_name(&args);
            match scheduler::uninstall(&task_name) {
                Ok(()) => {
                    println!("'{}' task uninstalled successfully.", task_name);
                }
                Err(e) => {
                    eprintln!("Failed to uninstall task: {}", e);
                    std::process::exit(1);
                }
            }
        }
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
