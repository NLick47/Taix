pub mod job_object;
pub mod scheduler;
pub mod single_instance;
pub mod tray;

pub use scheduler::{install, uninstall};
pub use single_instance::try_acquire as try_acquire_single_instance;
pub use tray::run_tray;

use crate::config::{Language, Theme};

#[cfg(target_os = "windows")]
pub fn apply_menu_theme(theme: Theme) {
    use windows::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryA};
    use windows::core::PCSTR;

    #[repr(C)]
    #[derive(Clone, Copy)]
    enum PreferredAppMode {
        AllowDark = 1,
        ForceDark = 2,
        ForceLight = 3,
    }

    type SetPreferredAppMode = unsafe extern "system" fn(PreferredAppMode) -> PreferredAppMode;
    type FlushMenuThemes = unsafe extern "system" fn();

    unsafe {
        let Ok(hmodule) = LoadLibraryA(PCSTR::from_raw(b"uxtheme.dll\0".as_ptr())) else {
            return;
        };

        let set_mode = GetProcAddress(hmodule, PCSTR::from_raw(135 as *const u8));
        let flush = GetProcAddress(hmodule, PCSTR::from_raw(136 as *const u8));

        if let Some(set_mode) = set_mode {
            let set_mode: SetPreferredAppMode = std::mem::transmute(set_mode);
            let mode = match theme {
                Theme::Dark => PreferredAppMode::ForceDark,
                Theme::Light => PreferredAppMode::ForceLight,
                Theme::System => PreferredAppMode::AllowDark,
            };
            let _ = set_mode(mode);
        }

        if let Some(flush) = flush {
            let flush: FlushMenuThemes = std::mem::transmute(flush);
            flush();
        }
    }
}

#[cfg(target_os = "windows")]
pub fn is_process_alive(pid: u32) -> bool {
    use windows::Win32::Foundation::{CloseHandle, STILL_ACTIVE};
    use windows::Win32::System::Threading::{GetExitCodeProcess, OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION};

    unsafe {
        let handle = match OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) {
            Ok(h) => h,
            Err(_) => return false,
        };
        let mut exit_code: u32 = 0;
        let alive = if GetExitCodeProcess(handle, &mut exit_code).is_ok() {
            exit_code == STILL_ACTIVE.0 as u32
        } else {
            false
        };
        let _ = CloseHandle(handle);
        alive
    }
}

#[cfg(target_os = "windows")]
pub fn detect_system_language() -> Language {
    use windows::Win32::Globalization::GetUserDefaultUILanguage;

    unsafe {
        let lang_id = GetUserDefaultUILanguage();
        let primary_lang = lang_id & 0x3FF;
        if primary_lang == 0x04 {
            Language::ZhCn
        } else {
            Language::EnUs
        }
    }
}
