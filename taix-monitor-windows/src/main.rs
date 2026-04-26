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

fn parse_data_dir(args: &[String]) -> Option<PathBuf> {
    for i in 0..args.len() {
        if args[i] == "--data-dir" && i + 1 < args.len() {
            return Some(PathBuf::from(&args[i + 1]));
        }
    }
    std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from)
}

#[tokio::main]
async fn main() {
    logging::init();

    let args: Vec<String> = std::env::args().collect();
    let data_dir = parse_data_dir(&args);

    info!("[Taix.Monitor] Starting... dataDir={:?}", data_dir);

    let ignore_processes = config::ignore_processes();

    let app_manager = Arc::new(app_manager::AppManager::new(data_dir));
    let window_manager = Arc::new(window_manager::WindowManager::new());
    let app_observer = app_observer::AppObserver::spawn(Arc::clone(&app_manager), Arc::clone(&window_manager));
    let app_events = app_observer.subscribe();

    let sleep_detector = sleep_detector::SleepDetector::new();
    let sleep_events = sleep_detector.subscribe();

    let app_timer = app_timer::AppTimer::new(app_events, sleep_detector.subscribe(), ignore_processes);
    let duration_events = app_timer.subscribe();

    // 传输层
    let pipe = transport::pipe::NamedPipeTransport::new("TaixDaemon");
    let queue = transport::queue::ReliableMessageQueue::new(pipe);
    let client = Arc::new(transport::client::MonitorClient::new(queue));

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