use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;

use crate::constants::SHELL_MUTEX_NAME;
use crate::platform::{run_tray, try_acquire_single_instance, TrayCmd};
use crate::service_manager::ServiceManager;

pub fn run(data_dir: Option<PathBuf>) -> anyhow::Result<()> {
    let _single_instance = try_acquire_single_instance(SHELL_MUTEX_NAME)
        .ok_or_else(|| anyhow::anyhow!("another instance of taix-shell is already running"))?;

    let (cmd_tx, cmd_rx) = std::sync::mpsc::sync_channel::<TrayCmd>(8);

    let data_dir_for_config = data_dir.clone().or_else(|| {
        std::env::current_exe()
            .ok()
            .and_then(|p| p.parent().map(|p| p.to_path_buf().join("Data")))
    });

    let initial_tray_config = data_dir_for_config
        .as_ref()
        .and_then(|dir| crate::config::load_tray_config(dir))
        .unwrap_or_default();

    #[cfg(target_os = "windows")]
    crate::platform::apply_menu_theme(initial_tray_config.theme);

    let shutdown = Arc::new(AtomicBool::new(false));

    let service_shutdown = Arc::clone(&shutdown);
    let service_data_dir = data_dir.clone();
    let service_handle = std::thread::spawn(move || {
        let manager = ServiceManager::new(service_data_dir);
        manager.run(service_shutdown);
    });

    let tray_shutdown = Arc::clone(&shutdown);
    let tray_handle = std::thread::spawn(move || {
        let _ = run_tray(cmd_tx, initial_tray_config, tray_shutdown);
    });

    // 主线程：处理命令
    while !shutdown.load(Ordering::Relaxed) {
        match cmd_rx.recv_timeout(Duration::from_millis(100)) {
            Ok(TrayCmd::LaunchClient) => {
                // 在独立线程执行 launch_or_wake，避免 pipe 操作阻塞主线程
                // 主线程立即返回继续处理后续托盘消息
                std::thread::spawn(|| {
                    if let Err(e) = crate::client::launch_or_wake() {
                        tracing::error!(target: "taix_shell::runner", "launch client failed: {}", e);
                    }
                });
            }
            Err(std::sync::mpsc::RecvTimeoutError::Timeout) => continue,
            Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => break,
        }
    }

    let _ = tray_handle.join();
    let _ = service_handle.join();
    Ok(())
}
