use crate::models::{MonitorConfig, SleepStatus};
use crate::win32::audio::AudioState;
use crate::win32::gamepad::GamepadState;
use crate::win32::get_system_idle_time;
use crate::win32::power_watcher::{PowerEvent, PowerWatcher};
use std::time::{Duration, Instant};
use tracing::info;

/// 恢复后判定用户活跃的 idle 阈值
const RESUME_IDLE_THRESHOLD: Duration = Duration::from_secs(30);
/// idle 单次检测跳变超过此值视为系统从休眠/锁定恢复
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
    /// 等待用户活动或恢复事件
    /// from_event=true: Lock/Suspend 进入，只等 Unlock/Resume 事件才恢复
    /// from_event=false: idle跳变/系统恢复 进入，等用户活动(idle<30s)才恢复
    ResumePending {
        from_event: bool,
        _last_idle: Duration,
    },
}

pub struct SleepDetector {
    state: DetectorState,
    current: SleepStatus,
    audio_state: AudioState,
    gamepad_state: GamepadState,
    power_watcher: Option<PowerWatcher>,
    tick_count: u32,
    last_valid_idle: Duration,
    idle_failures: u32,
    inactive_threshold: Duration,
    max_sound_duration: Duration,
    sleep_watch: bool,
}

impl SleepDetector {
    pub fn new(audio_state: AudioState, config: &MonitorConfig) -> Self {
        let power_watcher = if config.sleep_watch {
            Some(PowerWatcher::start())
        } else {
            None
        };

        Self {
            state: DetectorState::Initial,
            current: SleepStatus::Wake,
            audio_state,
            gamepad_state: GamepadState::new(),
            power_watcher,
            tick_count: 0,
            last_valid_idle: Duration::ZERO,
            idle_failures: 0,
            inactive_threshold: Duration::from_secs(config.inactive_threshold_secs),
            max_sound_duration: Duration::from_secs(config.max_sound_duration_secs),
            sleep_watch: config.sleep_watch,
        }
    }

    /// 主循环每秒调用
    pub fn tick(&mut self) -> Option<SleepStatus> {
        if !self.sleep_watch {
            return None;
        }

        // 1. 优先处理电源事件 (只取一个，避免丢事件)
        if let Some(status) = self.process_power_event() {
            return Some(status);
        }

        // 2. ResumePending 状态下，即使不到 5 秒也要做 idle 检测
        if matches!(self.state, DetectorState::ResumePending { .. }) {
            self.tick_count = SLEEP_CHECK_TICKS;
        }

        // 3. 轮询 idle 检测 (每 5 秒一次)
        self.tick_count += 1;
        if self.tick_count < SLEEP_CHECK_TICKS {
            return None;
        }
        self.tick_count = 0;

        self.poll_idle_detection()
    }

    /// 处理电源事件 (每次只取一个)
    ///
    /// 锁屏 -> 立即 Sleep，timer 停止计时
    /// 解锁 -> 用户已通过身份验证，立即 Wake
    /// 挂起 -> 立即 Sleep
    /// 恢复 -> 进入 ResumePending，等用户活动
    fn process_power_event(&mut self) -> Option<SleepStatus> {
        let event = self
            .power_watcher
            .as_ref()
            .and_then(|w| w.try_recv());

        match event {
            Some(PowerEvent::SessionLock) => {
                // 锁屏 -> 立即 Sleep，进入 ResumePending 等待解锁事件
                if self.current != SleepStatus::Sleep {
                    self.current = SleepStatus::Sleep;
                    self.state = DetectorState::ResumePending {
                        from_event: true,
                        _last_idle: Duration::ZERO,
                    };
                    info!(target: "sleep_detector", "Power event: SessionLock -> Sleep");
                    Some(SleepStatus::Sleep)
                } else {
                    None
                }
            }
            Some(PowerEvent::SessionUnlock) => {
                // 解锁 -> 已通过身份验证，立即 Wake
                // 不管当前是在 Monitoring 还是 ResumePending 都恢复
                if self.current == SleepStatus::Sleep {
                    self.current = SleepStatus::Wake;
                    self.state = DetectorState::Monitoring {
                        last_idle: Duration::ZERO,
                        sound_start: None,
                    };
                    info!(target: "sleep_detector", "Power event: SessionUnlock -> Wake");
                    Some(SleepStatus::Wake)
                } else {
                    None
                }
            }
            Some(PowerEvent::Suspend) => {
                // 系统挂起 -> 立即 Sleep，进入 ResumePending 等待恢复事件
                if self.current != SleepStatus::Sleep {
                    self.current = SleepStatus::Sleep;
                    self.state = DetectorState::ResumePending {
                        from_event: true,
                        _last_idle: Duration::ZERO,
                    };
                    info!(target: "sleep_detector", "Power event: Suspend -> Sleep");
                    Some(SleepStatus::Sleep)
                } else {
                    None
                }
            }
            Some(PowerEvent::Resume) => {
                // 系统恢复 -> 进入 ResumePending，等用户活动确认
                if self.current == SleepStatus::Sleep {
                    self.state = DetectorState::ResumePending {
                        from_event: false,
                        _last_idle: Duration::ZERO,
                    };
                    info!(target: "sleep_detector", "Power event: Resume -> ResumePending (waiting for activity)");
                }
                None
            }
            None => None,
        }
    }

    /// 轮询 idle 检测
    fn poll_idle_detection(&mut self) -> Option<SleepStatus> {
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
                        DetectorState::ResumePending {
                            from_event: false,
                            _last_idle: idle,
                        },
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
            DetectorState::ResumePending { from_event, .. } => {
                if from_event {
                    // 事件进入的 ResumePending (Lock/Suspend)：
                    // 只等 Unlock/Resume 事件恢复，idle 检测不能恢复
                    (
                        DetectorState::ResumePending {
                            from_event: true,
                            _last_idle: idle,
                        },
                        SleepStatus::Sleep,
                    )
                } else if idle < RESUME_IDLE_THRESHOLD || is_gamepad_active || is_playing_sound {
                    // 非事件进入的 ResumePending (idle跳变/系统恢复)：
                    // 用户活动确认后恢复
                    info!(
                        target: "sleep_detector",
                        "User activity detected after resume, broadcasting Wake"
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
                            from_event: false,
                            _last_idle: idle,
                        },
                        SleepStatus::Sleep,
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
