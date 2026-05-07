use crate::models::WindowInfo;
use std::path::Path;
use windows::Win32::Foundation::{HWND, LPARAM, RECT};
use windows::Win32::Graphics::Gdi::{
    DeleteObject, GetDC, GetDIBits, GetObjectW, ReleaseDC, BITMAPINFO, BITMAPINFOHEADER, BI_RGB,
    DIB_RGB_COLORS, HGDIOBJ,
};
use windows::Win32::UI::Shell::ExtractIconExW;
use windows::Win32::UI::WindowsAndMessaging::{
    DestroyIcon, EnumChildWindows, GetClassNameW, GetForegroundWindow, GetIconInfo, GetWindowRect,
    GetWindowTextLengthW, GetWindowTextW, IsWindow, IsWindowVisible, HICON,
};
use windows::Win32::UI::Accessibility::{SetWinEventHook, UnhookWinEvent};
use windows::core::PCWSTR;

const WINEVENT_OUTOFCONTEXT: u32 = 0x0000;

pub fn get_foreground_window() -> HWND {
    unsafe { GetForegroundWindow() }
}

pub fn is_valid_visible_window(hwnd: HWND) -> bool {
    unsafe { IsWindow(Some(hwnd)).as_bool() && IsWindowVisible(hwnd).as_bool() }
}

pub fn get_window_text(hwnd: HWND) -> String {
    unsafe {
        let len = GetWindowTextLengthW(hwnd);
        if len <= 0 {
            return String::new();
        }
        let mut buf = vec![0u16; (len + 1) as usize];
        let actual = GetWindowTextW(hwnd, &mut buf);
        if actual > 0 {
            let mut s = String::from_utf16_lossy(&buf[..actual as usize]);
            if let Some(pos) = s.find('\0') {
                s.truncate(pos);
            }
            s
        } else {
            String::new()
        }
    }
}

pub fn get_window_class_name(hwnd: HWND) -> String {
    unsafe {
        let mut buf = vec![0u16; 256];
        let len = GetClassNameW(hwnd, &mut buf);
        if len > 0 {
            let mut s = String::from_utf16_lossy(&buf[..len as usize]);
            if let Some(pos) = s.find('\0') {
                s.truncate(pos);
            }
            s
        } else {
            String::new()
        }
    }
}

pub fn get_window_rect(hwnd: HWND) -> Option<RECT> {
    unsafe {
        let mut rect = RECT::default();
        GetWindowRect(hwnd, &mut rect).ok()?;
        Some(rect)
    }
}

pub fn get_window_thread_process_id(hwnd: HWND) -> (u32, u32) {
    unsafe {
        let mut pid = 0u32;
        let tid = windows::Win32::UI::WindowsAndMessaging::GetWindowThreadProcessId(hwnd, Some(&mut pid));
        (tid, pid)
    }
}

pub fn get_window_info(hwnd: HWND) -> Option<WindowInfo> {
    let title = get_window_text(hwnd);
    if get_window_rect(hwnd).is_none() {
        tracing::error!(target: "win32::window", "GetWindowRect failed for hwnd={:?}", hwnd);
        return None;
    }
    let class_name = get_window_class_name(hwnd);
    Some(WindowInfo {
        class_name,
        title,
        _handle: hwnd.0 as isize,
    })
}

pub unsafe fn set_win_event_hook(
    event_min: u32,
    event_max: u32,
    callback: windows::Win32::UI::Accessibility::WINEVENTPROC,
) -> Result<windows::Win32::UI::Accessibility::HWINEVENTHOOK, windows::core::Error> {
    let hook = SetWinEventHook(event_min, event_max, None, callback, 0, 0, WINEVENT_OUTOFCONTEXT);
    if hook.0.is_null() {
        Err(windows::core::Error::new(
            windows::core::HRESULT(0x80004005u32 as i32),
            "SetWinEventHook failed",
        ))
    } else {
        Ok(hook)
    }
}

pub unsafe fn unhook_win_event(
    hook: windows::Win32::UI::Accessibility::HWINEVENTHOOK,
) {
    if !UnhookWinEvent(hook).as_bool() {
        tracing::debug!(target: "win32::window", "UnhookWinEvent returned false");
    }
}

pub fn enum_child_windows(parent: HWND) -> Vec<HWND> {
    let mut children: Vec<HWND> = Vec::new();
    unsafe {
        let ptr = LPARAM(&mut children as *mut _ as isize);
        if !EnumChildWindows(Some(parent), Some(enum_child_proc), ptr).as_bool() {
            tracing::debug!(target: "win32::window", "EnumChildWindows returned false");
        }
    }
    children
}

unsafe extern "system" fn enum_child_proc(hwnd: HWND, lparam: LPARAM) -> windows::core::BOOL {
    let children = lparam.0 as *mut Vec<HWND>;
    (*children).push(hwnd);
    windows::core::BOOL(1) // true
}

