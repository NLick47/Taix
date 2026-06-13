//! 单实例检测：Windows 下用命名互斥量，Unix 下用 flock 文件锁

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

#[cfg(unix)]
mod imp {
    use std::fs::OpenOptions;
    use std::os::unix::fs::OpenOptionsExt;
    use std::os::unix::io::AsRawFd;

    pub struct Guard {
        _file: std::fs::File,
    }

    pub fn try_acquire(name: &str) -> Option<Guard> {
        let safe_name: String = name
            .trim_start_matches("Global\\")
            .chars()
            .map(|c| if c.is_alphanumeric() || c == '-' || c == '_' { c } else { '_' })
            .collect();

        let lock_path = format!("/tmp/{}.lock", safe_name);

        let file = OpenOptions::new()
            .read(true)
            .write(true)
            .create(true)
            .truncate(true)
            .mode(0o644)
            .open(&lock_path)
            .ok()?;

        let fd = file.as_raw_fd();

        if unsafe { libc::flock(fd, libc::LOCK_EX | libc::LOCK_NB) } == 0 {
            Some(Guard { _file: file })
        } else {
            None
        }
    }
}

pub use imp::try_acquire;
