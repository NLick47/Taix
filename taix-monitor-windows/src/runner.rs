use std::path::PathBuf;
use std::sync::Arc;
use tokio::sync::broadcast;
use tracing::info;

use crate::models::SessionCheckpoint;

/// 崩溃恢复：从 active_session.json 读取上次活跃会话并上报
async fn recover_session(data_dir: &std::path::Path, client: &crate::transport::client::MonitorClient) {
    let cp_path = data_dir.join("active_session.json");
    if !cp_path.exists() {
        return;
    }

    let recovered = match std::fs::read_to_string(&cp_path) {
        Ok(s) => match serde_json::from_str::<SessionCheckpoint>(&s) {
            Ok(cp) => Some(cp),
            Err(e) => {
                tracing::warn!(target: "main", "Corrupt checkpoint: {}", e);
                None
            }
        },
        Err(e) => {
            tracing::warn!(target: "main", "Failed to read checkpoint: {}", e);
            None
        }
    };

    let cp_mtime = match std::fs::metadata(&cp_path) {
        Ok(m) => m.modified().ok(),
        Err(e) => {
            tracing::warn!(target: "main", "Failed to read checkpoint metadata: {}", e);
            None
        }
    };

    if let Some(cp) = recovered {
        let now = chrono::Local::now();
        let since = chrono::DateTime::from_timestamp(cp.since_ts, 0)
            .map(|utc| utc.with_timezone(&chrono::Local))
            .unwrap_or(now);

        let idle = crate::win32::get_system_idle_time();
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
                target: "main",
                "Recovery skipped: system is currently idle ({:?} >= {:?}), assuming sleep state | last app: {}",
                idle, inactive_threshold, cp.process
            );
        } else if too_old {
            info!(
                target: "main",
                "Recovery skipped: checkpoint too old, last app: {}",
                cp.process
            );
        } else {
            let delta = (now - since).num_seconds().max(0);
            let delta = delta.min(3600);
            info!(
                target: "main",
                "Recovered session: {} ({}s since checkpoint, idle={:?})",
                cp.process, delta, idle
            );
            let exe = (!cp.exe_path.is_empty()).then_some(cp.exe_path.as_str());
            let icon = (!cp.icon_path.is_empty()).then_some(cp.icon_path.as_str());
            let desc = if !cp.desc.is_empty() {
                Some(cp.desc.as_str())
            } else {
                Some(cp.process.as_str())
            };
            client.send_app_duration(&cp.process, delta, since, exe, icon, desc).await;
        }
    }

    if let Err(e) = std::fs::remove_file(&cp_path) {
        tracing::debug!(target: "main", "Failed to remove checkpoint: {}", e);
    }
    if let Err(e) = std::fs::remove_file(data_dir.join("active_session.json.tmp")) {
        tracing::debug!(target: "main", "Failed to remove checkpoint tmp: {}", e);
    }
}