pub fn extract_icon_to_png(exe_path: &str, output_path: &Path) -> Result<(), Box<dyn std::error::Error>> {
    unsafe {
        let path_wide: Vec<u16> = exe_path.encode_utf16().chain(Some(0)).collect();
        let mut large = HICON::default();
        let mut small = HICON::default();
        let count = ExtractIconExW(
            PCWSTR(path_wide.as_ptr()),
            0,
            Some(&mut large as *mut _),
            Some(&mut small as *mut _),
            1,
        );
        if count == 0 || large.is_invalid() {
            if !small.is_invalid() {
                if DestroyIcon(small).is_err() {
                    tracing::debug!(target: "win32::window", "DestroyIcon(small) failed");
                }
            }
            return Err("No icon found".into());
        }
        if !small.is_invalid() {
            if let Err(e) = DestroyIcon(small) {
                tracing::debug!(target: "win32::window", "DestroyIcon(small) failed: {:?}", e);
            }
        }

        let mut info = windows::Win32::UI::WindowsAndMessaging::ICONINFO::default();
        if GetIconInfo(large, &mut info as *mut _).is_err() {
            if DestroyIcon(large).is_err() {
                tracing::debug!(target: "win32::window", "DestroyIcon(large) failed");
            }
            return Err("GetIconInfo failed".into());
        }

        let hdc = GetDC(None);

        let (width, height, hbm_color) = if info.hbmColor.is_invalid() {
            let mut bm = windows::Win32::Graphics::Gdi::BITMAP::default();
            let _ = GetObjectW(
                HGDIOBJ::from(info.hbmMask),
                std::mem::size_of::<windows::Win32::Graphics::Gdi::BITMAP>() as i32,
                Some(&mut bm as *mut _ as *mut _),
            );
            (bm.bmWidth, bm.bmHeight / 2, info.hbmMask)
        } else {
            let mut bm = windows::Win32::Graphics::Gdi::BITMAP::default();
            let _ = GetObjectW(
                HGDIOBJ::from(info.hbmColor),
                std::mem::size_of::<windows::Win32::Graphics::Gdi::BITMAP>() as i32,
                Some(&mut bm as *mut _ as *mut _),
            );
            (bm.bmWidth, bm.bmHeight, info.hbmColor)
        };

        let mut bmi = BITMAPINFO {
            bmiHeader: BITMAPINFOHEADER {
                biSize: std::mem::size_of::<BITMAPINFOHEADER>() as u32,
                biWidth: width,
                biHeight: -height,
                biPlanes: 1,
                biBitCount: 32,
                biCompression: BI_RGB.0,
                biSizeImage: 0,
                biXPelsPerMeter: 0,
                biYPelsPerMeter: 0,
                biClrUsed: 0,
                biClrImportant: 0,
            },
            bmiColors: [windows::Win32::Graphics::Gdi::RGBQUAD::default(); 1],
        };

        let mut pixels = vec![0u8; (width * height * 4) as usize];
        let lines = GetDIBits(
            hdc,
            hbm_color,
            0,
            height as u32,
            Some(pixels.as_mut_ptr() as *mut _),
            &mut bmi,
            DIB_RGB_COLORS,
        );

        if lines == 0 {
            if !info.hbmColor.is_invalid() {
                if !DeleteObject(HGDIOBJ::from(info.hbmColor)).as_bool() {
                    tracing::debug!(target: "win32::window", "DeleteObject(hbmColor) returned false");
                }
            }
            if !DeleteObject(HGDIOBJ::from(info.hbmMask)).as_bool() {
                tracing::debug!(target: "win32::window", "DeleteObject(hbmMask) returned false");
            }
            if ReleaseDC(None, hdc) == 0 {
                tracing::debug!(target: "win32::window", "ReleaseDC returned 0");
            }
            if DestroyIcon(large).is_err() {
                tracing::debug!(target: "win32::window", "DestroyIcon(large) failed");
            }
            return Err("GetDIBits failed".into());
        }

        if !info.hbmColor.is_invalid() {
            if !DeleteObject(HGDIOBJ::from(info.hbmColor)).as_bool() {
                tracing::debug!(target: "win32::window", "DeleteObject(hbmColor) returned false");
            }
        }
        if !DeleteObject(HGDIOBJ::from(info.hbmMask)).as_bool() {
            tracing::debug!(target: "win32::window", "DeleteObject(hbmMask) returned false");
        }
        if ReleaseDC(None, hdc) == 0 {
            tracing::debug!(target: "win32::window", "ReleaseDC returned 0");
        }
        if DestroyIcon(large).is_err() {
            tracing::debug!(target: "win32::window", "DestroyIcon(large) failed");
        }

        let mut img = image::RgbaImage::new(width as u32, height as u32);
        for y in 0..height {
            for x in 0..width {
                let idx = ((y * width + x) * 4) as usize;
                let b = pixels[idx];
                let g = pixels[idx + 1];
                let r = pixels[idx + 2];
                let a = pixels[idx + 3];
                img.put_pixel(x as u32, y as u32, image::Rgba([r, g, b, a]));
            }
        }

        img.save(output_path)?;
        Ok(())
    }
}
