use std::fs::File;
use std::io;
use std::os::unix::io::AsRawFd;
use std::path::PathBuf;

pub struct Guard {
    _file: File,
}

pub fn try_acquire(name: &str) -> Option<Guard> {
    let lock_dir = dirs::cache_dir()
        .unwrap_or_else(|| PathBuf::from("/tmp"))
        .join("taix");

    let _ = std::fs::create_dir_all(&lock_dir);

    let lock_path = lock_dir.join(format!("{}.lock", name));

    let file = File::options()
        .read(true)
        .write(true)
        .create(true)
        .truncate(false)
        .open(&lock_path)
        .ok()?;

    // Try to acquire an exclusive lock (non-blocking)
    let result = unsafe { libc::flock(file.as_raw_fd(), libc::LOCK_EX | libc::LOCK_NB) };

    if result == -1 {
        let err = io::Error::last_os_error();
        if err.kind() == io::ErrorKind::WouldBlock {
            return None;
        }
        return None;
    }

    Some(Guard { _file: file })
}
