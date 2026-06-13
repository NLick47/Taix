use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;

use crate::constants::CLIENT_EXE_NAME;

#[cfg(target_os = "windows")]
use crate::constants::CLIENT_PIPE_NAME;

static IS_LAUNCHING: AtomicBool = AtomicBool::new(false);

pub fn launch_or_wake() -> anyhow::Result<()> {
    if try_wake_existing() {
        return Ok(());
    }

    if IS_LAUNCHING.swap(true, Ordering::SeqCst) {
        return Ok(());
    }

    let result = spawn_new_process();

    std::thread::spawn(|| {
        std::thread::sleep(Duration::from_secs(3));
        IS_LAUNCHING.store(false, Ordering::SeqCst);
    });

    result
}

fn try_wake_existing() -> bool {
    const MAX_RETRIES: usize = 3;
    const RETRY_DELAY_MS: u64 = 300;

    for attempt in 0..MAX_RETRIES {
        match try_wake_once() {
            true => return true,
            false if attempt + 1 < MAX_RETRIES => {
                std::thread::sleep(Duration::from_millis(RETRY_DELAY_MS));
            }
            false => return false,
        }
    }
    false
}

#[cfg(target_os = "windows")]
fn try_wake_once() -> bool {
    use std::io::Write;
    use windows::Win32::System::Pipes::WaitNamedPipeW;

    const PIPE_TIMEOUT_MS: u32 = 200;

    // WaitNamedPipeW 先等待 pipe server 就绪（带超时），避免后续 CreateFileW 无限阻塞
    let pipe_name_wide: Vec<u16> = CLIENT_PIPE_NAME.encode_utf16().chain(Some(0)).collect();
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

    // pipe 已就绪，此时 CreateFileW 不会阻塞
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
    const SOCKET_TIMEOUT_MS: u64 = 200;

    // Try to connect to the Unix domain socket
    let mut stream = match UnixStream::connect(SOCKET_PATH) {
        Ok(s) => s,
        Err(_) => return false,
    };

    // Set a timeout to avoid blocking
    let _ = stream.set_write_timeout(Some(std::time::Duration::from_millis(SOCKET_TIMEOUT_MS)));

    // Send wake command
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
        let exe_dir = std::env::current_exe()?
            .parent()
            .ok_or_else(|| anyhow::anyhow!("failed to get exe directory"))?
            .to_path_buf();

        let client_exe = exe_dir.join(CLIENT_EXE_NAME);

        if !client_exe.exists() {
            return Err(anyhow::anyhow!(
                "client executable not found: {}",
                client_exe.display()
            ));
        }

        std::process::Command::new(&client_exe)
            .spawn()
            .map_err(|e| anyhow::anyhow!("failed to spawn client: {}", e))?;
    }

    Ok(())
}
