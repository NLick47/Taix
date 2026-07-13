pub mod audio;
pub mod gamepad;
pub mod power_watcher;
pub mod process;
pub mod single_instance;
pub mod window;

use std::time::Duration;
use windows::Win32::UI::Input::KeyboardAndMouse::{GetLastInputInfo, LASTINPUTINFO};

#[link(name = "kernel32")]
extern "system" {
    fn GetTickCount64() -> u64;
}

pub fn get_tick_ms() -> u64 {
    unsafe { GetTickCount64() }
}


pub fn get_system_idle_time() -> Option<Duration> {
    unsafe {
        let mut info = LASTINPUTINFO {
            cbSize: std::mem::size_of::<LASTINPUTINFO>() as u32,
            dwTime: 0,
        };
        if GetLastInputInfo(&mut info).as_bool() {
            let now = GetTickCount64();
            let last_input_32 = info.dwTime as u64;
            let now_low_32 = now & 0xFFFF_FFFF;
            let last_input_64 = if now_low_32 >= last_input_32 {
                (now & !0xFFFF_FFFF) | last_input_32
            } else {
                ((now & !0xFFFF_FFFF) + 0x1_0000_0000) | last_input_32
            };
            let idle_ms = now.saturating_sub(last_input_64);
            Some(Duration::from_millis(idle_ms))
        } else {
            None
        }
    }
}
