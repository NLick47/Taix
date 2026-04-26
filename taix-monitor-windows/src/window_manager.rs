use crate::models::WindowInfo;
use crate::win32::window::{get_window_class_name, get_window_rect, get_window_text};
use tracing::error;
use windows::Win32::Foundation::HWND;

pub struct WindowManager;

impl WindowManager {
    pub fn new() -> Self {
        Self
    }

    pub fn get_window_info(&self, hwnd: HWND) -> WindowInfo {
        let title = get_window_text(hwnd);
        let rect = match get_window_rect(hwnd) {
            Some(r) => r,
            None => {
                error!("[WindowManager] GetWindowRect failed for hwnd={:?}", hwnd);
                return WindowInfo::empty();
            }
        };
        let class_name = get_window_class_name(hwnd);
        WindowInfo {
            class_name,
            title,
            handle: hwnd.0 as isize,
            width: rect.right - rect.left,
            height: rect.bottom - rect.top,
            x: rect.left,
            y: rect.top,
        }
    }
}