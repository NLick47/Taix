use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;

use crate::constants::{default_data_dir, SHELL_MUTEX_NAME};
use crate::platform::{run_tray, try_acquire_single_instance, TrayCmd};
use crate::service_manager::ServiceManager;

#[cfg(target_os = "windows")]
pub fn run(_data_dir: Option<PathBuf>) -> anyhow::Result<()> {
    let _single_instance = try_acquire_single_instance(SHELL_MUTEX_NAME)
        .ok_or_else(|| anyhow::anyhow!("another instance of taix-shell is already running"))?;

    let (cmd_tx, cmd_rx) = std::sync::mpsc::sync_channel::<TrayCmd>(8);

    let data_dir = default_data_dir();

    let initial_tray_config = crate::config::load_tray_config(&data_dir).unwrap_or_default();

    crate::platform::apply_menu_theme(initial_tray_config.theme);

    let shutdown = Arc::new(AtomicBool::new(false));

    let service_shutdown = Arc::clone(&shutdown);
    let service_handle = std::thread::spawn(move || {
        let manager = ServiceManager::new(Some(data_dir));
        manager.run(service_shutdown);
    });

    let tray_shutdown = Arc::clone(&shutdown);
    if initial_tray_config.is_visible {
        let tray_tx = cmd_tx.clone();
        std::thread::spawn(move || {
            let _ = run_tray(tray_tx, initial_tray_config, tray_shutdown);
        });
    }

    while !shutdown.load(Ordering::Relaxed) {
        match cmd_rx.recv_timeout(Duration::from_millis(100)) {
            Ok(TrayCmd::LaunchClient) => {
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

    let _ = service_handle.join();
    Ok(())
}

#[cfg(target_os = "macos")]
pub fn run(_data_dir: Option<PathBuf>) -> anyhow::Result<()> {
    let _single_instance = try_acquire_single_instance(SHELL_MUTEX_NAME)
        .ok_or_else(|| anyhow::anyhow!("another instance of taix-shell is already running"))?;

    let (cmd_tx, cmd_rx) = std::sync::mpsc::sync_channel::<TrayCmd>(8);

    let data_dir = default_data_dir();

    let initial_tray_config = crate::config::load_tray_config(&data_dir).unwrap_or_default();

    let shutdown = Arc::new(AtomicBool::new(false));

    let service_shutdown = Arc::clone(&shutdown);
    let service_handle = std::thread::spawn(move || {
        let manager = ServiceManager::new(Some(data_dir));
        manager.run(service_shutdown);
    });

    let cmd_shutdown = Arc::clone(&shutdown);
    let cmd_handle = std::thread::spawn(move || {
        while !cmd_shutdown.load(Ordering::Relaxed) {
            match cmd_rx.recv_timeout(Duration::from_millis(100)) {
                Ok(TrayCmd::LaunchClient) => {
                    if let Err(e) = crate::client::launch_or_wake() {
                        tracing::error!(target: "taix_shell::runner", "launch client failed: {}", e);
                    }
                }
                Err(std::sync::mpsc::RecvTimeoutError::Timeout) => continue,
                Err(std::sync::mpsc::RecvTimeoutError::Disconnected) => break,
            }
        }
    });

    if initial_tray_config.is_visible {
        let _ = run_tray(cmd_tx, initial_tray_config, Arc::clone(&shutdown));
    } else {
        shutdown.store(true, Ordering::Relaxed);
    }

    shutdown.store(true, Ordering::Relaxed);
    let _ = service_handle.join();
    let _ = cmd_handle.join();
    Ok(())
}
