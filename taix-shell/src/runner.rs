use std::path::PathBuf;
use std::sync::mpsc::{self, RecvTimeoutError};
use std::thread;
use std::time::{Duration, Instant};

use crate::constants::{default_data_dir, SHELL_MUTEX_NAME};
use crate::platform::{run_tray, try_acquire_single_instance, TrayCmd};
use crate::service_manager::ServiceManager;
use crate::shutdown::Shutdown;

pub fn run(_data_dir: Option<PathBuf>) -> anyhow::Result<()> {
    tracing::info!(target: "taix_shell::lifecycle", "acquiring single-instance lock name={}", SHELL_MUTEX_NAME);
    let _single_instance = try_acquire_single_instance(SHELL_MUTEX_NAME).ok_or_else(|| {
        tracing::error!(
            target: "taix_shell::lifecycle",
            "single-instance lock is held by another process; refusing to start"
        );
        anyhow::anyhow!("another instance of taix-shell is already running")
    })?;
    tracing::info!(target: "taix_shell::lifecycle", "single-instance lock acquired");

    crate::client::warm_client_exe_path();

    let data_dir = default_data_dir();
    tracing::info!(target: "taix_shell::lifecycle", "data_dir={:?}", data_dir);

    let tray_config = match crate::config::load_tray_config(&data_dir) {
        Some(config) => {
            tracing::info!(
                target: "taix_shell::lifecycle",
                "tray config loaded theme={:?} language={:?} is_visible={}",
                config.theme,
                config.language,
                config.is_visible
            );
            config
        }
        None => {
            tracing::warn!(
                target: "taix_shell::lifecycle",
                "AppConfig.json missing or unreadable under {:?}; falling back to defaults",
                data_dir
            );
            crate::config::TrayConfig::default()
        }
    };

    let shutdown = Shutdown::new();

    let (cmd_tx, cmd_rx) = mpsc::sync_channel::<TrayCmd>(8);

    let service_shutdown = shutdown.clone();
    let service_data_dir = data_dir.clone();
    let service_handle = thread::spawn(move || {
        ServiceManager::new(Some(service_data_dir)).run(service_shutdown);
    });
    tracing::info!(target: "taix_shell::lifecycle", "service supervisor thread spawned");

    let action_shutdown = shutdown.clone();
    let action_handle = thread::spawn(move || run_action_loop(cmd_rx, action_shutdown));
    tracing::info!(target: "taix_shell::lifecycle", "tray action thread spawned");

    // 阻塞到托盘事件循环结束
    tracing::info!(target: "taix_shell::lifecycle", "entering tray event loop (main thread)");
    let tray_started = Instant::now();
    let result = run_tray(cmd_tx, tray_config, shutdown.clone());
    let tray_elapsed = tray_started.elapsed();

    match &result {
        Ok(()) => tracing::info!(
            target: "taix_shell::lifecycle",
            "tray event loop returned Ok after {:?}",
            tray_elapsed
        ),
        Err(e) => tracing::error!(
            target: "taix_shell::lifecycle",
            "tray event loop returned Err after {:?} error={:#}",
            tray_elapsed,
            e
        ),
    }

    tracing::info!(target: "taix_shell::lifecycle", "requesting shutdown and joining worker threads");
    shutdown.request();

    let action_started = Instant::now();
    let _ = action_handle.join();
    tracing::info!(
        target: "taix_shell::lifecycle",
        "joined tray action thread after {:?}",
        action_started.elapsed()
    );

    let service_started = Instant::now();
    let _ = service_handle.join();
    tracing::info!(
        target: "taix_shell::lifecycle",
        "joined service supervisor thread after {:?}",
        service_started.elapsed()
    );

    tracing::info!(target: "taix_shell::lifecycle", "all threads joined; run() returning");
    result
}

/// 消费托盘菜单动作。
///
fn run_action_loop(rx: mpsc::Receiver<TrayCmd>, shutdown: Shutdown) {
    tracing::debug!(target: "taix_shell::runner", "tray action loop started");
    while !shutdown.is_requested() {
        match rx.recv_timeout(Duration::from_millis(100)) {
            Ok(TrayCmd::LaunchClient) => {
                tracing::info!(target: "taix_shell::runner", "tray action received: LaunchClient");
                let started = Instant::now();
                match crate::client::launch_or_wake() {
                    Ok(()) => tracing::info!(
                        target: "taix_shell::runner",
                        "launch_or_wake ok after {:?}",
                        started.elapsed()
                    ),
                    Err(e) => tracing::error!(
                        target: "taix_shell::runner",
                        "launch_or_wake failed after {:?} error={:#}",
                        started.elapsed(),
                        e
                    ),
                }
            }
            Err(RecvTimeoutError::Timeout) => continue,
            Err(RecvTimeoutError::Disconnected) => {
                tracing::info!(
                    target: "taix_shell::runner",
                    "tray command channel disconnected; action loop exiting"
                );
                return;
            }
        }
    }
    tracing::info!(target: "taix_shell::runner", "shutdown observed; action loop exiting");
}
