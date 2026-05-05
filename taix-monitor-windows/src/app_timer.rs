use crate::models::{AppDurationEvent, AppInfo, AppType, SessionCheckpoint, SleepStatus, WindowInfo};
use std::path::PathBuf;
use std::time::Duration;
use tokio::sync::broadcast;
use tracing::info;

pub struct AppTimer {
    rx: broadcast::Receiver<crate::models::AppActiveEvent>,
    sleep_rx: broadcast::Receiver<SleepStatus>,
    tx: broadcast::Sender<AppDurationEvent>,
    state: AppTimerState,
    ignore_list: Vec<String>,
    data_dir: Option<PathBuf>,
}

struct AppTimerState {
    active_app: AppInfo,
    active_window: WindowInfo,
    start_time: Option<chrono::DateTime<chrono::Local>>,
    accumulated_ms: i64,
    is_sleeping: bool,
}

impl Default for AppTimerState {
    fn default() -> Self {
        Self {
            active_app: AppInfo::empty(),
            active_window: WindowInfo::empty(),
            start_time: None,
            accumulated_ms: 0,
            is_sleeping: false,
        }
    }
}

impl AppTimer {
    pub fn new(
        event_rx: broadcast::Receiver<crate::models::AppActiveEvent>,
        sleep_rx: broadcast::Receiver<SleepStatus>,
        ignore_list: Vec<String>,
        data_dir: Option<PathBuf>,
    ) -> Self {
        let (tx, _rx) = broadcast::channel(512);
        Self {
            rx: event_rx,
            sleep_rx,
            tx,
            state: AppTimerState::default(),
            ignore_list,
            data_dir,
        }
    }

    pub fn subscribe(&self) -> broadcast::Receiver<AppDurationEvent> {
        self.tx.subscribe()
    }

