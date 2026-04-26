//! 单实例检测：Windows 下用命名互斥量，其他平台暂不做限制

#[cfg(target_os = "windows")]
mod imp {
    type Handle = *mut core::ffi::c_void;

    #[link(name = "kernel32")]
    extern "system" {
        fn CreateMutexW(
            lpMutexAttributes: *mut core::ffi::c_void,
            bInitialOwner: i32,
            lpName: *const u16,
        ) -> Handle;
        fn GetLastError() -> u32;
        fn CloseHandle(hObject: Handle) -> i32;
    }

    const ERROR_ALREADY_EXISTS: u32 = 183;

    pub struct Guard(Handle);

    impl Drop for Guard {
        fn drop(&mut self) {
            unsafe {
                let _ = CloseHandle(self.0);
            }
        }
    }

    pub fn try_acquire(name: &str) -> Option<Guard> {
        let name_wide: Vec<u16> = name.encode_utf16().chain(Some(0)).collect();
        let handle = unsafe { CreateMutexW(std::ptr::null_mut(), 1, name_wide.as_ptr()) };
        if handle.is_null() {
            return None;
        }
        let err = unsafe { GetLastError() };
        if err == ERROR_ALREADY_EXISTS {
            unsafe {
                let _ = CloseHandle(handle);
            }
            return None;
        }
        Some(Guard(handle))
    }
}

#[cfg(not(target_os = "windows"))]
mod imp {
    pub struct Guard;

    pub fn try_acquire(_name: &str) -> Option<Guard> {
        // 非 Windows 平台暂不做限制，确保跨平台编译通过
        Some(Guard)
    }
}

pub use imp::try_acquire;
