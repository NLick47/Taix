use crate::models::{AppDurationEvent, AppInfo, AppType, SleepStatus, WindowInfo};
use tokio::sync::broadcast;
use tracing::info;

pub struct AppTimer {
    rx: broadcast::Receiver<crate::models::AppActiveEvent>,
    sleep_rx: broadcast::Receiver<SleepStatus>,
    tx: broadcast::Sender<AppDurationEvent>,
    state: AppTimerState,
    ignore_list: Vec<String>,
}

struct AppTimerState {
    active_app: AppInfo,
    active_window: WindowInfo,
    start_time: Option<chrono::DateTime<chrono::Local>>,
    last_reported_start: Option<chrono::DateTime<chrono::Local>>,
    last_reported_end: Option<chrono::DateTime<chrono::Local>>,
    accumulated_ms: i64,
}

impl Default for AppTimerState {
    fn default() -> Self {
        Self {
            active_app: AppInfo::empty(),
            active_window: WindowInfo::empty(),
            start_time: None,
            last_reported_start: None,
            last_reported_end: None,
            accumulated_ms: 0,
        }
    }
}

impl AppTimer {
    pub fn new(
        event_rx: broadcast::Receiver<crate::models::AppActiveEvent>,
        sleep_rx: broadcast::Receiver<SleepStatus>,
        ignore_list: Vec<String>,
    ) -> Self {
        let (tx, _rx) = broadcast::channel(256);
        Self {
            rx: event_rx,
            sleep_rx,
            tx,
            state: AppTimerState::default(),
            ignore_list,
        }
    }

    pub fn subscribe(&self) -> broadcast::Receiver<AppDurationEvent> {
        self.tx.subscribe()
    }

    pub async fn run(mut self) {
        let mut rx = self.rx;
        let mut sleep_rx = self.sleep_rx;
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

                                // 毫秒零头归前 app，避免时间归属错误
                                self.state.accumulated_ms = 0;

                                self.state.start_time = if is_statistical { Some(now) } else { None };
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
                                }
                                SleepStatus::Wake => {
                                    if !self.state.active_app.is_empty() && self.state.start_time.is_none() {
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
    if active.is_empty() {
        return None;
    }

    let duration_ms = (end_time - start).num_milliseconds();
    if duration_ms <= 0 {
        return None;
    }

    let total_ms = duration_ms + state.accumulated_ms;
    let mut duration_secs = total_ms / 1000;
    state.accumulated_ms = total_ms % 1000;

    // 强制刷盘时不足一秒进位
    if duration_secs <= 0 && force_flush && state.accumulated_ms > 0 {
        duration_secs = 1;
        state.accumulated_ms = 0;
    }

    if duration_secs <= 0 {
        return None;
    }

    if state.last_reported_start == Some(start) && state.last_reported_end == Some(end_time) {
        return None;
    }

    state.last_reported_start = Some(start);
    state.last_reported_end = Some(end_time);

    Some(AppDurationEvent {
        duration_secs,
        app: active.clone(),
        start_time: start,
    })
}