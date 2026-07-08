pub mod audio;
pub mod gamepad;
pub mod process;
pub mod single_instance;
pub mod window;

use std::time::Duration;
use windows::Win32::UI::Input::KeyboardAndMouse::{GetLastInputInfo, LASTINPUTINFO};

// 获取系统启动以来的毫秒数，在系统睡眠期间不 tick。
// 用于替代 `Instant::now()`，避免 Windows 上 QPC 穿透睡眠的问题。
// 底层使用 `GetTickCount64`，精度 10-16ms，对我们秒级统计足够。
#[link(name = "kernel32")]
extern "system" {
    fn GetTickCount64() -> u64;
}

pub fn get_tick_ms() -> u64 {
    unsafe { GetTickCount64() }
}

/// 拿到系统空闲时间，就是距离上次用户输入多久了。
/// 底层使用 GetTickCount64 计算差值，避免 GetTickCount 的 32 位回绕问题。
pub fn get_system_idle_time() -> Option<Duration> {
    unsafe {
        let mut info = LASTINPUTINFO {
            cbSize: std::mem::size_of::<LASTINPUTINFO>() as u32,
            dwTime: 0,
        };
        if GetLastInputInfo(&mut info).as_bool() {
            let now = GetTickCount64();
            // LASTINPUTINFO.dwTime 是 DWORD（32 位），需要用 wrapping_sub 处理回绕
            // 但 GetTickCount64 是 64 位，直接减即可
            let last_input_ms = info.dwTime as u64;
            // 处理 32 位回绕：如果 last_input_ms > now，说明 last_input 记录在回绕前
            let idle_ms = if now >= last_input_ms {
                now - last_input_ms
            } else {
                // last_input 在 32 位回绕前，now 在回绕后
                // 实际差值 = (u32::MAX - last_input_ms) + now
                (u64::from(u32::MAX) - last_input_ms) + now
            };
            Some(Duration::from_millis(idle_ms))
        } else {
            None
        }
    }
}
