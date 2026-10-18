use crate::models::{AppDurationEvent, AppInfo, AppType, SessionCheckpoint, SleepStatus, WindowInfo};
use crate::transport::queue::MessageQueue;
use crate::win32::get_tick_ms;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};
use tracing::{info, warn};

const CHECKPOINT_INTERVAL_MS: u64 = 10_000;

enum FlushKind {
    Periodic,
    Final,
}

#[derive(Debug, Clone)]
pub struct ActiveSession {
    pub app: AppInfo,
    pub start_time: SystemTime,
    pub session_id: String,
}

impl ActiveSession {
    pub fn start_epoch(&self) -> i64 {
        self.start_time
            .duration_since(UNIX_EPOCH)
            .unwrap_or_default()
            .as_secs() as i64
    }
}

enum TimerState {
    Idle,
    Tracking {
        app: AppInfo,
        window: WindowInfo,
        start_time: SystemTime,
        start_tick: u64,
        accumulated_ms: i64,
        last_checkpoint_tick: u64,
        session_id: String,
    },
    Suspended {
        app: Option<AppInfo>,
        window: Option<WindowInfo>,
    },
}

pub struct AppTimer {
    state: TimerState,
    pending: Option<AppDurationEvent>,
}

impl AppTimer {
    pub fn new() -> Self {
        Self {
            state: TimerState::Idle,
            pending: None,
        }
    }

    fn is_statistical(app: &AppInfo) -> bool {
        app.app_type != AppType::SystemComponent && !app.executable_path.is_empty()
    }

    pub fn on_app_switch(&mut self, event: crate::models::AppActiveEvent) {
        let now = SystemTime::now();
        let now_tick = get_tick_ms();
        let is_statistical = Self::is_statistical(&event.app);

        match &mut self.state {
            TimerState::Suspended { app, window, .. } => {
                if is_statistical {
                    *app = Some(event.app);
                    *window = Some(event.window);
                } else {
                    *app = None;
                    *window = None;
                }
                return;
            }
            TimerState::Idle => {
                if is_statistical {
                    info!(
                        target: "app_timer",
                        "App active: [{}] {} (title: {})",
                        event.app.app_type, event.app.process, event.window.title
                    );
                    self.state = TimerState::Tracking {
                        app: event.app,
                        window: event.window,
                        start_time: now,
                        start_tick: now_tick,
                        accumulated_ms: 0,
                        last_checkpoint_tick: now_tick,
                        session_id: event.session_id,
                    };
                }
                return;
            }
            TimerState::Tracking {
                app,
                window,
                start_time,
                start_tick,
                accumulated_ms,
                session_id,
                ..
            } => {
                let identity_changed = event.app.process != app.process
                    || event.app.executable_path != app.executable_path;

                if identity_changed {
                    let flush = Self::compute_flush(
                        *start_time,
                        *start_tick,
                        now,
                        now_tick,
                        FlushKind::Final,
                        accumulated_ms,
                        app,
                        session_id,
                    );
                    if let Some(duration_event) = &flush {
                        info!(
                            target: "app_timer",
                            "App switched | previous: [{}] {} (title: {}) | duration: {}s | new: [{}] {} (title: {})",
                            duration_event.app.app_type, duration_event.app.process, window.title,
                            duration_event.duration_secs,
                            event.app.app_type, event.app.process, event.window.title
                        );
                    } else {
                        info!(
                            target: "app_timer",
                            "App active: [{}] {} (title: {})",
                            event.app.app_type, event.app.process, event.window.title
                        );
                    }

                    if is_statistical {
                        self.state = TimerState::Tracking {
                            app: event.app,
                            window: event.window,
                            start_time: now,
                            start_tick: now_tick,
                            accumulated_ms: 0,
                            last_checkpoint_tick: now_tick,
                            session_id: event.session_id,
                        };
                    } else {
                        self.state = TimerState::Idle;
                    }

                    self.pending = flush;
                } else {
                    *window = event.window;
                }
            }
        }
    }

