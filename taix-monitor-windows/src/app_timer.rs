use crate::models::{AppDurationEvent, AppInfo, AppType, SessionCheckpoint, SleepStatus, WindowInfo};
use std::path::PathBuf;
use std::time::Duration;
use tokio::sync::broadcast;
use tracing::info;

enum FlushKind {
    /// 60 秒周期刷盘，保留毫秒零头继续累积
    Periodic,
    /// 应用切换、睡眠或关机时的最终刷盘，零头必须处理掉
    Final,
}

/// 计时器核心状态机
enum TimerState {
    /// 没有活跃应用在计时
    Idle,
    /// 正在跟踪某个应用的活跃时间
    Tracking {
        app: AppInfo,
        window: WindowInfo,
        start: chrono::DateTime<chrono::Local>,
        /// 跨周期累积的毫秒零头
        accumulated_ms: i64,
    },
    /// 系统处于睡眠/锁定状态，计时暂停
    Suspended {
        /// 睡眠前正在跟踪的应用
        app: Option<AppInfo>,
        window: Option<WindowInfo>,
        /// 睡眠前累积的毫秒零头
        accumulated_ms: i64,
    },
}

pub struct AppTimer {
    rx: broadcast::Receiver<crate::models::AppActiveEvent>,
    sleep_rx: broadcast::Receiver<SleepStatus>,
    tx: broadcast::Sender<AppDurationEvent>,
    state: TimerState,
    data_dir: Option<PathBuf>,
}

impl AppTimer {
    pub fn new(
        event_rx: broadcast::Receiver<crate::models::AppActiveEvent>,
        sleep_rx: broadcast::Receiver<SleepStatus>,
        data_dir: Option<PathBuf>,
    ) -> Self {
        let (tx, _rx) = broadcast::channel(512);
        Self {
            rx: event_rx,
            sleep_rx,
            tx,
            state: TimerState::Idle,
            data_dir,
        }
    }

    pub fn subscribe(&self) -> broadcast::Receiver<AppDurationEvent> {
        self.tx.subscribe()
    }

    pub async fn run(mut self) {
        let mut periodic_tick = tokio::time::interval(Duration::from_secs(60));
        periodic_tick.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
        periodic_tick.reset();
        info!(target: "app_timer", "Started");

        loop {
            tokio::select! {
                result = self.rx.recv() => {
                    match result {
                        Ok(event) => self.on_app_event(event).await,
                        Err(broadcast::error::RecvError::Lagged(n)) => {
                            tracing::warn!(target: "app_timer", "App events lagged by {} messages", n);
                        }
                        Err(broadcast::error::RecvError::Closed) => break,
                    }
                }
                result = self.sleep_rx.recv() => {
                    match result {
                        Ok(status) => self.on_sleep_event(status).await,
                        Err(broadcast::error::RecvError::Lagged(n)) => {
                            tracing::warn!(target: "app_timer", "Sleep events lagged by {} messages", n);
                        }
                        Err(broadcast::error::RecvError::Closed) => break,
                    }
                }
                _ = periodic_tick.tick() => {
                    self.on_periodic_tick().await;
                }
            }
        }

        // 退出时最终刷盘
        if let Some(event) = self.shutdown_flush(chrono::Local::now()) {
            info!(target: "app_timer",
                "Shutdown flush | last: [{}] {} (title: {}) | duration: {}s",
                event.app.app_type, event.app.process,
                self.window_title_for_log(),
                event.duration_secs
            );
            if let Err(e) = self.tx.send(event) {
                tracing::warn!(target: "app_timer", "Failed to broadcast final duration: {}", e);
            }
        }

        info!(target: "app_timer", "Stopped");
    }

    /// 判断一个应用是否应该被统计
    fn is_statistical(&self, app: &AppInfo) -> bool {
        app.app_type != AppType::SystemComponent
            && !app.executable_path.is_empty()
    }

    fn window_title_for_log(&self) -> &str {
        match &self.state {
            TimerState::Tracking { window, .. } => &window.title,
            _ => "",
        }
    }

