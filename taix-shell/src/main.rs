#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod client;
mod config;
mod constants;
mod i18n;
mod platform;
mod runner;
mod service_manager;
mod shutdown;

use std::path::PathBuf;

#[cfg(target_os = "windows")]
fn set_dpi_awareness() {
    use windows::Win32::UI::HiDpi::{
        SetProcessDpiAwarenessContext, DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2,
    };
    unsafe {
        let _ = SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
    }
}

fn parse_option<'a>(args: &'a [String], name: &str) -> Option<&'a str> {
    args.iter()
        .position(|arg| arg == name)
        .and_then(|i| args.get(i + 1))
        .map(|value| value.as_str())
}

fn parse_data_dir(args: &[String]) -> Option<PathBuf> {
    parse_option(args, "--data-dir")
        .map(PathBuf::from)
        .or_else(|| std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from))
}

fn print_usage(program: &str) {
    eprintln!("Usage:");
    eprintln!("  {} [run] [--data-dir <path>]", program);
    eprintln!("  {} install [--data-dir <path>]", program);
    eprintln!("  {} uninstall", program);
    eprintln!();
    eprintln!("Debug:");
    eprintln!(
        "  {} --test-tray [--data-dir <path>] [--lang auto|zh|en] [--theme system|light|dark]",
        program
    );
    eprintln!("      Only the tray is started (no server/monitor). Menu actions are printed");
    eprintln!("      as they fire, and --lang/--theme override the on-disk config.");
}

/// 只跑托盘，用于单独验证菜单文案与外观。
///
/// 会读取真实配置（含 `--data-dir`），`--lang` / `--theme` 可覆盖；托盘动作打印到
/// stderr 而不是真的去唤起客户端或退出。失败时把错误打出来，不再静默返回。
fn run_test_tray(args: &[String]) -> i32 {
    let mut config = parse_data_dir(args)
        .as_deref()
        .and_then(config::load_tray_config)
        .unwrap_or_default();

    if let Some(language) = parse_option(args, "--lang").and_then(config::Language::from_name) {
        config.language = language;
    }
    if let Some(theme) = parse_option(args, "--theme").and_then(config::Theme::from_name) {
        config.theme = theme;
    }

    // 这个开关的意义就是"把托盘显示出来看"，配置里关掉了托盘也要强制打开，
    // 否则图标是隐藏的、进程又不会自己结束，看起来像卡死。
    config.is_visible = true;

    // 打印解析后的文案：`--lang auto` 时这是唯一能看出实际落到哪个语言的地方
    let texts = i18n::menu_texts(config.language);
    eprintln!(
        "[test-tray] language={:?} theme={:?} menu=[{:?}, {:?}]",
        config.language, config.theme, texts.show, texts.quit
    );

    let (cmd_tx, cmd_rx) = std::sync::mpsc::sync_channel::<platform::TrayCmd>(8);

    std::thread::spawn(move || {
        while let Ok(cmd) = cmd_rx.recv() {
            eprintln!("[test-tray] tray action: {:?}", cmd);
        }
    });

    match platform::run_tray(cmd_tx, config, shutdown::Shutdown::new()) {
        Ok(()) => 0,
        Err(e) => {
            eprintln!("[test-tray] failed: {:#}", e);
            1
        }
    }
}

#[cfg(debug_assertions)]
fn init_logging() -> Option<taix_logging::LoggingGuard> {
    let dir = std::env::current_exe()
        .ok()
        .and_then(|exe| exe.parent().map(|dir| dir.join("Logs")))?;

    if std::fs::create_dir_all(&dir).is_err() {
        eprintln!(
            "[taix-shell] logging disabled: cannot create {:?}; continuing without logs",
            dir
        );
        return None;
    }

    let probe = dir.join(".write-probe");
    match std::fs::File::create(&probe) {
        Ok(_) => {
            let _ = std::fs::remove_file(&probe);
        }
        Err(e) => {
            eprintln!(
                "[taix-shell] logging disabled: {:?} is not writable ({}); continuing without logs",
                dir, e
            );
            return None;
        }
    }

    eprintln!(
        "[taix-shell] debug build: writing logs to {} (file name uses the UTC date)",
        dir.display()
    );

    Some(taix_logging::init(
        "taix-shell",
        "taix_shell=info",
        taix_logging::PanicMode::SyncFile,
        7,
    ))
}

fn main() {
    #[cfg(target_os = "windows")]
    set_dpi_awareness();

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
            match platform::uninstall(constants::TASK_NAME) {
                Ok(()) => println!("'{}' task uninstalled successfully.", constants::TASK_NAME),
                Err(e) => {
                    eprintln!("Failed to uninstall task: {}", e);
                    std::process::exit(1);
                }
            }
        }
        "--test-tray" => {
            // test-tray 是验证托盘/i18n/主题的主路径，日志从这里开始
            #[cfg(debug_assertions)]
            let _logging_guard = init_logging();
            tracing::info!(
                target: "taix_shell::lifecycle",
                "taix-shell {} starting command=--test-tray args={:?}",
                env!("CARGO_PKG_VERSION"),
                &args[1..]
            );

            let code = run_test_tray(&args);
            tracing::info!(target: "taix_shell::lifecycle", "test-tray exited code={}", code);
            std::process::exit(code);
        }
        "--help" | "-h" => print_usage(program),
        "run" | _ => {
            if !is_flag && cmd != "run" {
                eprintln!("Unknown command: {}", cmd);
                print_usage(program);
                std::process::exit(1);
            }

            #[cfg(debug_assertions)]
            let _logging_guard = init_logging();
            tracing::info!(
                target: "taix_shell::lifecycle",
                "taix-shell {} starting command={} args={:?}",
                env!("CARGO_PKG_VERSION"),
                cmd,
                &args[1..]
            );

            let data_dir = parse_data_dir(&args);
            if let Err(e) = runner::run(data_dir) {
                tracing::error!(target: "taix_shell::lifecycle", "shell run returned error={:#}", e);
                eprintln!("Fatal error: {}", e);
                std::process::exit(1);
            }

            tracing::info!(target: "taix_shell::lifecycle", "taix-shell main completed; process exiting normally");
        }
    }
}
