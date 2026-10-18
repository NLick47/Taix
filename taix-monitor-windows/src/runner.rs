use std::path::{Path, PathBuf};
use std::time::Duration;

use crate::app_timer::ActiveSession;
use crate::models::{MonitorConfig, MonitorMessage, SessionCheckpoint, SleepStatus};
use crate::transport::queue::MessageQueue;
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
    let queue = MessageQueue::new("TaixDaemon");

    // 崩溃恢复：只补到检查点最后一次观测到的时间，并复用原 session id
    try_recover_session(&data_dir, &queue);

    let data_dir_opt = Some(data_dir.clone());
    let mut last_hwnd = windows::Win32::Foundation::HWND(0 as *mut _);
    info!(target: "main", "Running.");

    let mut is_sleeping = false;
    let mut beat: u64 = 0;

    loop {
        let loop_start = std::time::Instant::now();
        beat = beat.wrapping_add(1);

        // 前台窗口轮询
        if let Some(event) = app_manager.poll_foreground(&mut last_hwnd) {
            timer.on_app_switch(event);
            // 先把上一段结算消息入队，再发新会话的 current，避免顺序反了
            timer.flush_pending(&queue);
            timer.checkpoint_now(&data_dir_opt);
            if !is_sleeping {
                send_current(&queue, timer.active_session().as_ref());
            }
        }

        // Sleep/Wake 检测
        if let Some(status) = sleep_detector.tick() {
            is_sleeping = matches!(status, SleepStatus::Sleep);
            let new_session_id = if is_sleeping {
                String::new()
            } else {
                crate::models::next_session_id()
            };
            timer.on_sleep_status_changed(status, new_session_id);
            timer.checkpoint_now(&data_dir_opt);
            info!(
                target: "main",
                "{} detected",
                match status {
                    SleepStatus::Sleep => "System sleep",
                    SleepStatus::Wake => "System wake",
                }
            );

            timer.tick(&data_dir_opt, &queue);
            let msg = match status {
                SleepStatus::Sleep => MonitorMessage::Sleep,
                SleepStatus::Wake => MonitorMessage::Wake,
            };
            let mut json = msg.to_string();
            json.push('\n');
            queue.enqueue(json.into_bytes());
            // 唤醒先发 wake 再补 current，避免先到的 current 被 set_sleep(false) 清掉
            if !is_sleeping {
                send_current(&queue, timer.active_session().as_ref());
            }
            // 跳过下面重复的 timer.tick
            continue;
        }

        timer.tick(&data_dir_opt, &queue);

        // 5s 一拍上报 current。没有可统计会话时发空 p，让 server 清空快照
        if !is_sleeping && beat % 5 == 0 {
            send_current(&queue, timer.active_session().as_ref());
        }

        let elapsed = loop_start.elapsed();
        if elapsed < Duration::from_secs(1) {
            std::thread::sleep(Duration::from_secs(1) - elapsed);
        }
    }
}

fn send_current(queue: &MessageQueue, session: Option<&ActiveSession>) {
    let msg = match session {
        Some(session) => MonitorMessage::Current {
            p: &session.app.process,
            f: (!session.app.executable_path.is_empty())
                .then_some(session.app.executable_path.as_str()),
            i: (!session.app.icon_path.is_empty()).then_some(session.app.icon_path.as_str()),
            s: session.start_epoch(),
            sid: &session.session_id,
        },
        None => MonitorMessage::Current {
            p: "",
            f: None,
            i: None,
            s: 0,
            sid: "",
        },
    };
    let mut json = msg.to_string();
    json.push('\n');
    queue.enqueue(json.into_bytes());
}

/// 崩溃恢复：读检查点，按 last_seen 结算到崩溃前最后一次观测点。
/// 复用检查点里的 session id，server 已收到过这段结算时会直接去重。
fn try_recover_session(data_dir: &Path, queue: &MessageQueue) {
    let cp_path = data_dir.join("active_session.json");
    if !cp_path.exists() {
        return;
    }

    let cp = match std::fs::read_to_string(&cp_path) {
        Ok(s) => match SessionCheckpoint::from_str(&s) {
            Some(cp) => cp,
            None => {
                warn!(target: "main", "Corrupt checkpoint, skipped");
                cleanup_checkpoint(data_dir);
                return;
            }
        },
        Err(e) => {
            warn!(target: "main", "Failed to read checkpoint: {}", e);
            cleanup_checkpoint(data_dir);
            return;
        }
    };

    let Some(plan) = cp.recovery_plan() else {
        // 旧格式没有 sid/last_seen：无法安全去重，宁可丢这一段也不重复记账
        warn!(
            target: "main",
            "Recovery skipped: checkpoint for {} has no session id/last_seen", cp.process
        );
        cleanup_checkpoint(data_dir);
        return;
    };

    let exe = (!cp.exe_path.is_empty()).then_some(cp.exe_path.as_str());
    let icon = (!cp.icon_path.is_empty()).then_some(cp.icon_path.as_str());
    let desc = if !cp.desc.is_empty() {
        Some(cp.desc.as_str())
    } else {
        Some(cp.process.as_str())
    };
    let msg = MonitorMessage::App {
        p: &cp.process,
        d: plan.duration_secs,
        a: plan.since_ts,
        sid: &plan.session_id,
        f: exe,
        i: icon,
        desc,
    };
    let mut json = msg.to_string();
    json.push('\n');
    queue.enqueue(json.into_bytes());
    info!(
        target: "main",
        "Recovered session: {} ({}s observed, sid={})",
        cp.process, plan.duration_secs, plan.session_id
    );

    cleanup_checkpoint(data_dir);
}

fn cleanup_checkpoint(data_dir: &Path) {
    let _ = std::fs::remove_file(data_dir.join("active_session.json"));
    let _ = std::fs::remove_file(data_dir.join("active_session.json.tmp"));
}