pub async fn run(
    data_dir: Option<PathBuf>,
    mut shutdown_rx: tokio::sync::watch::Receiver<()>,
) {
    let _logging_guard = crate::logging::init();

    let default_panic = std::panic::take_hook();
    std::panic::set_hook(Box::new(move |info| {
        tracing::error!(target: "main", "Panic: {}", info);
        default_panic(info);
    }));

    let _single_instance = match crate::win32::single_instance::try_acquire("Global\\TaixMonitorSingleInstance") {
        Some(guard) => guard,
        None => {
            tracing::error!(target: "main", "Another instance is already running. Exiting.");
            return;
        }
    };

    info!(target: "main", "Starting... dataDir={:?}", data_dir);

    let config = crate::config::load_or_create(data_dir.as_deref());
    let ignore_processes = config.ignore_processes;

    let app_manager = Arc::new(crate::app_manager::AppManager::new(data_dir.clone()));
    let app_observer = crate::app_observer::AppObserver::spawn(Arc::clone(&app_manager));
    let app_events = app_observer.subscribe();

    let audio_monitor = crate::win32::audio::AudioMonitor::start();
    let sleep_detector = crate::sleep_detector::SleepDetector::new(audio_monitor.state());
    let sleep_events = sleep_detector.subscribe();

    let app_timer = crate::app_timer::AppTimer::new(app_events, sleep_detector.subscribe(), ignore_processes, data_dir.clone());
    let duration_events = app_timer.subscribe();

    // 传输层
    let pipe = crate::transport::pipe::NamedPipeTransport::new("TaixDaemon");
    let queue = crate::transport::queue::MessageQueue::new(pipe);
    let client = Arc::new(crate::transport::client::MonitorClient::new(queue));

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
                    ).await;
                    info!(
                        target: "main",
                        "Queued app duration: {} = {}s",
                        event.app.process,
                        event.duration_secs,
                    );
                }
                Err(broadcast::error::RecvError::Lagged(n)) => {
                    tracing::warn!(target: "main", "Duration events lagged by {} messages", n);
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
                        crate::models::SleepStatus::Sleep => {
                            info!(target: "main", "System sleep detected, sending notification...");
                            client_clone.send_sleep().await;
                            info!(target: "main", "Sleep notification queued");
                        }
                        crate::models::SleepStatus::Wake => {
                            info!(target: "main", "System wake detected, sending notification...");
                            client_clone.send_wake().await;
                            info!(target: "main", "Wake notification queued");
                        }
                    }
                }
                Err(broadcast::error::RecvError::Lagged(n)) => {
                    tracing::warn!(target: "main", "Sleep events lagged by {} messages", n);
                }
                Err(broadcast::error::RecvError::Closed) => break,
            }
        }
    });

    // 启动核心循环
    let timer_handle = tokio::spawn(app_timer.run());
    let sleep_handle = tokio::spawn(sleep_detector.clone().run());

    info!(target: "main", "Running. Waiting for shutdown signal.");

    // 等待关闭信号
    let _ = shutdown_rx.changed().await;

    info!(target: "main", "Stopping...");

    // 发送关机信号
    sleep_detector.shutdown();
    // drop observer，AppTimer 刷出剩余时长后退出
    drop(app_observer);
    info!(target: "main", "AppObserver stopped");

    // 等待 AppTimer 结束，需在 duration_handle 之前
    if let Err(e) = tokio::time::timeout(std::time::Duration::from_secs(2), timer_handle).await {
        tracing::warn!(target: "main", "AppTimer shutdown timed out: {}", e);
    }
    info!(target: "main", "AppTimer stopped");

    // 等待 duration_handle 处理剩余事件
    if let Err(e) = tokio::time::timeout(std::time::Duration::from_secs(2), duration_handle).await {
        tracing::warn!(target: "main", "Duration handler shutdown timed out: {}", e);
    }
    info!(target: "main", "Duration handler stopped");

    // 等待 sleep detector 结束
    if let Err(e) = tokio::time::timeout(std::time::Duration::from_secs(2), sleep_handle).await {
        tracing::warn!(target: "main", "Sleep detector shutdown timed out: {}", e);
    }
    info!(target: "main", "Sleep detector stopped");

    // 停止音频监控线程（专用 COM 线程，不依赖 tokio 线程池）
    audio_monitor.stop();
    info!(target: "main", "Audio monitor stopped");

    // drop sleep_detector，sleep_event_handle 退出
    drop(sleep_detector);
    if let Err(e) = tokio::time::timeout(std::time::Duration::from_secs(2), sleep_event_handle).await {
        tracing::warn!(target: "main", "Sleep event handler shutdown timed out: {}", e);
    }
    info!(target: "main", "Sleep event handler stopped");

    // 等待 client 任务结束
    if let Ok(client) = Arc::try_unwrap(client) {
        client.shutdown().await;
    } else {
        tracing::warn!(target: "main", "Could not unwrap Arc for client shutdown; background tasks may still hold references");
    }
    info!(target: "main", "MonitorClient stopped");

    info!(target: "main", "Stopped.");
}
