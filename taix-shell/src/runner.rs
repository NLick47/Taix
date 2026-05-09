use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;

use crate::constants::SHELL_MUTEX_NAME;
use crate::platform::{run_tray, try_acquire_single_instance, TrayCmd};
use crate::service_manager::ServiceManager;

pub fn run(data_dir: Option<PathBuf>) -> anyhow::Result<()> {
    let _logging_guard =
        taix_logging::init("taix-shell", "taix_shell=info", taix_logging::PanicMode::LogOnly);

    let _single_instance = try_acquire_single_instance(SHELL_MUTEX_NAME)
        .ok_or_else(|| anyhow::anyhow!("another instance of taix-shell is already running"))?;

    let rt = tokio::runtime::Builder::new_current_thread()
        .enable_all()
        .build()?;

    let service_manager = ServiceManager::new(data_dir.clone());
    let service_handle = rt.spawn({
        let sm = service_manager.clone();
        async move { sm.run().await }
    });

    let (cmd_tx, mut cmd_rx) = tokio::sync::mpsc::channel::<TrayCmd>(8);

    let data_dir_for_watch = data_dir.or_else(|| {
        std::env::current_exe()
            .ok()
            .and_then(|p| p.parent().map(|p| p.to_path_buf().join("Data")))
    });

    let initial_tray_config = data_dir_for_watch
        .as_ref()
        .and_then(|dir| crate::config::load_tray_config(dir))
        .unwrap_or_default();

    #[cfg(target_os = "windows")]
    crate::platform::apply_menu_theme(initial_tray_config.theme);

    let tray_shutdown = Arc::new(AtomicBool::new(false));
    let tray_handle = std::thread::spawn({
        let shutdown = Arc::clone(&tray_shutdown);
        move || {
            if let Err(e) = run_tray(cmd_tx, initial_tray_config, shutdown) {
                tracing::error!(target: "taix_shell::runner", "tray error: {}", e);
            }
        }
    });

    rt.block_on(async {
        loop {
            tokio::select! {
                Some(cmd) = cmd_rx.recv() => {
                    match cmd {
                        TrayCmd::LaunchClient => {
                            if let Err(e) = crate::client::launch_or_wake().await {
                                tracing::error!(target: "taix_shell::runner", "launch client failed: {}", e);
                            }
                        }
                    }
                }
                _ = tokio::signal::ctrl_c() => {
                    tracing::info!(target: "taix_shell::runner", "ctrl-c received, shutting down");
                    break;
                }
            }
        }
        service_manager.shutdown();
    });

    tray_shutdown.store(true, Ordering::Relaxed);
    let _ = tray_handle.join();
    let _ = rt.block_on(service_handle);
    Ok(())
}
