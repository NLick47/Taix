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

struct MonitorArgs {
    data_dir: Option<PathBuf>,
    inactive_threshold_secs: Option<u64>,
    max_sound_duration_secs: Option<u64>,
    sleep_watch: Option<bool>,
}

fn parse_args(args: &[String]) -> MonitorArgs {
    let mut result = MonitorArgs {
        data_dir: None,
        inactive_threshold_secs: None,
        max_sound_duration_secs: None,
        sleep_watch: None,
    };

    for i in 0..args.len() {
        if args[i] == "--data-dir" && i + 1 < args.len() {
            result.data_dir = Some(PathBuf::from(&args[i + 1]));
        } else if args[i] == "--inactive-threshold" && i + 1 < args.len() {
            if let Ok(v) = args[i + 1].parse::<u64>() {
                if v >= 1 && v <= 60 {
                    result.inactive_threshold_secs = Some(v * 60);
                }
            }
        } else if args[i] == "--max-sound-duration" && i + 1 < args.len() {
            if let Ok(v) = args[i + 1].parse::<u64>() {
                if v >= 15 && v <= 480 && v % 15 == 0 {
                    result.max_sound_duration_secs = Some(v * 60);
                }
            }
        } else if args[i] == "--sleep-watch" && i + 1 < args.len() {
            result.sleep_watch = Some(args[i + 1] == "true");
        }
    }

    // fallback: environment variable
    if result.data_dir.is_none() {
        if let Ok(dir) = std::env::var("TAIX_DATA_DIR") {
            result.data_dir = Some(PathBuf::from(dir));
        }
    }

    result
}

fn print_usage(program: &str) {
    eprintln!("Usage:");
    eprintln!("  {} [run] [--data-dir <path>] [--inactive-threshold <mins>] [--max-sound-duration <mins>] [--sleep-watch <true|false>]", program);
}

fn main() {
    let log_filter = if cfg!(debug_assertions) {
        "info"
    } else {
        "taix_monitor=info"
    };
    let _guard = init("taix-monitor", log_filter, PanicMode::SyncFile, 30);

    let args: Vec<String> = std::env::args().collect();
    let program = args.get(0).map(|s| s.as_str()).unwrap_or("taix-monitor-windows.exe");

    let parsed = parse_args(&args);
    let data_dir = parsed.data_dir;

    let cmd = args.get(1).map(|s| s.as_str()).unwrap_or("run");

    match cmd {
        "run" => runner::run(data_dir, parsed.inactive_threshold_secs, parsed.max_sound_duration_secs, parsed.sleep_watch),
        _ if cmd.starts_with('-') => runner::run(data_dir, parsed.inactive_threshold_secs, parsed.max_sound_duration_secs, parsed.sleep_watch),
        _ => {
            eprintln!("Unknown command: {}", cmd);
            print_usage(program);
            std::process::exit(1);
        }
    }
}
