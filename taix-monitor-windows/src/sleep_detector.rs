use crate::models::SleepStatus;
use crate::win32::audio::is_windows_playing_sound;
use crate::win32::get_system_idle_time;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::Arc;
use std::time::Duration;
use tokio::sync::broadcast;
use tracing::{debug, info};

#[cfg(debug_assertions)]
const INACTIVE_THRESHOLD: Duration = Duration::from_secs(10);
#[cfg(not(debug_assertions))]
const INACTIVE_THRESHOLD: Duration = Duration::from_secs(300);
const MAX_SOUND_DURATION: Duration = Duration::from_secs(7200);
const HEARTBEAT_INTERVAL: Duration = Duration::from_secs(1);

#[derive(Clone)]
pub struct SleepDetector {
    inner: Arc<Inner>,
}

struct Inner {
    tx: broadcast::Sender<SleepStatus>,
    shutdown: AtomicBool,
}

impl SleepDetector {
    pub fn new() -> Self {
        let (tx, _rx) = broadcast::channel(4);
        Self {
            inner: Arc::new(Inner {
                tx,
                shutdown: AtomicBool::new(false),
            }),
        }
    }

    pub fn subscribe(&self) -> broadcast::Receiver<SleepStatus> {
        self.inner.tx.subscribe()
    }

    pub fn shutdown(&self) {
        self.inner.shutdown.store(true, Ordering::SeqCst);
    }

    pub async fn run(self) {
        let mut interval = tokio::time::interval(HEARTBEAT_INTERVAL);
        let mut status = SleepStatus::Wake;
        let mut sound_start: Option<std::time::Instant> = None;

        info!("[SleepDetector] Started");

        loop {
            interval.tick().await;

            if self.inner.shutdown.load(Ordering::SeqCst) {
                info!("[SleepDetector] Shutdown requested");
                break;
            }

            let idle = get_system_idle_time();
            let is_playing_sound = is_windows_playing_sound();

            let prev_status = status;
            status = Self::evaluate_status(status, idle, is_playing_sound, &mut sound_start);

            // 仅在状态真正发生切换时才发送广播，避免重复通知
            if status != prev_status {
                self.broadcast_status(status, idle);
            }
        }
    }

    /// 算下一个状态。只有 Wake 且没人碰电脑时才计声音超时，一动就重置
    fn evaluate_status(
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        sound_start: &mut Option<std::time::Instant>,
    ) -> SleepStatus {
        match current {
            SleepStatus::Sleep if idle < INACTIVE_THRESHOLD => {
                *sound_start = None;
                SleepStatus::Wake
            }
            SleepStatus::Wake if idle >= INACTIVE_THRESHOLD => {
                if is_playing_sound {
                    match *sound_start {
                        None => {
                            *sound_start = Some(std::time::Instant::now());
                            SleepStatus::Wake
                        }
                        Some(t) if t.elapsed() < MAX_SOUND_DURATION => SleepStatus::Wake,
                        _ => {
                            *sound_start = None;
                            SleepStatus::Sleep
                        }
                    }
                } else {
                    *sound_start = None;
                    SleepStatus::Sleep
                }
            }
            SleepStatus::Wake if idle < INACTIVE_THRESHOLD => {
                // 用户已恢复活动，重置声音计时器
                if sound_start.is_some() {
                    *sound_start = None;
                }
                SleepStatus::Wake
            }
            // 其余情况保持当前状态不变（如 Sleep + idle >= 阈值时不重复发送 Sleep）
            _ => current,
        }
    }

    fn broadcast_status(&self, status: SleepStatus, idle: Duration) {
        let result = self.inner.tx.send(status);
        match result {
            Ok(n) => {
                if n > 0 {
                    match status {
                        SleepStatus::Wake => info!(
                            "[SleepDetector] Status changed: Sleep -> Wake (idle={:?}), broadcast to {} subscribers",
                            idle, n
                        ),
                        SleepStatus::Sleep => info!(
                            "[SleepDetector] Status changed: Wake -> Sleep (idle={:?}), broadcast to {} subscribers",
                            idle, n
                        ),
                    }
                } else {
                    debug!("[SleepDetector] Status changed to {:?} (idle={:?}), no active subscribers", status, idle);
                }
            }
            Err(_) => {
                debug!("[SleepDetector] Status changed to {:?} (idle={:?}), all subscribers dropped", status, idle);
            }
        }
    }
}

impl Default for SleepDetector {
    fn default() -> Self {
        Self::new()
    }
}
