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

    let _guard = crate::win32::single_instance::try_acquire("Global\\TaixMonitorSingleInstance").unwrap_or_else(|| {
        error!(target: "main", "Another instance is already running");
        std::process::exit(1);
    });

    let data_dir = data_dir
        .or_else(|| std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from))
        .unwrap_or_else(|| {
            std::env::current_exe()
                .ok()
                .and_then(|p| p.parent().map(|d| d.to_path_buf()))
                .unwrap_or_else(|| PathBuf::from("."))
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

    // 心跳：前台应用 + 会话开始；睡眠停发
    let mut last_app: Option<crate::models::AppInfo> = None;
    let mut last_since = now_epoch_secs();
    let mut last_session_id = String::new();
    let mut is_sleeping = false;
    let mut beat: u64 = 0;

    loop {
        let loop_start = std::time::Instant::now();
        beat = beat.wrapping_add(1);

        // 前台窗口轮询
        if let Some(event) = app_manager.poll_foreground(&mut last_hwnd) {
            last_app = Some(event.app.clone());
            last_since = now_epoch_secs();
            last_session_id = event.session_id.clone();
            timer.on_app_switch(event);
            if !is_sleeping {
                if let Some(app) = &last_app {
                    send_current(&queue, app, last_since, &last_session_id);
                }
            }
        }

        // Sleep/Wake 检测
        if let Some(status) = sleep_detector.tick() {
            is_sleeping = matches!(status, crate::models::SleepStatus::Sleep);
            if !is_sleeping {
                last_since = now_epoch_secs(); // 唤醒即新会话
                last_session_id = crate::models::next_session_id();
            }
            timer.on_sleep_status_changed(status, last_session_id.clone());
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
            // 唤醒先发 wake 再补 current，避免先到的 current 被 set_sleep(false) 清掉
            if !is_sleeping {
                if let Some(app) = &last_app {
                    send_current(&queue, app, last_since, &last_session_id);
                }
            }
            // 跳过下面重复的 timer.tick
            continue;
        }

        timer.tick(&Some(data_dir.clone()), &queue);

        // 5s 一拍上报 current
        if !is_sleeping && beat % 5 == 0 {
            if let Some(app) = &last_app {
                send_current(&queue, app, last_since, &last_session_id);
            }
        }

        let elapsed = loop_start.elapsed();
        if elapsed < Duration::from_secs(1) {
            std::thread::sleep(Duration::from_secs(1) - elapsed);
        }
    }
}

fn send_current(
    queue: &crate::transport::queue::MessageQueue,
    app: &crate::models::AppInfo,
    since: i64,
    session_id: &str,
) {
    let msg = crate::models::MonitorMessage::Current {
        p: &app.process,
        f: (!app.executable_path.is_empty()).then_some(app.executable_path.as_str()),
        i: (!app.icon_path.is_empty()).then_some(app.icon_path.as_str()),
        s: since,
        sid: session_id,
    };
    let mut json = msg.to_string();
    json.push('\n');
    queue.enqueue(json.into_bytes());
}

fn now_epoch_secs() -> i64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0)
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
            let sid = crate::models::next_session_id();
            let msg = crate::models::MonitorMessage::App {
                p: &cp.process,
                d: delta,
                a: cp.since_ts,
                sid: &sid,
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
