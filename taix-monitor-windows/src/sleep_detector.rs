use crate::models::{MonitorConfig, SleepStatus};
use crate::win32::audio::AudioState;
use crate::win32::gamepad::GamepadState;
use crate::win32::get_system_idle_time;
use std::time::{Duration, Instant};
use tracing::info;

/// 恢复后判定用户活跃的 idle 阈值（Duration）
const RESUME_IDLE_THRESHOLD: Duration = Duration::from_secs(30);
/// idle 单次检测跳变超过此值视为系统从休眠/锁定恢复（Duration）
const RESUME_JUMP_THRESHOLD: Duration = Duration::from_secs(60);

/// 每 N 次主循环 tick 检测一次 sleep（约 5 秒）
const SLEEP_CHECK_TICKS: u32 = 5;
/// get_system_idle_time 连续失败超过此次数时主动视为睡眠
const MAX_IDLE_FAILURES: u32 = 3;

enum DetectorState {
    Initial,
    Monitoring {
        last_idle: Duration,
        sound_start: Option<Instant>,
    },
    ResumePending {
        _last_idle: Duration,
    },
}

pub struct SleepDetector {
    state: DetectorState,
    current: SleepStatus,
    audio_state: AudioState,
    gamepad_state: GamepadState,
    tick_count: u32,
    last_valid_idle: Duration,
    idle_failures: u32,
    inactive_threshold: Duration,
    max_sound_duration: Duration,
    sleep_watch: bool,
}

impl SleepDetector {
    pub fn new(audio_state: AudioState, config: &MonitorConfig) -> Self {
        Self {
            state: DetectorState::Initial,
            current: SleepStatus::Wake,
            audio_state,
            gamepad_state: GamepadState::new(),
            tick_count: 0,
            last_valid_idle: Duration::ZERO,
            idle_failures: 0,
            inactive_threshold: Duration::from_secs(config.inactive_threshold_secs),
            max_sound_duration: Duration::from_secs(config.max_sound_duration_secs),
            sleep_watch: config.sleep_watch,
        }
    }

    /// 主循环每秒调用。内部每 SLEEP_CHECK_INTERVAL 秒做一次检测
    pub fn tick(&mut self) -> Option<SleepStatus> {
        // 睡眠检测关闭时，始终返回 Wake
        if !self.sleep_watch {
            return None;
        }
        self.tick_count += 1;
        if self.tick_count < SLEEP_CHECK_TICKS {
            return None;
        }
        self.tick_count = 0;

        if let Some(idle_val) = get_system_idle_time() {
            self.last_valid_idle = idle_val;
            self.idle_failures = 0;
        } else {
            self.idle_failures += 1;
        }
        let idle = self.last_valid_idle;
        let is_playing_sound = self.audio_state.is_playing();
        let is_gamepad_active = self.gamepad_state.is_active();

        info!(
            target: "sleep_detector",
            "tick: idle={:?}, threshold={:?}, sound={}, gamepad={}",
            idle, self.inactive_threshold, is_playing_sound, is_gamepad_active
        );

        // get_system_idle_time 连续多次失败时主动进入睡眠态
        if self.idle_failures >= MAX_IDLE_FAILURES {
            if self.current != SleepStatus::Sleep {
                self.current = SleepStatus::Sleep;
                self.state = DetectorState::Monitoring {
                    last_idle: idle,
                    sound_start: None,
                };
                return Some(SleepStatus::Sleep);
            }
            return None;
        }

        let state = std::mem::replace(&mut self.state, DetectorState::Initial);
        let (next_state, next_status) = self.transition(
            state,
            self.current,
            idle,
            is_playing_sound,
            is_gamepad_active,
        );

        self.state = next_state;

        if next_status != self.current {
            self.current = next_status;
            match next_status {
                SleepStatus::Wake => info!(
                    target: "sleep_detector",
                    "Status changed: Sleep -> Wake (idle={:?})", idle
                ),
                SleepStatus::Sleep => info!(
                    target: "sleep_detector",
                    "Status changed: Wake -> Sleep (idle={:?})", idle
                ),
            }
            Some(next_status)
        } else {
            None
        }
    }

    fn transition(
        &self,
        state: DetectorState,
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        is_gamepad_active: bool,
    ) -> (DetectorState, SleepStatus) {
        match state {
            DetectorState::Initial => {
                let next =
                    self.evaluate_status(current, idle, is_playing_sound, None, is_gamepad_active);
                (
                    DetectorState::Monitoring {
                        last_idle: idle,
                        sound_start: None,
                    },
                    next,
                )
            }
            DetectorState::Monitoring {
                last_idle,
                sound_start,
            } => {
                if current == SleepStatus::Wake && idle > last_idle + RESUME_JUMP_THRESHOLD {
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

                let (next_status, next_sound) = self.evaluate_status_with_sound(
                    current,
                    idle,
                    is_playing_sound,
                    sound_start,
                    is_gamepad_active,
                );
                (
                    DetectorState::Monitoring {
                        last_idle: idle,
                        sound_start: next_sound,
                    },
                    next_status,
                )
            }
            DetectorState::ResumePending { .. } => {
                if idle < RESUME_IDLE_THRESHOLD || is_gamepad_active || is_playing_sound {
                    info!(
                        target: "sleep_detector",
                        "User activity detected, exiting resume cooldown, broadcasting Wake"
                    );
                    let (next_status, next_sound) = self.evaluate_status_with_sound(
                        SleepStatus::Wake,
                        idle,
                        is_playing_sound,
                        None,
                        is_gamepad_active,
                    );
                    (
                        DetectorState::Monitoring {
                            last_idle: idle,
                            sound_start: next_sound,
                        },
                        next_status,
                    )
                } else {
                    (
                        DetectorState::ResumePending {
                            _last_idle: idle,
                        },
                        current,
                    )
                }
            }
        }
    }

    fn evaluate_status(
        &self,
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        sound_start: Option<Instant>,
        is_gamepad_active: bool,
    ) -> SleepStatus {
        self.evaluate_status_with_sound(current, idle, is_playing_sound, sound_start, is_gamepad_active).0
    }

    fn evaluate_status_with_sound(
        &self,
        current: SleepStatus,
        idle: Duration,
        is_playing_sound: bool,
        sound_start: Option<Instant>,
        is_gamepad_active: bool,
    ) -> (SleepStatus, Option<Instant>) {
        let user_active = idle < self.inactive_threshold || is_gamepad_active;

        match current {
            SleepStatus::Sleep if user_active => (SleepStatus::Wake, None),
            SleepStatus::Wake if !user_active => {
                if is_playing_sound {
                    match sound_start {
                        None => (SleepStatus::Wake, Some(Instant::now())),
                        Some(t) if t.elapsed() < self.max_sound_duration => {
                            (SleepStatus::Wake, Some(t))
                        }
                        Some(_) => (SleepStatus::Sleep, None),
                    }
                } else {
                    (SleepStatus::Sleep, None)
                }
            }
            SleepStatus::Wake if user_active => (SleepStatus::Wake, None),
            _ => (current, sound_start),
        }
    }
}
