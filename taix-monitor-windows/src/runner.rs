use std::path::PathBuf;
use std::time::Duration;

use crate::models::MonitorConfig;
use crate::win32::audio::AudioMonitor;
use tracing::{error, info, warn};

pub fn run(
    data_dir: Option<PathBuf>,
    inactive_threshold_secs: Option<u64>,
    max_sound_duration_secs: Option<u64>,
    sleep_watch: Option<bool>,
) -> ! {
    // 单实例锁
    drop(crate::win32::single_instance::try_acquire("Global\\TaixMonitorSingleInstance").unwrap_or_else(|| {
        error!(target: "main", "Another instance is already running");
        std::process::exit(1);
    }));

    let data_dir = data_dir
        .or_else(|| std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from))
        .unwrap_or_else(|| {
            std::env::current_exe()
                .ok()
                .and_then(|p| p.parent().map(|d| d.to_path_buf().join("Data")))
                .unwrap_or_else(|| PathBuf::from("Data"))
        });

    info!(target: "main", "Starting... dataDir={:?}", data_dir);

    let config = MonitorConfig {
        inactive_threshold_secs: inactive_threshold_secs.unwrap_or(900),
        max_sound_duration_secs: max_sound_duration_secs.unwrap_or(7200),
        sleep_watch: sleep_watch.unwrap_or(true),
    };
    info!(
        target: "main",
        "Config: inactive_threshold={}s, max_sound_duration={}s, sleep_watch={}",
        config.inactive_threshold_secs,
        config.max_sound_duration_secs,
        config.sleep_watch
    );

    // 启动音频监控 COM 线程
    let audio_monitor = AudioMonitor::start();
    let audio_state = audio_monitor.state();

    // 初始化组件
    let mut app_manager = crate::app_manager::AppManager::new(Some(data_dir.clone()));
    let mut timer = crate::app_timer::AppTimer::new();
    let mut sleep_detector = crate::sleep_detector::SleepDetector::new(audio_state, &config);
    let queue = crate::transport::queue::MessageQueue::new("TaixDaemon");

    // 崩溃恢复
    try_recover_session(&data_dir, &queue, &config);

    let mut last_hwnd = windows::Win32::Foundation::HWND(0 as *mut _);
    info!(target: "main", "Running.");

    loop {
        let loop_start = std::time::Instant::now();

        // 前台窗口轮询
        if let Some(event) = app_manager.poll_foreground(&mut last_hwnd) {
            timer.on_app_switch(event);
        }

        // Sleep/Wake 检测
        if let Some(status) = sleep_detector.tick() {
            timer.on_sleep_status_changed(status);
            info!(
                target: "main",
                "{} detected",
                match status {
                    crate::models::SleepStatus::Sleep => "System sleep",
                    crate::models::SleepStatus::Wake => "System wake",
                }
            );

            timer.tick(&Some(data_dir.clone()), &queue);
            let msg = match status {
                crate::models::SleepStatus::Sleep => crate::models::MonitorMessage::Sleep,
                crate::models::SleepStatus::Wake => crate::models::MonitorMessage::Wake,
            };
            let mut json = msg.to_string();
            json.push('\n');
            queue.enqueue(json.into_bytes());
            // 跳过下面重复的 timer.tick
            continue;
        }

        timer.tick(&Some(data_dir.clone()), &queue);

        let elapsed = loop_start.elapsed();
        if elapsed < Duration::from_secs(1) {
            std::thread::sleep(Duration::from_secs(1) - elapsed);
        }
    }
}

/// 崩溃恢复 从 active_session.json 读取上次活跃会话并上报
fn try_recover_session(data_dir: &std::path::Path, queue: &crate::transport::queue::MessageQueue, config: &MonitorConfig) {
    let cp_path = data_dir.join("active_session.json");
    if !cp_path.exists() {
        return;
    }

    let recovered = match std::fs::read_to_string(&cp_path) {
        Ok(s) => match crate::models::SessionCheckpoint::from_str(&s) {
            Some(cp) => Some(cp),
            None => {
                warn!(target: "main", "Corrupt checkpoint");
                None
            }
        },
        Err(e) => {
            warn!(target: "main", "Failed to read checkpoint: {}", e);
            None
        }
    };

    let cp_mtime = std::fs::metadata(&cp_path).ok().and_then(|m| m.modified().ok());

    if let Some(cp) = recovered {
        let now = std::time::SystemTime::now();
        let since = std::time::UNIX_EPOCH + std::time::Duration::from_secs(cp.since_ts.max(0) as u64);

        let idle = crate::win32::get_system_idle_time().unwrap_or(Duration::ZERO);
        let inactive_threshold = Duration::from_secs(config.inactive_threshold_secs);

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
            let delta = now
                .duration_since(since)
                .unwrap_or_default()
                .as_secs() as i64;
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
            let msg = crate::models::MonitorMessage::App {
                p: &cp.process,
                d: delta,
                a: cp.since_ts,
                f: exe,
                i: icon,
                desc,
            };
            let mut json = msg.to_string();
            json.push('\n');
            queue.enqueue(json.into_bytes());
        }
    }

    let _ = std::fs::remove_file(&cp_path);
    let _ = std::fs::remove_file(data_dir.join("active_session.json.tmp"));
}
