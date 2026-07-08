#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

use taix_logging::{init, PanicMode};

mod app_manager;
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
    // 默认：exe 同级目录
    std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
}

fn print_usage(program: &str) {
    eprintln!("Usage:");
    eprintln!("  {} [run] [--data-dir <path>]", program);
}

fn main() {
    let _guard = init("taix-monitor", "info", PanicMode::LogOnly, 30);

    let args: Vec<String> = std::env::args().collect();
    let program = args.get(0).map(|s| s.as_str()).unwrap_or("taix-monitor-windows.exe");

    let data_dir = parse_data_dir(&args);

    let cmd = args.get(1).map(|s| s.as_str()).unwrap_or("run");

    match cmd {
        "run" => runner::run(data_dir),
        _ if cmd.starts_with('-') => runner::run(data_dir),
        _ => {
            eprintln!("Unknown command: {}", cmd);
            print_usage(program);
            std::process::exit(1);
        }
    }
}
