use std::path::PathBuf;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::OnceLock;
use std::time::Duration;

use crate::constants::CLIENT_EXE_NAME;

#[cfg(target_os = "windows")]
use crate::constants::CLIENT_PIPE_NAME;

static IS_LAUNCHING: AtomicBool = AtomicBool::new(false);

static CLIENT_EXE_PATH: OnceLock<PathBuf> = OnceLock::new();

#[cfg(target_os = "windows")]
const CLIENT_MUTEX_NAME: &str = "Taix";

const LAUNCH_DEBOUNCE_MS: u64 = 1500;

fn cache_client_exe_path() -> Option<PathBuf> {
    #[cfg(target_os = "macos")]
    {
        Some(PathBuf::from("/Applications/Taix.app"))
    }

    #[cfg(not(target_os = "macos"))]
    {
        let exe_dir = std::env::current_exe().ok()?.parent()?.to_path_buf();
        Some(exe_dir.join(CLIENT_EXE_NAME))
    }
}

pub fn warm_client_exe_path() {
    if let Some(path) = cache_client_exe_path() {
        let _ = CLIENT_EXE_PATH.set(path);
    }
}

pub fn launch_or_wake() -> anyhow::Result<()> {
    if is_client_running() {
        if try_wake_existing() {
            return Ok(());
        }
        return Ok(());
    }

    if IS_LAUNCHING.swap(true, Ordering::SeqCst) {
        return Ok(());
    }

    let result = spawn_new_process();

    std::thread::spawn(|| {
        std::thread::sleep(Duration::from_millis(LAUNCH_DEBOUNCE_MS));
        IS_LAUNCHING.store(false, Ordering::SeqCst);
    });

    result
}

#[cfg(target_os = "windows")]
fn is_client_running() -> bool {
    use windows::Win32::Foundation::CloseHandle;
    use windows::Win32::System::Threading::{OpenMutexW, SYNCHRONIZATION_SYNCHRONIZE};

    static MUTEX_NAME_WIDE: OnceLock<Vec<u16>> = OnceLock::new();
    let name_wide = MUTEX_NAME_WIDE
        .get_or_init(|| CLIENT_MUTEX_NAME.encode_utf16().chain(Some(0)).collect());

    unsafe {
        match OpenMutexW(
            SYNCHRONIZATION_SYNCHRONIZE,
            false,
            windows::core::PCWSTR(name_wide.as_ptr()),
        ) {
            Ok(handle) => {
                let _ = CloseHandle(handle);
                true
            }
            Err(_) => false,
        }
    }
}

#[cfg(target_os = "macos")]
fn is_client_running() -> bool {
    std::os::unix::net::UnixStream::connect("/tmp/taix-client.sock").is_ok()
}

fn try_wake_existing() -> bool {
    const MAX_RETRIES: usize = 2;
    const RETRY_DELAY_MS: u64 = 80;

    for attempt in 0..MAX_RETRIES {
        if try_wake_once() {
            return true;
        }
        if attempt + 1 < MAX_RETRIES {
            std::thread::sleep(Duration::from_millis(RETRY_DELAY_MS));
        }
    }
    false
}

#[cfg(target_os = "windows")]
fn try_wake_once() -> bool {
    use std::io::Write;
    use windows::Win32::System::Pipes::WaitNamedPipeW;

    const PIPE_TIMEOUT_MS: u32 = 100;

    static PIPE_NAME_WIDE: OnceLock<Vec<u16>> = OnceLock::new();
    let pipe_name_wide = PIPE_NAME_WIDE
        .get_or_init(|| CLIENT_PIPE_NAME.encode_utf16().chain(Some(0)).collect());

    let pipe_available = unsafe {
        WaitNamedPipeW(
            windows::core::PCWSTR(pipe_name_wide.as_ptr()),
            PIPE_TIMEOUT_MS,
        )
        .as_bool()
    };

    if !pipe_available {
        return false;
    }

    let mut file = match std::fs::OpenOptions::new()
        .write(true)
        .open(CLIENT_PIPE_NAME)
    {
        Ok(f) => f,
        Err(_) => return false,
    };

    file.write_all(b"show\n").is_ok()
}

#[cfg(not(target_os = "windows"))]
fn try_wake_once() -> bool {
    use std::io::Write;
    use std::os::unix::net::UnixStream;

    const SOCKET_PATH: &str = "/tmp/taix-client.sock";
    const SOCKET_TIMEOUT_MS: u64 = 100;

    let mut stream = match UnixStream::connect(SOCKET_PATH) {
        Ok(s) => s,
        Err(_) => return false,
    };

    let _ = stream.set_write_timeout(Some(std::time::Duration::from_millis(SOCKET_TIMEOUT_MS)));

    stream.write_all(b"show\n").is_ok()
}

fn spawn_new_process() -> anyhow::Result<()> {
    #[cfg(target_os = "macos")]
    {
        std::process::Command::new("open")
            .arg("/Applications/Taix.app")
            .spawn()
            .map_err(|e| anyhow::anyhow!("failed to spawn client: {}", e))?;
    }

    #[cfg(not(target_os = "macos"))]
    {
        let client_exe = match CLIENT_EXE_PATH.get() {
            Some(p) => p.clone(),
            None => {
                let path = cache_client_exe_path()
                    .ok_or_else(|| anyhow::anyhow!("failed to resolve client exe path"))?;
                let _ = CLIENT_EXE_PATH.set(path.clone());
                path
            }
        };

        if !client_exe.exists() {
            return Err(anyhow::anyhow!(
                "client executable not found: {}",
                client_exe.display()
            ));
        }

        let mut cmd = std::process::Command::new(&client_exe);

        #[cfg(target_os = "windows")]
        {
            use std::os::windows::process::CommandExt;
            const ABOVE_NORMAL_PRIORITY_CLASS: u32 = 0x00008000;
            cmd.creation_flags(ABOVE_NORMAL_PRIORITY_CLASS);
        }

        cmd.spawn()
            .map_err(|e| anyhow::anyhow!("failed to spawn client: {}", e))?;
    }

    Ok(())
}