    pub async fn run(mut self) {
        let mut rx = self.rx;
        let mut sleep_rx = self.sleep_rx;
        let mut periodic_tick = tokio::time::interval(Duration::from_secs(60));
        periodic_tick.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
        // 跳过首次立即触发，等 60s 后再开始
        periodic_tick.reset();
        info!("[AppTimer] Started");

        loop {
            tokio::select! {
                result = rx.recv() => {
                    match result {
                        Ok(event) => {
                            let is_ignored = self.ignore_list.iter().any(|p| p.eq_ignore_ascii_case(&event.app.process));
                            let is_statistical = event.app.app_type != AppType::SystemComponent
                                && !event.app.executable_path.is_empty()
                                && !is_ignored;

                            let now = chrono::Local::now();

                            let app_uid = format!("{}:{}", event.app.process, event.app.executable_path);
                            let current_uid = format!("{}:{}", self.state.active_app.process, self.state.active_app.executable_path);
                            if app_uid != current_uid {
                                let prev_title = self.state.active_window.title.clone();
                                if let Some(args) = build_args(&mut self.state, now, true) {
                                    info!("[AppTimer] App switched | previous: [{}] {} (title: {}) | duration: {}s | new: [{}] {} (title: {})",
                                        args.app.app_type, args.app.process, prev_title, args.duration_secs,
                                        event.app.app_type, event.app.process, event.window.title);
                                    if let Err(e) = self.tx.send(args) {
                                        tracing::warn!("[AppTimer] Failed to broadcast duration: {}", e);
                                    }
                                } else {
                                    info!("[AppTimer] App active: [{}] {} (title: {})", event.app.app_type, event.app.process, event.window.title);
                                }

                                // build_args 的 force_flush 已把毫秒零头进位并清零

                                self.state.start_time = if is_statistical && !self.state.is_sleeping { Some(now) } else { None };
                                self.state.active_app = if is_statistical { event.app } else { AppInfo::empty() };
                                self.state.active_window = if is_statistical { event.window } else { WindowInfo::empty() };
                            }
                        }
                        Err(broadcast::error::RecvError::Lagged(n)) => {
                            tracing::warn!("[AppTimer] App events lagged by {} messages", n);
                        }
                        Err(broadcast::error::RecvError::Closed) => break,
                    }
                }
                result = sleep_rx.recv() => {
                    match result {
                        Ok(status) => {
                            match status {
                                SleepStatus::Sleep => {
                                    if let Some(args) = build_args(&mut self.state, chrono::Local::now(), true) {
                                        info!("[AppTimer] System sleep | flushed: [{}] {} (title: {}) | duration: {}s",
                                            args.app.app_type, args.app.process, self.state.active_window.title, args.duration_secs);
                                        if let Err(e) = self.tx.send(args) {
                                            tracing::warn!("[AppTimer] Failed to broadcast sleep duration: {}", e);
                                        }
                                    }
                                    self.state.start_time = None;
                                    self.state.is_sleeping = true;
                                    info!("[AppTimer] Entered sleep mode, timer paused");
                                }
                                SleepStatus::Wake => {
                                    self.state.is_sleeping = false;
                                    info!("[AppTimer] Exited sleep mode, timer resuming");
                                    if self.state.active_app.is_valid() && self.state.start_time.is_none() {
                                        self.state.start_time = Some(chrono::Local::now());
                                        info!("[AppTimer] System wake | resumed: [{}] {} (title: {})",
                                            self.state.active_app.app_type, self.state.active_app.process, self.state.active_window.title);
                                    }
                                }
                            }
                        }
                        Err(broadcast::error::RecvError::Lagged(n)) => {
                            tracing::warn!("[AppTimer] Sleep events lagged by {} messages", n);
                        }
                        Err(broadcast::error::RecvError::Closed) => break,
                    }
                }
                _ = periodic_tick.tick() => {
                    if self.state.is_sleeping {
                        if self.state.active_app.is_valid() {
                            tracing::warn!("[AppTimer] Skipping periodic flush while sleeping | app={}", self.state.active_app.process);
                        } else {
                            tracing::warn!("[AppTimer] Skipping periodic flush while sleeping | no active app");
                        }
                        continue;
                    }
                    if !self.state.active_app.is_valid() || self.state.start_time.is_none() {
                        continue;
                    }
                    if let Some(args) = build_args(&mut self.state, chrono::Local::now(), true) {
                        info!("[AppTimer] Periodic flush | [{}] {} (title: {}) | duration: {}s",
                            args.app.app_type, args.app.process, self.state.active_window.title, args.duration_secs);
                        if let Err(e) = self.tx.send(args) {
                            tracing::warn!("[AppTimer] Failed to broadcast periodic duration: {}", e);
                        }
                    }
                    self.state.start_time = Some(chrono::Local::now());
                    // 写检查点用于崩溃恢复
                    if let Some(dir) = &self.data_dir {
                        if self.state.active_app.is_valid() {
                            if let Some(since) = self.state.start_time {
                                let cp = SessionCheckpoint {
                                    process: self.state.active_app.process.clone(),
                                    exe_path: self.state.active_app.executable_path.clone(),
                                    icon_path: self.state.active_app.icon_path.clone(),
                                    desc: self.state.active_app.description.clone(),
                                    since_ts: since.timestamp(),
                                };
                                let path = dir.join("active_session.json");
                                let tmp = dir.join("active_session.json.tmp");
                                if let Ok(json) = serde_json::to_string(&cp) {
                                    if std::fs::write(&tmp, &json).is_ok() {
                                        let _ = std::fs::rename(&tmp, &path);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        // 退出时刷出剩余时间
        if let Some(args) = build_args(&mut self.state, chrono::Local::now(), true) {
            info!("[AppTimer] Shutdown flush | last: [{}] {} (title: {}) | duration: {}s",
                args.app.app_type, args.app.process, self.state.active_window.title, args.duration_secs);
            if let Err(e) = self.tx.send(args) {
                tracing::warn!("[AppTimer] Failed to broadcast final duration: {}", e);
            }
        }

        info!("[AppTimer] Stopped");
    }
}

fn build_args(
    state: &mut AppTimerState,
    end_time: chrono::DateTime<chrono::Local>,
    force_flush: bool,
) -> Option<AppDurationEvent> {
    let start = state.start_time?;
    let active = &state.active_app;
    if !active.is_valid() {
        return None;
    }

    let duration_ms = (end_time - start).num_milliseconds();
    if duration_ms <= 0 {
        return None;
    }

    // 单次 flush 不超过 1 小时，防止系统时钟跳变（NTP、手动调时、休眠恢复）导致异常时长
    const MAX_FLUSH_DURATION_MS: i64 = 3600_000;
    let duration_ms = if duration_ms > MAX_FLUSH_DURATION_MS {
        tracing::warn!(
            "[AppTimer] Flush duration {}ms exceeds max {}ms for {}, truncating",
            duration_ms, MAX_FLUSH_DURATION_MS, active.process
        );
        MAX_FLUSH_DURATION_MS
    } else {
        duration_ms
    };

    let total_ms = duration_ms + state.accumulated_ms;
    let mut duration_secs = total_ms / 1000;
    state.accumulated_ms = total_ms % 1000;

    // 强制刷盘时毫秒零头四舍五入（>=500ms 进位），同时清零防止归属给下一个应用
    if force_flush {
        if state.accumulated_ms >= 500 {
            duration_secs += 1;
        }
        state.accumulated_ms = 0;
    }

    if duration_secs <= 0 {
        return None;
    }

    Some(AppDurationEvent {
        duration_secs,
        app: active.clone(),
        start_time: start,
    })
}
