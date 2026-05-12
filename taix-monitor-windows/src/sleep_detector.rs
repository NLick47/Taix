use crate::models::SleepStatus;
use crate::win32::audio::AudioState;
use crate::win32::gamepad::GamepadState;
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

enum DetectorState {
    /// 首次 tick，跳过恢复检测。
    Initial,
    /// 正常监测中。
    Monitoring {
        last_idle: Duration,
        sound_start: Option<std::time::Instant>,
    },
    /// 系统从冻结/休眠恢复后，等待用户操作确认。
    ResumePending { _last_idle: Duration },
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
        interval.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);

        let mut state = DetectorState::Initial;
        let mut current = SleepStatus::Wake;
        let mut gamepad_state = GamepadState::new();

        info!(target: "sleep_detector", "Started");

        loop {
            interval.tick().await;

            if self.inner.shutdown.load(Ordering::SeqCst) {
                info!(target: "sleep_detector", "Shutdown requested");
                break;
            }

            let idle = get_system_idle_time();
            let is_playing_sound = self.inner.audio_state.is_playing();
            let is_gamepad_active = gamepad_state.is_active();

            let (next_state, next_status) = Self::transition(
                state,
                current,
                idle,
                is_playing_sound,
                is_gamepad_active,
            );

            state = next_state;

            if next_status != current {
                current = next_status;
                self.broadcast_status(current, idle);
            }
        }
    }

    /// 纯函数状态转换：给定当前状态、idle、声音状态，返回下一状态和应切换到的 SleepStatus。
    fn transition(
        state: DetectorState,
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        is_gamepad_active: bool,
    ) -> (DetectorState, SleepStatus) {
        match state {
            DetectorState::Initial => {
                // 首次 tick 只记录 idle，不检测恢复跳变
                let next = Self::evaluate_status(current, idle, is_playing_sound, None, is_gamepad_active);
                (
                    DetectorState::Monitoring {
                        last_idle: idle,
                        sound_start: None,
                    },
                    next,
                )
            }
            DetectorState::Monitoring { last_idle, sound_start } => {
                // 恢复检测：idle 在单个 tick 内大幅跳变
                if current == SleepStatus::Wake
                    && idle > last_idle + RESUME_JUMP_THRESHOLD
                {
                    info!(
                        target: "sleep_detector",
                        "System resume detected (idle jump: {:?} -> {:?}), broadcasting Sleep to flush timer",
                        last_idle, idle
                    );
                    return (
                        DetectorState::ResumePending { _last_idle: idle },
                        SleepStatus::Sleep,
                    );
                }

                let (next_status, next_sound) =
                    Self::evaluate_status_with_sound(current, idle, is_playing_sound, sound_start, is_gamepad_active);
                (
                    DetectorState::Monitoring {
                        last_idle: idle,
                        sound_start: next_sound,
                    },
                    next_status,
                )
            }
            DetectorState::ResumePending { _last_idle: _ } => {
                if idle < INACTIVE_THRESHOLD || is_gamepad_active {
                    info!(
                        target: "sleep_detector",
                        "User activity detected, exiting resume cooldown, broadcasting Wake"
                    );
                    let (next_status, next_sound) =
                        Self::evaluate_status_with_sound(SleepStatus::Wake, idle, is_playing_sound, None, is_gamepad_active);
                    (
                        DetectorState::Monitoring {
                            last_idle: idle,
                            sound_start: next_sound,
                        },
                        next_status,
                    )
                } else {
                    (
                        DetectorState::ResumePending { _last_idle: idle },
                        current,
                    )
                }
            }
        }
    }

    /// 计算下一个 SleepStatus，不考虑恢复跳变。
    fn evaluate_status(
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        sound_start: Option<std::time::Instant>,
        is_gamepad_active: bool,
    ) -> SleepStatus {
        Self::evaluate_status_with_sound(current, idle, is_playing_sound, sound_start, is_gamepad_active).0
    }

    fn evaluate_status_with_sound(
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        sound_start: Option<std::time::Instant>,
        is_gamepad_active: bool,
    ) -> (SleepStatus, Option<std::time::Instant>) {
        let user_active = idle < INACTIVE_THRESHOLD || is_gamepad_active;

        match current {
            SleepStatus::Sleep if user_active => {
                (SleepStatus::Wake, None)
            }
            SleepStatus::Wake if !user_active => {
                if is_playing_sound {
                    match sound_start {
                        None => (SleepStatus::Wake, Some(std::time::Instant::now())),
                        Some(t) if t.elapsed() < MAX_SOUND_DURATION => {
                            (SleepStatus::Wake, Some(t))
                        }
                        Some(_) => (SleepStatus::Sleep, None),
                    }
                } else {
                    (SleepStatus::Sleep, None)
                }
            }
            SleepStatus::Wake if user_active => {
                (SleepStatus::Wake, None)
            }
            _ => (current, sound_start),
        }
    }

    fn broadcast_status(&self, status: SleepStatus, idle: Duration) {
        let result = self.inner.tx.send(status);
        match result {
            Ok(n) => {
                if n > 0 {
                    match status {
                        SleepStatus::Wake => info!(
                            target: "sleep_detector",
                            "Status changed: Sleep -> Wake (idle={:?}), broadcast to {} subscribers",
                            idle, n
                        ),
                        SleepStatus::Sleep => info!(
                            target: "sleep_detector",
                            "Status changed: Wake -> Sleep (idle={:?}), broadcast to {} subscribers",
                            idle, n
                        ),
                    }
                } else {
                    debug!(target: "sleep_detector", "Status changed to {:?} (idle={:?}), no active subscribers", status, idle);
                }
            }
            Err(_) => {
                debug!(target: "sleep_detector", "Status changed to {:?} (idle={:?}), all subscribers dropped", status, idle);
            }
        }
    }
}
