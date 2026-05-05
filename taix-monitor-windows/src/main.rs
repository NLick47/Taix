#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

mod app_manager;
mod app_observer;
mod app_timer;
mod config;
mod logging;
mod models;
mod sleep_detector;
mod transport;
mod win32;
mod window_manager;

use std::path::PathBuf;
use std::sync::Arc;
use tokio::signal;
use tokio::sync::broadcast;
use tracing::{error, info};

use models::SessionCheckpoint;

fn parse_data_dir(args: &[String]) -> Option<PathBuf> {
    for i in 0..args.len() {
        if args[i] == "--data-dir" && i + 1 < args.len() {
            return Some(PathBuf::from(&args[i + 1]));
        }
    }
    std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from)
}

/// 崩溃恢复：从 active_session.json 读取上次活跃会话并上报
async fn recover_session(data_dir: &std::path::Path, client: &transport::client::MonitorClient) {
    let cp_path = data_dir.join("active_session.json");
    if !cp_path.exists() {
        return;
    }

    let recovered = std::fs::read_to_string(&cp_path)
        .ok()
        .and_then(|s| serde_json::from_str::<SessionCheckpoint>(&s).ok());

    let cp_mtime = std::fs::metadata(&cp_path)
        .ok()
        .and_then(|m| m.modified().ok());

    if let Some(cp) = recovered {
        let now = chrono::Local::now();
        let since = chrono::DateTime::from_timestamp(cp.since_ts, 0)
            .map(|utc| utc.with_timezone(&chrono::Local))
            .unwrap_or(now);

        let idle = win32::get_system_idle_time();
        let inactive_threshold = if cfg!(debug_assertions) {
            std::time::Duration::from_secs(10)
        } else {
            std::time::Duration::from_secs(300)
        };

        let too_old = cp_mtime.map_or(false, |t| {
            t.elapsed().map_or(false, |e| e > std::time::Duration::from_secs(300))
        });

        if idle >= inactive_threshold {
            info!(
                "[Program] Recovery skipped: system is currently idle ({:?} >= {:?}), assuming sleep state | last app: {}",
                idle, inactive_threshold, cp.process
            );
        } else if too_old {
            info!(
                "[Program] Recovery skipped: checkpoint too old, last app: {}",
                cp.process
            );
        } else {
            let delta = (now - since).num_seconds().max(0);
            let delta = delta.min(3600);
            info!(
                "[Program] Recovered session: {} ({}s since checkpoint, idle={:?})",
                cp.process, delta, idle
            );
            let exe = (!cp.exe_path.is_empty()).then_some(cp.exe_path.as_str());
            let icon = (!cp.icon_path.is_empty()).then_some(cp.icon_path.as_str());
            let desc = if !cp.desc.is_empty() {
                Some(cp.desc.as_str())
            } else {
                Some(cp.process.as_str())
            };
            client.send_app_duration(&cp.process, delta, since, exe, icon, desc);
        }
    }

    let _ = std::fs::remove_file(&cp_path);
    let _ = std::fs::remove_file(data_dir.join("active_session.json.tmp"));
}

