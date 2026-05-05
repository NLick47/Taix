use crate::models::SleepStatus;
use crate::win32::audio::AudioState;
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
const HEARTBEAT_INTERVAL: Duration = Duration::from_secs(5);
/// idle 单 tick 增长超过此值视为系统从休眠/锁定恢复
const RESUME_JUMP_THRESHOLD: Duration = Duration::from_secs(60);

#[derive(Clone)]
pub struct SleepDetector {
    inner: Arc<Inner>,
}

struct Inner {
    tx: broadcast::Sender<SleepStatus>,
    shutdown: AtomicBool,
    audio_state: AudioState,
}

impl SleepDetector {
    pub fn new(audio_state: AudioState) -> Self {
        let (tx, _rx) = broadcast::channel(128);
        Self {
            inner: Arc::new(Inner {
                tx,
                shutdown: AtomicBool::new(false),
                audio_state,
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
        let mut last_idle = Duration::ZERO;
        let mut resume_pending = false;
        let mut first_tick_done = false;

        info!("[SleepDetector] Started");

        loop {
            interval.tick().await;

            if self.inner.shutdown.load(Ordering::SeqCst) {
                info!("[SleepDetector] Shutdown requested");
                break;
            }

            let idle = get_system_idle_time();
            let is_playing_sound = self.inner.audio_state.is_playing();

            // 系统恢复检测：idle 在单个 tick 内大幅跳变（远超间隔时间）
            // 注意：需要 first_tick_done 来排除程序启动时的初始化跳变
            if first_tick_done
                && !resume_pending
                && status == SleepStatus::Wake
                && idle > last_idle + RESUME_JUMP_THRESHOLD
            {
                info!(
                    "[SleepDetector] System resume detected (idle jump: {:?} -> {:?}), broadcasting Sleep to flush timer",
                    last_idle, idle
                );
                // 系统从冻结/休眠恢复，补发 Sleep 事件让计时器刷出冻结前的时间
                status = SleepStatus::Sleep;
                self.broadcast_status(status, idle);
                resume_pending = true;
                last_idle = idle;
                continue;
            }

            // 恢复锁定中：不进入 Sleep，等用户操作后补发 Wake
            if resume_pending {
                if idle < INACTIVE_THRESHOLD {
                    info!("[SleepDetector] User activity detected, exiting resume cooldown, broadcasting Wake");
                    resume_pending = false;
                    // 用户恢复活动，补发 Wake 事件让计时器重新开始
                    status = SleepStatus::Wake;
                    self.broadcast_status(status, idle);
                }
                last_idle = idle;
                continue;
            }

            let prev_status = status;
            status = Self::evaluate_status(status, idle, is_playing_sound, &mut sound_start);

            // 仅在状态真正发生切换时才发送广播，避免重复通知
            if status != prev_status {
                self.broadcast_status(status, idle);
            }

            last_idle = idle;
            first_tick_done = true;
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
