use crate::models::WindowInfo;
use std::fs::File;
use std::io::BufWriter;
use std::path::Path;
use tracing::{debug, error};
use windows::Win32::Foundation::{HWND, LPARAM, RECT};
use windows::Win32::Graphics::Gdi::{
    CreateCompatibleDC, DeleteDC, DeleteObject, GetDC, GetDIBits, GetObjectW, ReleaseDC, BITMAPINFO, BITMAPINFOHEADER, BI_RGB,
    DIB_RGB_COLORS, HGDIOBJ,
};
use windows::Win32::Storage::FileSystem::FILE_ATTRIBUTE_NORMAL;
use windows::Win32::UI::Shell::{ExtractIconExW, SHGetFileInfoW, SHFILEINFOW, SHGFI_ICON, SHGFI_LARGEICON, SHGFI_SMALLICON, SHGFI_USEFILEATTRIBUTES};
use windows::Win32::UI::WindowsAndMessaging::{
    DestroyIcon, EnumChildWindows, GetClassNameW, GetForegroundWindow, GetIconInfo, GetWindowRect,
    GetWindowTextLengthW, GetWindowTextW, IsWindow, IsWindowVisible, HICON,
};
use windows::core::PCWSTR;

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
    if !is_valid_visible_window(hwnd) {
        return None;
    }
    let title = get_window_text(hwnd);
    if get_window_rect(hwnd).is_none() {
        error!(target: "win32::window", "GetWindowRect failed for hwnd={:?}", hwnd);
        return None;
    }
    let class_name = get_window_class_name(hwnd);
    Some(WindowInfo {
        class_name,
        title,
        _handle: hwnd.0 as isize,
    })
}