    pub fn on_sleep_status_changed(&mut self, status: SleepStatus, new_session_id: String) {
        let now = SystemTime::now();
        let now_tick = get_tick_ms();
        match status {
            SleepStatus::Sleep => match &mut self.state {
                TimerState::Tracking {
                    app,
                    window,
                    start_time,
                    start_tick,
                    accumulated_ms,
                    session_id,
                    ..
                } => {
                    let flush = Self::compute_flush(
                        *start_time,
                        *start_tick,
                        now,
                        now_tick,
                        FlushKind::Final,
                        accumulated_ms,
                        app,
                        session_id,
                    );
                    if let Some(event) = &flush {
                        info!(
                            target: "app_timer",
                            "System sleep | flushed: [{}] {} (title: {}) | duration: {}s",
                            event.app.app_type, event.app.process, window.title,
                            event.duration_secs
                        );
                        self.pending = flush;
                    }
                    let app_owned = std::mem::replace(
                        app,
                        AppInfo {
                            process: String::new(),
                            description: String::new(),
                            executable_path: String::new(),
                            icon_path: String::new(),
                            app_type: AppType::Win32,
                        },
                    );
                    let window_owned = std::mem::replace(
                        window,
                        WindowInfo::new(String::new(), String::new(), 0),
                    );
                    self.state = TimerState::Suspended {
                        app: Some(app_owned),
                        window: Some(window_owned),
                    };
                }
                TimerState::Idle => {
                    self.state = TimerState::Suspended {
                        app: None,
                        window: None,
                    };
                }
                TimerState::Suspended { .. } => {}
            },
            SleepStatus::Wake => match &mut self.state {
                TimerState::Suspended {
                    app,
                    window,
                    ..
                } => {
                    info!(target: "app_timer", "Exited sleep mode, timer resuming");
                    if let Some(app) = app.take() {
                        let window = window
                            .take()
                            .unwrap_or_else(|| WindowInfo::new(String::new(), String::new(), 0));
                        info!(
                            target: "app_timer",
                            "System wake | resumed: [{}] {} (title: {})",
                            app.app_type, app.process, window.title
                        );
                        self.state = TimerState::Tracking {
                            app,
                            window,
                            start_time: now,
                            start_tick: now_tick,
                            accumulated_ms: 0,
                            last_checkpoint_tick: now_tick,
                            session_id: new_session_id,
                        };
                    } else {
                        self.state = TimerState::Idle;
                    }
                }
                _ => {}
            },
        }
    }

    /// 当前真正在跟踪的会话；Idle/睡眠/非统计应用都返回 None
    pub fn active_session(&self) -> Option<ActiveSession> {
        match &self.state {
            TimerState::Tracking {
                app,
                start_time,
                session_id,
                ..
            } => Some(ActiveSession {
                app: app.clone(),
                start_time: *start_time,
                session_id: session_id.clone(),
            }),
            _ => None,
        }
    }