#[tokio::main]
async fn main() {
    let _logging_guard = logging::init();

    let default_panic = std::panic::take_hook();
    std::panic::set_hook(Box::new(move |info| {
        tracing::error!("[Program] Panic: {}", info);
        default_panic(info);
    }));

    let _single_instance = match win32::single_instance::try_acquire("Global\\TaixMonitorSingleInstance") {
        Some(guard) => guard,
        None => {
            tracing::error!("[Taix.Monitor] Another instance is already running. Exiting.");
            return;
        }
    };

    let args: Vec<String> = std::env::args().collect();
    let data_dir = parse_data_dir(&args);

    info!("[Taix.Monitor] Starting... dataDir={:?}", data_dir);

    let config = config::load_or_create(data_dir.as_deref());
    let ignore_processes = config.ignore_processes;

    let app_manager = Arc::new(app_manager::AppManager::new(data_dir.clone()));
    let app_observer = app_observer::AppObserver::spawn(Arc::clone(&app_manager));
    let app_events = app_observer.subscribe();

    let audio_monitor = win32::audio::AudioMonitor::start();
    let sleep_detector = sleep_detector::SleepDetector::new(audio_monitor.state());
    let sleep_events = sleep_detector.subscribe();

    let app_timer = app_timer::AppTimer::new(app_events, sleep_detector.subscribe(), ignore_processes, data_dir.clone());
    let duration_events = app_timer.subscribe();

    // 传输层
    let pipe = transport::pipe::NamedPipeTransport::new("TaixDaemon");
    let queue = transport::queue::MessageQueue::new(pipe);
    let client = Arc::new(transport::client::MonitorClient::new(queue));

    // 崩溃恢复
    if let Some(ref data_dir) = data_dir {
        recover_session(data_dir, &client).await;
    }

    // app 时长处理
    let client_clone = Arc::clone(&client);
    let duration_handle = tokio::spawn(async move {
        let mut rx = duration_events;
        loop {
            match rx.recv().await {
                Ok(event) => {
                    let exe = (!event.app.executable_path.is_empty()).then_some(event.app.executable_path.as_str());
                    let icon = (!event.app.icon_path.is_empty()).then_some(event.app.icon_path.as_str());
                    let desc = (!event.app.description.is_empty())
                        .then_some(event.app.description.as_str())
                        .or_else(|| Some(event.app.process.as_str()));
                    client_clone.send_app_duration(
                        &event.app.process,
                        event.duration_secs,
                        event.start_time,
                        exe,
                        icon,
                        desc,
                    );
                    info!(
                        "[Program] Queued app duration: {} = {}s",
                        event.app.process,
                        event.duration_secs,
                    );
                }
                Err(broadcast::error::RecvError::Lagged(n)) => {
                    tracing::warn!("[Program] Duration events lagged by {} messages", n);
                }
                Err(broadcast::error::RecvError::Closed) => break,
            }
        }
    });

    // 睡眠检测处理
    let client_clone = Arc::clone(&client);
    let sleep_event_handle = tokio::spawn(async move {
        let mut rx = sleep_events;
        loop {
            match rx.recv().await {
                Ok(status) => {
                    match status {
                        models::SleepStatus::Sleep => {
                            info!("[Program] System sleep detected, sending notification...");
                            client_clone.send_sleep();
                            info!("[Program] Sleep notification queued");
                        }
                        models::SleepStatus::Wake => {
                            info!("[Program] System wake detected, sending notification...");
                            client_clone.send_wake();
                            info!("[Program] Wake notification queued");
                        }
                    }
                }
                Err(broadcast::error::RecvError::Lagged(n)) => {
                    tracing::warn!("[Program] Sleep events lagged by {} messages", n);
                }
                Err(broadcast::error::RecvError::Closed) => break,
            }
        }
    });

    // 启动核心循环
    let timer_handle = tokio::spawn(app_timer.run());
    let sleep_handle = tokio::spawn(sleep_detector.clone().run());

    info!("[Taix.Monitor] Running. Press Ctrl+C to exit.");

    if let Err(e) = signal::ctrl_c().await {
        error!("[Program] Failed to listen for ctrl-c: {}", e);
    }

    info!("[Taix.Monitor] Stopping...");

    // 发送关机信号
    sleep_detector.shutdown();
    // drop observer，AppTimer 刷出剩余时长后退出
    drop(app_observer);
    info!("[Taix.Monitor] AppObserver stopped");

    // 等待 AppTimer 结束，需在 duration_handle 之前
    let _ = tokio::time::timeout(std::time::Duration::from_secs(2), timer_handle).await;
    info!("[Taix.Monitor] AppTimer stopped");

    // 等待 duration_handle 处理剩余事件
    let _ = tokio::time::timeout(std::time::Duration::from_secs(2), duration_handle).await;
    info!("[Taix.Monitor] Duration handler stopped");

    // 等待 sleep detector 结束
    let _ = tokio::time::timeout(std::time::Duration::from_secs(2), sleep_handle).await;
    info!("[Taix.Monitor] Sleep detector stopped");

    // 停止音频监控线程（专用 COM 线程，不依赖 tokio 线程池）
    audio_monitor.stop();
    info!("[Taix.Monitor] Audio monitor stopped");

    // drop sleep_detector，sleep_event_handle 退出
    drop(sleep_detector);
    let _ = tokio::time::timeout(std::time::Duration::from_secs(2), sleep_event_handle).await;
    info!("[Taix.Monitor] Sleep event handler stopped");

    // 等待 client 任务结束
    if let Ok(client) = Arc::try_unwrap(client) {
        client.shutdown().await;
    } else {
        tracing::warn!("[Taix.Monitor] Could not unwrap Arc for client shutdown; background tasks may still hold references");
    }
    info!("[Taix.Monitor] MonitorClient stopped");

    info!("[Taix.Monitor] Stopped.");
}