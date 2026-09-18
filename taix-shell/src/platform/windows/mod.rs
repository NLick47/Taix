pub mod job_object;
pub mod scheduler;
pub mod single_instance;
pub mod tray;

pub use scheduler::{install, uninstall};
pub use single_instance::try_acquire as try_acquire_single_instance;
pub use tray::run_tray;

use crate::config::{Language, Theme};

pub(crate) fn apply_menu_theme(theme: Theme) {
    use windows::Win32::System::LibraryLoader::{GetProcAddress, LoadLibraryA};
    use windows::core::PCSTR;

    #[repr(C)]
    #[derive(Clone, Copy, Debug)]
    enum PreferredAppMode {
        AllowDark = 1,
        ForceDark = 2,
        ForceLight = 3,
    }

    type SetPreferredAppMode = unsafe extern "system" fn(PreferredAppMode) -> PreferredAppMode;
    type FlushMenuThemes = unsafe extern "system" fn();

    tracing::info!(target: "taix_shell::theme", "apply_menu_theme requested={:?}", theme);

    unsafe {
        let Ok(hmodule) = LoadLibraryA(PCSTR::from_raw(b"uxtheme.dll\0".as_ptr())) else {
            tracing::warn!(
                target: "taix_shell::theme",
                "uxtheme.dll could not be loaded; popup menu keeps the default (light) theme"
            );
            return;
        };

        let set_mode = GetProcAddress(hmodule, PCSTR::from_raw(135 as *const u8));
        let flush = GetProcAddress(hmodule, PCSTR::from_raw(136 as *const u8));

        match (set_mode, flush) {
            (Some(_), Some(_)) => tracing::debug!(
                target: "taix_shell::theme",
                "uxtheme exports resolved by ordinal (135=SetPreferredAppMode, 136=FlushMenuThemes)"
            ),
            (set_mode, flush) => tracing::warn!(
                target: "taix_shell::theme",
                "uxtheme ordinal lookup incomplete (135 present={} 136 present={}); menu theme may not apply",
                set_mode.is_some(),
                flush.is_some()
            ),
        }

        if let Some(set_mode) = set_mode {
            let set_mode: SetPreferredAppMode = std::mem::transmute(set_mode);
            let mode = match theme {
                Theme::Dark => PreferredAppMode::ForceDark,
                Theme::Light => PreferredAppMode::ForceLight,
                Theme::System => PreferredAppMode::AllowDark,
            };
            let _ = set_mode(mode);
            tracing::info!(target: "taix_shell::theme", "SetPreferredAppMode(ordinal 135) called with {:?}", mode);
        }

        if let Some(flush) = flush {
            let flush: FlushMenuThemes = std::mem::transmute(flush);
            flush();
            tracing::debug!(target: "taix_shell::theme", "FlushMenuThemes(ordinal 136) called");
        }
    }
}

pub fn system_language() -> Language {
    use windows::Win32::Globalization::GetUserDefaultUILanguage;

    const LANG_CHINESE: u16 = 0x04;
    const PRIMARY_LANG_MASK: u16 = 0x03FF;

    let langid = unsafe { GetUserDefaultUILanguage() };
    let language = if langid & PRIMARY_LANG_MASK == LANG_CHINESE {
        Language::ZhCn
    } else {
        Language::EnUs
    };
    tracing::info!(
        target: "taix_shell::i18n",
        "GetUserDefaultUILanguage=0x{:04X} primary_lang=0x{:02X} → {:?}",
        langid,
        langid & PRIMARY_LANG_MASK,
        language
    );
    language
}

pub fn is_process_alive(pid: u32) -> bool {
    use windows::Win32::Foundation::{CloseHandle, STILL_ACTIVE};
    use windows::Win32::System::Threading::{GetExitCodeProcess, OpenProcess, PROCESS_QUERY_LIMITED_INFORMATION};

    let alive = unsafe {
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
    };
    tracing::debug!(target: "taix_shell::process", "is_process_alive pid={} → {}", pid, alive);
    alive
}
