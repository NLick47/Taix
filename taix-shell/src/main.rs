#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod client;
mod config;
mod constants;
mod i18n;
mod platform;
mod runner;
mod service_manager;

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
    eprintln!("  {} install [--data-dir <path>]", program);
    eprintln!("  {} uninstall", program);
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let program = args.first().map(|s| s.as_str()).unwrap_or("taix-shell.exe");

    let cmd = args.get(1).map(|s| s.as_str()).unwrap_or("run");
    let is_flag = cmd.starts_with('-');

    match cmd {
        "install" if !is_flag => {
            let exe_path = std::env::current_exe().expect("failed to get current executable path");
            let data_dir = parse_data_dir(&args);
            match platform::install(&exe_path, data_dir.as_ref(), constants::TASK_NAME) {
                Ok(()) => println!("'{}' task installed successfully.", constants::TASK_NAME),
                Err(e) => {
                    eprintln!("Failed to install task: {}", e);
                    std::process::exit(1);
                }
            }
        }
        "uninstall" if !is_flag => {
            let mut had_error = false;

            match platform::uninstall(constants::TASK_NAME) {
                Ok(()) => println!("'{}' task uninstalled successfully.", constants::TASK_NAME),
                Err(e) => {
                    eprintln!("Failed to uninstall task: {}", e);
                    had_error = true;
                }
            }

            match platform::remove_autostart() {
                Ok(()) => println!("Autostart registry entry removed successfully."),
                Err(e) => {
                    eprintln!("Failed to remove autostart: {}", e);
                    had_error = true;
                }
            }

            if had_error {
                std::process::exit(1);
            }
        }
        "run" | _ => {
            if !is_flag && cmd != "run" {
                eprintln!("Unknown command: {}", cmd);
                print_usage(program);
                std::process::exit(1);
            }
            let data_dir = parse_data_dir(&args);
            if let Err(e) = runner::run(data_dir) {
                eprintln!("Fatal error: {}", e);
                std::process::exit(1);
            }
        }
    }
}