    pub fn checkpoint_now(&self, data_dir: &Option<std::path::PathBuf>) {
        let Some(dir) = data_dir else {
            return;
        };
        match &self.state {
            TimerState::Tracking {
                app,
                start_time,
                session_id,
                ..
            } => {
                let since_ts = start_time
                    .duration_since(UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_secs() as i64;
                let last_seen = SystemTime::now()
                    .duration_since(UNIX_EPOCH)
                    .unwrap_or_default()
                    .as_secs() as i64;
                let cp = SessionCheckpoint {
                    process: app.process.clone(),
                    exe_path: app.executable_path.clone(),
                    icon_path: app.icon_path.clone(),
                    desc: app.description.clone(),
                    since_ts,
                    session_id: session_id.clone(),
                    last_seen,
                };
                write_checkpoint(dir, &cp);
            }
            TimerState::Idle | TimerState::Suspended { .. } => clear_checkpoint(data_dir),
        }
    }

    pub fn tick(&mut self, data_dir: &Option<std::path::PathBuf>, queue: &MessageQueue) {
        let now_tick = get_tick_ms();
        let mut checkpoint_due = false;

        match &mut self.state {
            TimerState::Tracking {
                last_checkpoint_tick,
                ..
            } => {
                if now_tick >= *last_checkpoint_tick
                    && now_tick - *last_checkpoint_tick >= CHECKPOINT_INTERVAL_MS
                {
                    *last_checkpoint_tick = now_tick;
                    checkpoint_due = true;
                }
            }
            TimerState::Idle | TimerState::Suspended { .. } => {
                // 不活跃/睡眠时不能留下可恢复的进行中会话
                clear_checkpoint(data_dir);
            }
        }

        if checkpoint_due {
            self.checkpoint_now(data_dir);
        }

        // 消费 pending event
        if let Some(event) = self.pending.take() {
            send_duration_event(queue, &event);
        }
    }

    fn compute_flush(
        start_time: SystemTime,
        start_tick: u64,
        _now: SystemTime,
        now_tick: u64,
        kind: FlushKind,
        accumulated_ms: &mut i64,
        app: &AppInfo,
        session_id: &str,
    ) -> Option<AppDurationEvent> {
        let duration_ms = if now_tick >= start_tick {
            (now_tick - start_tick) as i64
        } else {
            0
        };
        if duration_ms <= 0 {
            return None;
        }

        let total_ms = duration_ms + *accumulated_ms;
        let mut duration_secs = total_ms / 1000;
        let remainder = total_ms % 1000;

        match kind {
            FlushKind::Periodic => {
                *accumulated_ms = remainder;
            }
            FlushKind::Final => {
                if remainder >= 500 {
                    duration_secs += 1;
                }
                *accumulated_ms = 0;
            }
        }

        if duration_secs <= 0 {
            return None;
        }

        Some(AppDurationEvent {
            duration_secs,
            app: app.clone(),
            start_time,
            session_id: session_id.to_string(),
        })
    }

    /// 检查是否有被拒绝的时长
    pub fn flush_pending(&mut self, queue: &MessageQueue) {
        if let Some(event) = self.pending.take() {
            send_duration_event(queue, &event);
        }
    }
}


fn send_duration_event(queue: &MessageQueue, event: &AppDurationEvent) {
    let exe = (!event.app.executable_path.is_empty())
        .then_some(event.app.executable_path.as_str());
    let icon = (!event.app.icon_path.is_empty()).then_some(event.app.icon_path.as_str());
    let desc = (!event.app.description.is_empty())
        .then_some(event.app.description.as_str())
        .or_else(|| Some(event.app.process.as_str()));
    let a = event
        .start_time
        .duration_since(UNIX_EPOCH)
        .unwrap_or_default()
        .as_secs() as i64;
    let msg = crate::models::MonitorMessage::App {
        p: &event.app.process,
        d: event.duration_secs,
        a,
        sid: &event.session_id,
        f: exe,
        i: icon,
        desc,
    };
    let mut json = msg.to_string();
    json.push('\n');
    queue.enqueue(json.into_bytes());
}

fn write_checkpoint(dir: &Path, cp: &SessionCheckpoint) {
    let json = cp.to_string();
    let path = dir.join("active_session.json");
    let tmp = dir.join("active_session.json.tmp");
    if let Err(e) = std::fs::write(&tmp, json.as_bytes()) {
        warn!(target: "app_timer", "Failed to write checkpoint tmp: {}", e);
    } else if let Err(e) = std::fs::rename(&tmp, &path) {
        warn!(target: "app_timer", "Failed to rename checkpoint: {}", e);
    }
}

fn clear_checkpoint(data_dir: &Option<std::path::PathBuf>) {
    if let Some(dir) = data_dir {
        let path = dir.join("active_session.json");
        let _ = std::fs::remove_file(&path);
        let tmp = dir.join("active_session.json.tmp");
        let _ = std::fs::remove_file(&tmp);
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::models::AppActiveEvent;

    fn app(process: &str) -> AppInfo {
        AppInfo {
            process: process.to_string(),
            description: process.to_string(),
            executable_path: format!(r"C:\{process}.exe"),
            icon_path: String::new(),
            app_type: AppType::Win32,
        }
    }

    fn event(process: &str, session_id: &str) -> AppActiveEvent {
        AppActiveEvent {
            app: app(process),
            window: WindowInfo::new("class".to_string(), "title".to_string(), 1),
            session_id: session_id.to_string(),
        }
    }

    #[test]
    fn same_process_window_change_keeps_session_identity() {
        let mut timer = AppTimer::new();
        timer.on_app_switch(event("chrome.exe", "s1"));
        let first = timer.active_session().unwrap();
        timer.on_app_switch(event("chrome.exe", "s2"));
        let second = timer.active_session().unwrap();
        assert_eq!(second.session_id, "s1");
        assert_eq!(second.start_time, first.start_time);
    }

    #[test]
    fn different_process_starts_new_session() {
        let mut timer = AppTimer::new();
        timer.on_app_switch(event("chrome.exe", "s1"));
        timer.on_app_switch(event("code.exe", "s2"));
        let session = timer.active_session().unwrap();
        assert_eq!(session.session_id, "s2");
        assert_eq!(session.app.process, "code.exe");
    }

    #[test]
    fn non_statistical_app_has_no_active_session() {
        let mut timer = AppTimer::new();
        let mut ev = event("system.exe", "s1");
        ev.app.app_type = AppType::SystemComponent;
        timer.on_app_switch(ev);
        assert!(timer.active_session().is_none());
    }
}
