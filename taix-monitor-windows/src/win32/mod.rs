pub mod audio;
pub mod process;
pub mod single_instance;
pub mod window;

use std::time::Duration;
use windows::Win32::System::SystemInformation::GetTickCount64;
use windows::Win32::UI::Input::KeyboardAndMouse::{GetLastInputInfo, LASTINPUTINFO};

/// 拿到系统空闲时间，就是距离上次用户输入多久了
pub fn get_system_idle_time() -> Duration {
    unsafe {
        let mut info = LASTINPUTINFO {
            cbSize: std::mem::size_of::<LASTINPUTINFO>() as u32,
            dwTime: 0,
        };
        if GetLastInputInfo(&mut info).as_bool() {
            let now = GetTickCount64();
            // dwTime是u32会回绕，取低32位做wrapping_sub才能算对
            let now_low32 = now as u32;
            let idle_ms = now_low32.wrapping_sub(info.dwTime) as u64;
            Duration::from_millis(idle_ms)
        } else {
            Duration::ZERO
        }
    }
}
