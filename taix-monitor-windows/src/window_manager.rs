use crate::models::WindowInfo;
use crate::win32::window::{get_window_class_name, get_window_rect, get_window_text};
use tracing::error;
use windows::Win32::Foundation::HWND;

pub struct WindowManager;

impl WindowManager {
    pub fn get_window_info(hwnd: HWND) -> Option<WindowInfo> {
        let title = get_window_text(hwnd);
        if get_window_rect(hwnd).is_none() {
            error!("[WindowManager] GetWindowRect failed for hwnd={:?}", hwnd);
            return None;
        }
        let class_name = get_window_class_name(hwnd);
        Some(WindowInfo {
            class_name,
            title,
            handle: hwnd.0 as isize,
        })
    }
}