    async fn on_app_event(&mut self, event: crate::models::AppActiveEvent) {
        let now = chrono::Local::now();
        let is_statistical = self.is_statistical(&event.app);

        match &mut self.state {
            TimerState::Suspended { app, window, accumulated_ms: _ } => {
                // 睡眠期间也更新前台应用记录，唤醒时能从正确状态恢复
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
                    info!(target: "app_timer",
                        "App active: [{}] {} (title: {})",
                        event.app.app_type, event.app.process, event.window.title
                    );
                    self.state = TimerState::Tracking {
                        app: event.app,
                        window: event.window,
                        start: now,
                        accumulated_ms: 0,
                    };
                }
                return;
            }
            TimerState::Tracking { app, window, start, accumulated_ms } => {
                let identity_changed = event.app.process != app.process
                    || event.app.executable_path != app.executable_path;

                if identity_changed {
                    // 应用切换：flush 上一个应用
                    let start = *start;
                    if let Some(duration_event) =
                        Self::compute_flush(start, now, FlushKind::Final, accumulated_ms, app)
                    {
                        info!(target: "app_timer",
                            "App switched | previous: [{}] {} (title: {}) | duration: {}s | new: [{}] {} (title: {})",
                            duration_event.app.app_type, duration_event.app.process, window.title,
                            duration_event.duration_secs,
                            event.app.app_type, event.app.process, event.window.title
                        );
                        if let Err(e) = self.tx.send(duration_event) {
                            tracing::warn!(target: "app_timer", "Failed to broadcast duration: {}", e);
                        }
                    } else {
                        info!(target: "app_timer",
                            "App active: [{}] {} (title: {})",
                            event.app.app_type, event.app.process, event.window.title
                        );
                    }

                    if is_statistical {
                        self.state = TimerState::Tracking {
                            app: event.app,
                            window: event.window,
                            start: now,
                            accumulated_ms: 0,
                        };
                    } else {
                        self.state = TimerState::Idle;
                    }
                } else {
                    // 同一应用，仅更新窗口信息
                    *window = event.window;
                }
            }
        }
    }

    async fn on_sleep_event(&mut self, status: SleepStatus) {
        let now = chrono::Local::now();
        match status {
            SleepStatus::Sleep => {
                match &mut self.state {
                    TimerState::Tracking { app, window, start, accumulated_ms } => {
                        let start = *start;
                        if let Some(event) =
                            Self::compute_flush(start, now, FlushKind::Final, accumulated_ms, app)
                        {
                            info!(target: "app_timer",
                                "System sleep | flushed: [{}] {} (title: {}) | duration: {}s",
                                event.app.app_type, event.app.process, window.title,
                                event.duration_secs
                            );
                            if let Err(e) = self.tx.send(event) {
                                tracing::warn!(target: "app_timer", "Failed to broadcast sleep duration: {}", e);
                            }
                        }
                        let app = Some(std::mem::replace(app, AppInfo {
                            process: String::new(),
                            description: String::new(),
                            executable_path: String::new(),
                            icon_path: String::new(),
                            app_type: AppType::Win32,
                        }));
                        let window = Some(std::mem::replace(window, WindowInfo::new(String::new(), String::new(), 0)));
                        self.state = TimerState::Suspended { app, window, accumulated_ms: 0 };
                    }
                    TimerState::Idle => {
                        self.state = TimerState::Suspended { app: None, window: None, accumulated_ms: 0 };
                    }
                    TimerState::Suspended { .. } => {}
                }
                info!(target: "app_timer", "Entered sleep mode, timer paused");
            }
            SleepStatus::Wake => {
                match &mut self.state {
                    TimerState::Suspended { app, window, accumulated_ms } => {
                        info!(target: "app_timer", "Exited sleep mode, timer resuming");
                        if let Some(app) = app.take() {
                            let window = window.take().unwrap_or_else(|| WindowInfo::new(String::new(), String::new(), 0));
                            info!(target: "app_timer",
                                "System wake | resumed: [{}] {} (title: {})",
                                app.app_type, app.process, window.title
                            );
                            self.state = TimerState::Tracking {
                                app,
                                window,
                                start: chrono::Local::now(),
                                accumulated_ms: *accumulated_ms,
                            };
                        } else {
                            self.state = TimerState::Idle;
                        }
                    }
                    _ => {}
                }
            }
        }
    }

    async fn on_periodic_tick(&mut self) {
        let now = chrono::Local::now();
        match &mut self.state {
            TimerState::Tracking { app, window, start, accumulated_ms } => {
                let start_val = *start;
                if let Some(event) =
                    Self::compute_flush(start_val, now, FlushKind::Periodic, accumulated_ms, app)
                {
                    info!(target: "app_timer",
                        "Periodic flush | [{}] {} (title: {}) | duration: {}s",
                        event.app.app_type, event.app.process, window.title,
                        event.duration_secs
                    );
                    if let Err(e) = self.tx.send(event) {
                        tracing::warn!(target: "app_timer", "Failed to broadcast periodic duration: {}", e);
                    }
                }
                *start = now;
                // 写检查点用于崩溃恢复
                if let Some(dir) = &self.data_dir {
                    let cp = SessionCheckpoint {
                        process: app.process.clone(),
                        exe_path: app.executable_path.clone(),
                        icon_path: app.icon_path.clone(),
                        desc: app.description.clone(),
                        since_ts: now.timestamp(),
                    };
                    let path = dir.join("active_session.json");
                    let tmp = dir.join("active_session.json.tmp");
                    match serde_json::to_string(&cp) {
                        Ok(json) => {
                            if let Err(e) = std::fs::write(&tmp, &json) {
                                tracing::warn!(target: "app_timer", "Failed to write checkpoint tmp: {}", e);
                            } else if let Err(e) = std::fs::rename(&tmp, &path) {
                                tracing::warn!(target: "app_timer", "Failed to rename checkpoint: {}", e);
                            }
                        }
                        Err(e) => tracing::warn!(target: "app_timer", "Failed to serialize checkpoint: {}", e),
                    }
                }
            }
            TimerState::Idle => {
                tracing::debug!(target: "app_timer", "Skipping periodic flush | no active app");
            }
            TimerState::Suspended { app, .. } => {
                if let Some(app) = app {
                    tracing::warn!(target: "app_timer", "Skipping periodic flush while sleeping | app={}", app.process);
                } else {
                    tracing::warn!(target: "app_timer", "Skipping periodic flush while sleeping | no active app");
                }
            }
        }
    }

    /// 退出时从当前状态提取一个 duration event。
    fn shutdown_flush(
        &mut self,
        end_time: chrono::DateTime<chrono::Local>,
    ) -> Option<AppDurationEvent> {
        match &mut self.state {
            TimerState::Tracking { app, window: _window, start, accumulated_ms } => {
                Self::compute_flush(*start, end_time, FlushKind::Final, accumulated_ms, app)
            }
            _ => None,
        }
    }

    /// 纯函数：给定 start/end/kind/accumulated，计算应发送的 duration event。
    /// 不依赖 self，避免与 mutable borrow 冲突。
    fn compute_flush(
        start: chrono::DateTime<chrono::Local>,
        end_time: chrono::DateTime<chrono::Local>,
        kind: FlushKind,
        accumulated_ms: &mut i64,
        app: &AppInfo,
    ) -> Option<AppDurationEvent> {
        let duration_ms = (end_time - start).num_milliseconds();
        if duration_ms <= 0 {
            return None;
        }

        const MAX_FLUSH_DURATION_MS: i64 = 3600_000;
        let duration_ms = if duration_ms > MAX_FLUSH_DURATION_MS {
            tracing::warn!(target: "app_timer",
                "Flush duration {}ms exceeds max {}ms for {}, truncating",
                duration_ms, MAX_FLUSH_DURATION_MS, app.process
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
                // 周期刷盘保留零头继续累积
                *accumulated_ms = remainder;
            }
            FlushKind::Final => {
                // 最终刷盘必须把零头处理掉
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
            start_time: start,
        })
    }
}