pub fn enum_child_windows(parent: HWND) -> Vec<HWND> {
    let mut children: Vec<HWND> = Vec::new();
    unsafe {
        let ptr = LPARAM(&mut children as *mut _ as isize);
        if !EnumChildWindows(Some(parent), Some(enum_child_proc), ptr).as_bool() {
            debug!(target: "win32::window", "EnumChildWindows returned false");
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
    if let Ok((width, height, pixels_rgba)) = extract_icon_rgba_via_extract_icon(exe_path) {
        return save_rgba_as_png(output_path, width, height, &pixels_rgba);
    }

    debug!(target: "win32::window", "ExtractIconExW failed for {}, trying SHGetFileInfoW", exe_path);
    if let Ok((width, height, pixels_rgba)) = extract_icon_rgba_via_shgetfileinfo(exe_path) {
        return save_rgba_as_png(output_path, width, height, &pixels_rgba);
    }

    Err("No icon found via ExtractIconExW or SHGetFileInfoW".into())
}

fn extract_icon_rgba_via_extract_icon(exe_path: &str) -> Result<(i32, i32, Vec<u8>), Box<dyn std::error::Error>> {
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

        if count == 0 || count == u32::MAX {
            return Err("ExtractIconExW returned 0 or u32::MAX".into());
        }

        let icon = if !large.is_invalid() {
            if !small.is_invalid() {
                let _ = DestroyIcon(small);
            }
            large
        } else if !small.is_invalid() {
            debug!(target: "win32::window", "Using small icon fallback for {}", exe_path);
            small
        } else {
            return Err("Both large and small icons are invalid".into());
        };

        extract_icon_rgba_from_hicon(icon)
    }
}

fn extract_icon_rgba_via_shgetfileinfo(exe_path: &str) -> Result<(i32, i32, Vec<u8>), Box<dyn std::error::Error>> {
    unsafe {
        let path_wide: Vec<u16> = exe_path.encode_utf16().chain(Some(0)).collect();
        let mut shfi = SHFILEINFOW::default();

        let result = SHGetFileInfoW(
            PCWSTR(path_wide.as_ptr()),
            FILE_ATTRIBUTE_NORMAL,
            Some(&mut shfi as *mut _),
            std::mem::size_of::<SHFILEINFOW>() as u32,
            SHGFI_ICON | SHGFI_LARGEICON | SHGFI_USEFILEATTRIBUTES,
        );

        if result != 0 && !shfi.hIcon.is_invalid() {
            return extract_icon_rgba_from_hicon(shfi.hIcon);
        }

        // 回退到小图标
        let result = SHGetFileInfoW(
            PCWSTR(path_wide.as_ptr()),
            FILE_ATTRIBUTE_NORMAL,
            Some(&mut shfi as *mut _),
            std::mem::size_of::<SHFILEINFOW>() as u32,
            SHGFI_ICON | SHGFI_SMALLICON | SHGFI_USEFILEATTRIBUTES,
        );

        if result != 0 && !shfi.hIcon.is_invalid() {
            return extract_icon_rgba_from_hicon(shfi.hIcon);
        }

        Err("SHGetFileInfoW failed to get icon".into())
    }
}

fn extract_icon_rgba_from_hicon(icon: HICON) -> Result<(i32, i32, Vec<u8>), Box<dyn std::error::Error>> {
    unsafe {
        let mut info = windows::Win32::UI::WindowsAndMessaging::ICONINFO::default();
        if GetIconInfo(icon, &mut info as *mut _).is_err() {
            // GetIconInfo 失败时仍需释放可能的 GDI 对象
            if !info.hbmColor.is_invalid() {
                let _ = DeleteObject(HGDIOBJ::from(info.hbmColor));
            }
            if !info.hbmMask.is_invalid() {
                let _ = DeleteObject(HGDIOBJ::from(info.hbmMask));
            }
            if DestroyIcon(icon).is_err() {
                debug!(target: "win32::window", "DestroyIcon failed");
            }
            return Err("GetIconInfo failed".into());
        }

        let hdc = GetDC(None);
        let mem_dc = CreateCompatibleDC(Some(hdc));

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

        // 检查宽高是否有效
        if width == 0 || height == 0 {
            if !info.hbmColor.is_invalid() {
                let _ = DeleteObject(HGDIOBJ::from(info.hbmColor));
            }
            let _ = DeleteObject(HGDIOBJ::from(info.hbmMask));
            let _ = ReleaseDC(None, hdc);
            let _ = DestroyIcon(icon);
            return Err("Invalid bitmap dimensions (width or height is 0)".into());
        }

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
            mem_dc,
            hbm_color,
            0,
            height as u32,
            Some(pixels.as_mut_ptr() as *mut _),
            &mut bmi,
            DIB_RGB_COLORS,
        );

        if lines == 0 {
            if !info.hbmColor.is_invalid() {
                let _ = DeleteObject(HGDIOBJ::from(info.hbmColor));
            }
            let _ = DeleteObject(HGDIOBJ::from(info.hbmMask));
            let _ = DeleteDC(mem_dc);
            let _ = ReleaseDC(None, hdc);
            let _ = DestroyIcon(icon);
            return Err("GetDIBits failed".into());
        }

        // 释放资源
        if !info.hbmColor.is_invalid() {
            let _ = DeleteObject(HGDIOBJ::from(info.hbmColor));
        }
        let _ = DeleteObject(HGDIOBJ::from(info.hbmMask));
        let _ = DeleteDC(mem_dc);
        let _ = ReleaseDC(None, hdc);
        let _ = DestroyIcon(icon);

        for chunk in pixels.chunks_exact_mut(4) {
            chunk.swap(0, 2);
        }

        Ok((width, height, pixels))
    }
}

/// 将 RGBA 像素数据保存为 PNG 文件
fn save_rgba_as_png(output_path: &Path, width: i32, height: i32, rgba_pixels: &[u8]) -> Result<(), Box<dyn std::error::Error>> {
    let file = File::create(output_path)?;
    let mut encoder = png::Encoder::new(BufWriter::new(file), width as u32, height as u32);
    encoder.set_color(png::ColorType::Rgba);
    encoder.set_depth(png::BitDepth::Eight);
    encoder.write_header()?.write_image_data(rgba_pixels)?;
    Ok(())
}
