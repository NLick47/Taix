use std::path::Path;
use windows::Win32::Foundation::CloseHandle;
use windows::Win32::Storage::FileSystem::{GetFileVersionInfoSizeW, GetFileVersionInfoW, VerQueryValueW};
use windows::Win32::System::Threading::{OpenProcess, PROCESS_QUERY_INFORMATION, PROCESS_VM_READ};
use windows::core::PCWSTR;

#[link(name = "kernel32")]
extern "system" {
    fn QueryFullProcessImageNameW(hProcess: windows::Win32::Foundation::HANDLE, dwFlags: u32, lpExeName: *mut u16, lpdwSize: *mut u32) -> i32;
    fn GetUserDefaultLangID() -> u16;
}

pub fn get_process_exe_path(pid: u32) -> Option<String> {
    unsafe {
        let handle = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, pid).ok()?;
        let mut buf = vec![0u16; 4096];
        let mut len = buf.len() as u32;
        let result = QueryFullProcessImageNameW(handle, 0, buf.as_mut_ptr(), &mut len);
        let _ = CloseHandle(handle);
        if result == 0 {
            return None;
        }
        Some(String::from_utf16_lossy(&buf[..len as usize]).trim_end_matches('\0').to_string())
    }
}

pub fn get_process_name(pid: u32) -> Option<String> {
    get_process_exe_path(pid).map(|p| {
        Path::new(&p)
            .file_stem()
            .map(|s| s.to_string_lossy().to_string())
            .unwrap_or_default()
    })
}

pub fn get_file_description(path: &str) -> Option<String> {
    if path.is_empty() {
        return None;
    }

    unsafe {
        let path_wide: Vec<u16> = path.encode_utf16().chain(Some(0)).collect();
        let mut handle = 0u32;
        let size = GetFileVersionInfoSizeW(PCWSTR(path_wide.as_ptr()), Some(&mut handle));
        if size == 0 {
            return None;
        }
        let mut data = vec![0u8; size as usize];
        if GetFileVersionInfoW(PCWSTR(path_wide.as_ptr()), Some(handle), size, data.as_mut_ptr() as _).is_err() {
            return None;
        }

        let mut buf_ptr = std::ptr::null_mut();
        let mut buf_len = 0u32;
        let sub_block: Vec<u16> = "\\VarFileInfo\\Translation"
            .encode_utf16()
            .chain(Some(0))
            .collect();

        if !VerQueryValueW(data.as_ptr() as _, PCWSTR(sub_block.as_ptr()), &mut buf_ptr, &mut buf_len).as_bool() || buf_len < 4 {
            return None;
        }

        let count = (buf_len / 4) as usize;
        let translations = std::slice::from_raw_parts(buf_ptr as *const u32, count);

        let mut pairs: Vec<(u16, u16)> = Vec::with_capacity(count);
        for &t in translations {
            let lang = (t & 0xFFFF) as u16;
            let codepage = ((t >> 16) & 0xFFFF) as u16;
            pairs.push((lang, codepage));
        }

        let user_lang = GetUserDefaultLangID();

        let try_query = |lang: u16, codepage: u16| -> Option<String> {
            let query = format!("\\StringFileInfo\\{:04x}{:04x}\\FileDescription", lang, codepage);
            let query_wide: Vec<u16> = query.encode_utf16().chain(Some(0)).collect();
            let mut desc_ptr = std::ptr::null_mut();
            let mut desc_len = 0u32;
            if VerQueryValueW(data.as_ptr() as _, PCWSTR(query_wide.as_ptr()), &mut desc_ptr, &mut desc_len).as_bool() && desc_len > 0 {
                // VerQueryValueW 对 StringFileInfo 字符串返回的 puLen 是字符数（wValueLength），
                // 不是字节数，不需要除以 2。参考 .NET FileVersionInfo 的实现。
                let slice = std::slice::from_raw_parts(desc_ptr as *const u16, desc_len as usize);
                let s = String::from_utf16_lossy(slice).trim_end_matches('\0').to_string();
                if !s.is_empty() {
                    return Some(s);
                }
            }
            None
        };

        // 尝试默认语言
        for &(lang, codepage) in &pairs {
            if lang == user_lang {
                if let Some(desc) = try_query(lang, codepage) {
                    return Some(desc);
                }
            }
        }

        // 尝试中文
        let chinese_langs: [u16; 5] = [0x0804, 0x0404, 0x0C04, 0x1004, 0x1404];
        for &target_lang in &chinese_langs {
            for &(lang, codepage) in &pairs {
                if lang == target_lang {
                    if let Some(desc) = try_query(lang, codepage) {
                        return Some(desc);
                    }
                }
            }
        }

        // 尝试英文
        for &(lang, codepage) in &pairs {
            if lang == 0x0409 {
                if let Some(desc) = try_query(lang, codepage) {
                    return Some(desc);
                }
            }
        }

        // 回退到第一个可用 Translation
        for &(lang, codepage) in &pairs {
            if let Some(desc) = try_query(lang, codepage) {
                return Some(desc);
            }
        }

        None
    }
}