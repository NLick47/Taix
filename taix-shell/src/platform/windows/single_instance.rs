use windows::Win32::Foundation::{CloseHandle, GetLastError, WIN32_ERROR};
use windows::Win32::System::Threading::CreateMutexW;
use windows::core::PCWSTR;

pub struct Guard(windows::Win32::Foundation::HANDLE);

impl Drop for Guard {
    fn drop(&mut self) {
        unsafe {
            let _ = CloseHandle(self.0);
        }
    }
}

pub fn try_acquire(name: &str) -> Option<Guard> {
    let name_wide: Vec<u16> = name.encode_utf16().chain(Some(0)).collect();
    unsafe {
        let handle = CreateMutexW(None, true, PCWSTR(name_wide.as_ptr())).ok()?;
        if GetLastError() == WIN32_ERROR(183) {
            let _ = CloseHandle(handle);
            return None;
        }
        Some(Guard(handle))
    }
}
