pub mod audio;
pub mod process;
pub mod single_instance;
pub mod window;

use std::time::Duration;
use windows::Win32::System::SystemInformation::GetTickCount;
use windows::Win32::UI::Input::KeyboardAndMouse::{GetLastInputInfo, LASTINPUTINFO};

/// 拿到系统空闲时间，就是距离上次用户输入多久了
pub fn get_system_idle_time() -> Duration {
    unsafe {
        let mut info = LASTINPUTINFO {
            cbSize: std::mem::size_of::<LASTINPUTINFO>() as u32,
            dwTime: 0,
        };
        if GetLastInputInfo(&mut info).as_bool() {
            // GetTickCount 与 dwTime 同为 u32，wrapping_sub 即可正确处理回绕，
            // 避免 GetTickCount64 截断低 32 位时可能产生的 race
            let now = GetTickCount();
            let idle_ms = now.wrapping_sub(info.dwTime) as u64;
            Duration::from_millis(idle_ms)
        } else {
            Duration::ZERO
        }
    }
}
