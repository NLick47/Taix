use std::time::{Duration, Instant};
use tracing::debug;
use windows::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryW};
use windows::core::w;

type XInputGetStateFn = unsafe extern "system" fn(u32, *mut XINPUT_STATE) -> u32;

#[repr(C)]
#[derive(Clone, Copy)]
#[allow(non_snake_case)]
struct XINPUT_GAMEPAD {
    pub wButtons: u16,
    pub bLeftTrigger: u8,
    pub bRightTrigger: u8,
    pub sThumbLX: i16,
    pub sThumbLY: i16,
    pub sThumbRX: i16,
    pub sThumbRY: i16,
}

#[repr(C)]
#[allow(non_snake_case)]
struct XINPUT_STATE {
    pub dwPacketNumber: u32,
    pub Gamepad: XINPUT_GAMEPAD,
}

const ERROR_SUCCESS: u32 = 0;
const ERROR_DEVICE_NOT_CONNECTED: u32 = 1167; // 0x48F
/// 未连接槽位的探测间隔，XInput 扫描断开设备并不便宜
const DISCONNECTED_RETRY: Duration = Duration::from_secs(10);

pub struct GamepadState {
    xinput_get_state: Option<XInputGetStateFn>,
    last_packet: [u32; 4],
    next_probe: [Instant; 4],
}

// HMODULE 内部是裸指针，但 GamepadState 只在本线程使用，
// 跨越 await 时需要 Send。
unsafe impl Send for GamepadState {}

impl GamepadState {
    pub fn new() -> Self {
        unsafe {
            let dll = LoadLibraryW(w!("xinput1_4.dll"))
                .ok()
                .or_else(|| LoadLibraryW(w!("xinput1_3.dll")).ok());

            let xinput_get_state = dll.and_then(|handle| {
                let proc = GetProcAddress(handle, windows::core::s!("XInputGetState"))?;
                Some(std::mem::transmute::<_, XInputGetStateFn>(proc))
            });

            if xinput_get_state.is_some() {
                debug!(target: "gamepad", "XInput loaded successfully");
            } else {
                debug!(target: "gamepad", "XInput not available, gamepad detection disabled");
            }

            Self {
                xinput_get_state,
                last_packet: [0; 4],
                next_probe: [Instant::now(); 4],
            }
        }
    }

    pub fn is_active(&mut self) -> bool {
        let Some(xinput_get_state) = self.xinput_get_state else {
            return false;
        };

        let now = Instant::now();
        let mut active = false;
        for i in 0..4u32 {
            let idx = i as usize;
            if self.next_probe[idx] > now {
                continue;
            }
            unsafe {
                let mut state: XINPUT_STATE = std::mem::zeroed();
                let result = xinput_get_state(i, &mut state);
                match result {
                    ERROR_SUCCESS => {
                        self.next_probe[idx] = now;
                        if state.dwPacketNumber != self.last_packet[idx] {
                            debug!(
                                target: "gamepad",
                                "Gamepad {} active (packet: {} -> {})",
                                i,
                                self.last_packet[idx],
                                state.dwPacketNumber
                            );
                            self.last_packet[idx] = state.dwPacketNumber;
                            active = true;
                        }
                    }
                    ERROR_DEVICE_NOT_CONNECTED => {
                        self.last_packet[idx] = 0;
                        self.next_probe[idx] = now + DISCONNECTED_RETRY;
                    }
                    _ => {
                        self.next_probe[idx] = now;
                    }
                }
            }
        }
        active
    }
}
