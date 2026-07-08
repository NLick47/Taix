use crate::models::{AppDurationEvent, AppInfo, AppType, SessionCheckpoint, SleepStatus, WindowInfo};
use crate::transport::queue::MessageQueue;
use crate::win32::get_tick_ms;
use std::path::Path;
use std::time::{SystemTime, UNIX_EPOCH};
use tracing::{info, warn};

enum FlushKind {
    Periodic,
    Final,
}

enum TimerState {
    Idle,
    Tracking {
        app: AppInfo,
        window: WindowInfo,
        start_time: SystemTime,
        start_tick: u64,
        accumulated_ms: i64,
        last_periodic_tick: u64,
    },
    Suspended {
        app: Option<AppInfo>,
        window: Option<WindowInfo>,
        accumulated_ms: i64,
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
                        last_periodic_tick: now_tick,
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
                            last_periodic_tick: now_tick,
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

    pub fn on_sleep_status_changed(&mut self, status: SleepStatus) {
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
                        accumulated_ms: 0,
                    };
                }
                TimerState::Idle => {
                    self.state = TimerState::Suspended {
                        app: None,
                        window: None,
                        accumulated_ms: 0,
                    };
                }
                TimerState::Suspended { .. } => {}
            },
            SleepStatus::Wake => match &mut self.state {
                TimerState::Suspended {
                    app,
                    window,
                    accumulated_ms,
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
                            accumulated_ms: *accumulated_ms,
                            last_periodic_tick: now_tick,
                        };
                    } else {
                        self.state = TimerState::Idle;
                    }
                }
                _ => {}
            },
        }
    }

    /// 主循环每秒调用。检查 periodic flush，返回累积的 duration event
    pub fn tick(&mut self, data_dir: &Option<std::path::PathBuf>, queue: &MessageQueue) {
        let now = SystemTime::now();
        let now_tick = get_tick_ms();

        match &mut self.state {
            TimerState::Tracking {
                app,
                window,
                start_time,
                start_tick,
                accumulated_ms,
                last_periodic_tick,
            } => {
                // GetTickCount64 在睡眠时不 tick，但 periodic 检测需要的是"实际运行时间"，
                // 所以直接用 tick 差值判断 60 秒周期
                if now_tick >= *last_periodic_tick && now_tick - *last_periodic_tick >= 60_000 {
                    let flush = Self::compute_flush(
                        *start_time,
                        *start_tick,
                        now,
                        now_tick,
                        FlushKind::Periodic,
                        accumulated_ms,
                        app,
                    );
                    *start_time = now;
                    *start_tick = now_tick;
                    *last_periodic_tick = now_tick;

                    if let Some(event) = &flush {
                        info!(
                            target: "app_timer",
                            "Periodic flush | [{}] {} (title: {}) | duration: {}s",
                            event.app.app_type, event.app.process, window.title,
                            event.duration_secs
                        );
                        send_duration_event(queue, event);
                    }

                    // 写检查点
                    if let Some(dir) = data_dir {
                        let since_ts = now
                            .duration_since(UNIX_EPOCH)
                            .unwrap_or_default()
                            .as_secs() as i64;
                        let cp = SessionCheckpoint {
                            process: app.process.clone(),
                            exe_path: app.executable_path.clone(),
                            icon_path: app.icon_path.clone(),
                            desc: app.description.clone(),
                            since_ts,
                        };
                        write_checkpoint(dir, &cp);
                    }
                }
            }
            TimerState::Idle => {
                // 不活跃时删除旧的 checkpoint，防止崩溃恢复时上报虚假会话
                clear_checkpoint(data_dir);
            }
            TimerState::Suspended { .. } => {
                // 睡眠时也删除 checkpoint，原因同上
                clear_checkpoint(data_dir);
            }
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
    ) -> Option<AppDurationEvent> {
        let duration_ms = if now_tick >= start_tick {
            (now_tick - start_tick) as i64
        } else {
            0
        };
        if duration_ms <= 0 {
            return None;
        }

        const MAX_FLUSH_DURATION_MS: i64 = 3600_000;
        let duration_ms = if duration_ms > MAX_FLUSH_DURATION_MS {
            warn!(
                target: "app_timer",
                "Flush duration {}ms exceeds max {}ms for {}, truncating",
                duration_ms,
                MAX_FLUSH_DURATION_MS,
                app.process
            );
            MAX_FLUSH_DURATION_MS
        } else {
            duration_ms
        };

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
