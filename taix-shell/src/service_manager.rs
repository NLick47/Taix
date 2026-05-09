use std::path::{Path, PathBuf};
use std::process::Stdio;
use std::time::Duration;
use tokio::process::{Child, Command};
use tokio::time::sleep;

#[derive(Clone)]
pub struct ServiceManager {
    data_dir: Option<PathBuf>,
    shutdown_tx: tokio::sync::broadcast::Sender<()>,
}

enum RunOutcome {
    Exited,
    Shutdown,
}

impl ServiceManager {
    /// 累计等待约 105 秒后放弃（5+10+15+20+25+30）。
    const MAX_MISSING_RETRIES: u32 = 6;
    const MAX_MISSING_WAIT_SECS: u64 = 105;

    pub fn new(data_dir: Option<PathBuf>) -> Self {
        let (shutdown_tx, _) = tokio::sync::broadcast::channel(1);
        Self { data_dir, shutdown_tx }
    }

    pub fn shutdown(&self) {
        let _ = self.shutdown_tx.send(());
    }

    pub async fn run(self) {
        let monitor = self.clone().supervise(
            crate::constants::MONITOR_EXE_NAME,
            &["run"],
            "taix-monitor",
        );
        let server = self.supervise(
            crate::constants::SERVER_EXE_NAME,
            &[],
            "taix-server",
        );

        let ((), ()) = tokio::join!(monitor, server);
        tracing::info!(target: "taix_shell::service_manager", "All services stopped");
    }

    async fn supervise(
        self,
        exe_name: &'static str,
        extra_args: &'static [&'static str],
        name: &'static str,
    ) {
        #[cfg(target_os = "windows")]
        let job = crate::platform::job_object::JobObject::new();

        let mut shutdown_rx = self.shutdown_tx.subscribe();
        let mut backoff = Backoff::new();
        let mut missing_retries: u32 = 0;

        loop {
            let exe_path = match resolve_exe_path(exe_name) {
                Some(p) => p,
                None => {
                    missing_retries += 1;
                    if missing_retries > Self::MAX_MISSING_RETRIES {
                        tracing::error!(
                            target: "taix_shell::service_manager",
                            "Giving up on {}: executable not found after {} retries (~{}s total)",
                            name,
                            Self::MAX_MISSING_RETRIES,
                            Self::MAX_MISSING_WAIT_SECS
                        );
                        return;
                    }
                    tracing::error!(
                        target: "taix_shell::service_manager",
                        "{} executable not found (retry {}/{})",
                        name,
                        missing_retries,
                        Self::MAX_MISSING_RETRIES
                    );
                    let delay = backoff.next_delay().max(Duration::from_secs(5));
                    tokio::select! {
                        _ = sleep(delay) => continue,
                        _ = shutdown_rx.recv() => {
                            tracing::info!(
                                target: "taix_shell::service_manager",
                                "Shutdown requested while {} executable was missing",
                                name
                            );
                            return;
                        }
                    }
                }
            };

            if missing_retries > 0 {
                tracing::info!(
                    target: "taix_shell::service_manager",
                    "Found {} at {}, resetting backoff",
                    name,
                    exe_path.display()
                );
                missing_retries = 0;
                backoff = Backoff::new();
            }

            let delay = backoff.next_delay();
            if delay > Duration::ZERO {
                tracing::info!(
                    target: "taix_shell::service_manager",
                    "Waiting {:?} before restarting {} ({} consecutive failures)",
                    delay,
                    name,
                    backoff.failures
                );
                tokio::select! {
                    _ = sleep(delay) => {}
                    _ = shutdown_rx.recv() => {
                        tracing::info!(
                            target: "taix_shell::service_manager",
                            "Shutdown requested during {} backoff",
                            name
                        );
                        return;
                    }
                }
            }

            // 检查是否已有同名进程在运行（如旧 shell 崩溃残留）
            #[cfg(target_os = "windows")]
            if let Some(pid) = find_existing_process(exe_name) {
                tracing::info!(
                    target: "taix_shell::service_manager",
                    "Existing {} process found (pid={}), taking over monitoring",
                    name,
                    pid
                );
                match wait_for_existing_process(pid, &mut shutdown_rx, name).await {
                    RunOutcome::Exited => continue,
                    RunOutcome::Shutdown => return,
                }
            }

            tracing::info!(
                target: "taix_shell::service_manager",
                "Starting {}: {} {:?}",
                name,
                exe_path.display(),
                extra_args
            );

            let args = build_args(extra_args, self.data_dir.as_ref());
            let mut child = match spawn_process(&exe_path, &args).await {
                Ok(c) => c,
                Err(e) => {
                    tracing::error!(
                        target: "taix_shell::service_manager",
                        "Failed to spawn {}: {}",
                        name,
                        e
                    );
                    backoff.record_failure();
                    continue;
                }
            };

            // 将子进程加入 Job Object：shell 异常崩溃时子进程随 Job 关闭而被系统终止。
            #[cfg(target_os = "windows")]
            if let (Some(pid), Ok(ref job)) = (child.id(), &job) {
                if let Err(e) = job.assign_process(pid) {
                    tracing::warn!(
                        target: "taix_shell::service_manager",
                        "Failed to assign {} (pid={}) to job object: {}. \
                         Shell crash may leave orphan process.",
                        name,
                        pid,
                        e
                    );
                }
            }

            match run_until_exit(&mut child, &mut shutdown_rx, name).await {
                RunOutcome::Exited => {
                    backoff.record_failure();
                }
                RunOutcome::Shutdown => return,
            }
        }
    }
}

fn build_args(extra: &[&str], data_dir: Option<&PathBuf>) -> Vec<String> {
    let mut args: Vec<String> = extra.iter().map(|&s| s.to_owned()).collect();
    if let Some(dir) = data_dir {
        args.push("--data-dir".to_owned());
        args.push(dir.to_string_lossy().into_owned());
    }
    args
}

fn resolve_exe_path(exe_name: &str) -> Option<PathBuf> {
    if let Ok(dir) = std::env::var("TAIX_EXE_DIR") {
        let p = PathBuf::from(dir).join(exe_name);
        if p.exists() {
            return Some(p);
        }
    }

    if let Some(dir) = std::env::current_exe()
        .ok()
        .and_then(|p| p.parent().map(|p| p.to_path_buf()))
    {
        let p = dir.join(exe_name);
        if p.exists() {
            return Some(p);
        }
    }

    if let Some(dir) = std::env::current_dir().ok() {
        let p = dir.join(exe_name);
        if p.exists() {
            return Some(p);
        }
    }

    None
}

/// 重定向标准流防止管道缓冲区死锁。
async fn spawn_process(exe: &Path, args: &[String]) -> std::io::Result<Child> {
    Command::new(exe)
        .current_dir(exe.parent().unwrap_or_else(|| Path::new(".")))
        .args(args)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()
}

/// 包含启动后 2 秒存活检查；若进程在 2 秒内退出，视为启动失败。
async fn run_until_exit(
    child: &mut Child,
    shutdown_rx: &mut tokio::sync::broadcast::Receiver<()>,
    name: &str,
) -> RunOutcome {
    let alive = tokio::select! {
        _ = sleep(Duration::from_secs(2)) => check_startup_alive(child, name),
        _ = shutdown_rx.recv() => {
            tracing::info!(
                target: "taix_shell::service_manager",
                "Shutdown requested during {} startup",
                name
            );
            let _ = child.kill().await;
            let _ = child.wait().await;
            return RunOutcome::Shutdown;
        }
    };

    if !alive {
        let _ = child.kill().await;
        let _ = child.wait().await;
        return RunOutcome::Exited;
    }

    tokio::select! {
        status = child.wait() => {
            match status {
                Ok(code) => {
                    tracing::warn!(
                        target: "taix_shell::service_manager",
                        "{} exited with status: {}",
                        name,
                        code
                    );
                }
                Err(e) => {
                    tracing::error!(
                        target: "taix_shell::service_manager",
                        "{} wait error: {}",
                        name,
                        e
                    );
                    let _ = child.kill().await;
                    let _ = child.wait().await;
                }
            }

            tokio::select! {
                _ = sleep(Duration::from_secs(5)) => {}
                _ = shutdown_rx.recv() => {
                    tracing::info!(
                        target: "taix_shell::service_manager",
                        "Shutdown requested during {} restart delay",
                        name
                    );
                    return RunOutcome::Shutdown;
                }
            }
            RunOutcome::Exited
        }
        _ = shutdown_rx.recv() => {
            tracing::info!(
                target: "taix_shell::service_manager",
                "Shutting down {}",
                name
            );
            let _ = child.kill().await;
            let _ = child.wait().await;
            RunOutcome::Shutdown
        }
    }
}

async fn wait_for_existing_process(
    pid: u32,
    shutdown_rx: &mut tokio::sync::broadcast::Receiver<()>,
    name: &str,
) -> RunOutcome {
    loop {
        if !crate::platform::is_process_alive(pid) {
            tracing::info!(
                target: "taix_shell::service_manager",
                "Existing {} (pid={}) has exited",
                name,
                pid
            );
            return RunOutcome::Exited;
        }

        tokio::select! {
            _ = sleep(Duration::from_secs(1)) => {}
            _ = shutdown_rx.recv() => {
                tracing::info!(
                    target: "taix_shell::service_manager",
                    "Shutdown requested while watching existing {}",
                    name
                );
                return RunOutcome::Shutdown;
            }
        }
    }
}

#[cfg(target_os = "windows")]
fn find_existing_process(exe_name: &str) -> Option<u32> {
    use windows::Win32::Foundation::CloseHandle;
    use windows::Win32::System::ProcessStatus::EnumProcesses;
    use windows::Win32::System::Threading::{
        OpenProcess, PROCESS_NAME_FORMAT, PROCESS_QUERY_LIMITED_INFORMATION, QueryFullProcessImageNameW,
    };
    use windows::core::PWSTR;

    let mut pids = [0u32; 1024];
    let mut needed = 0u32;

    // SAFETY: pids 是有效的栈数组，指针和长度均合法。
    unsafe {
        if EnumProcesses(pids.as_mut_ptr(), (pids.len() * 4) as u32, &mut needed).is_err() {
            return None;
        }
    }

    let count = ((needed / 4) as usize).min(pids.len());
    let current_pid = std::process::id();
    let exe_wide: Vec<u16> = exe_name.encode_utf16().collect();

    for &pid in &pids[..count] {
        if pid == 0 || pid == current_pid {
            continue;
        }

        // SAFETY: pid 来自 EnumProcesses，是合法的系统进程 ID。
        let handle = match unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) } {
            Ok(h) => h,
            Err(_) => continue,
        };

        let mut name_buf = [0u16; 260];
        let mut name_len = name_buf.len() as u32;
        // SAFETY: handle 是有效的进程句柄；name_buf 是有效的栈缓冲区。
        let name_result = unsafe {
            QueryFullProcessImageNameW(
                handle,
                PROCESS_NAME_FORMAT(0),
                PWSTR(name_buf.as_mut_ptr()),
                &mut name_len,
            )
        };
        let _ = unsafe { CloseHandle(handle) };

        if name_result.is_err() {
            continue;
        }

        let slice = &name_buf[..name_len as usize];
        let file_start = slice.iter().rposition(|&c| c == b'\\' as u16).map(|i| i + 1).unwrap_or(0);
        let file_name = &slice[file_start..];
        let file_name = if file_name.last() == Some(&0) {
            &file_name[..file_name.len() - 1]
        } else {
            file_name
        };

        if file_name == exe_wide.as_slice() {
            return Some(pid);
        }
    }

    None
}

#[cfg(not(target_os = "windows"))]
fn find_existing_process(_exe_name: &str) -> Option<u32> {
    None
}

fn check_startup_alive(child: &Child, name: &str) -> bool {
    let Some(pid) = child.id() else {
        tracing::warn!(
            target: "taix_shell::service_manager",
            "Could not get PID for {}, assuming startup failure",
            name
        );
        return false;
    };

    if crate::platform::is_process_alive(pid) {
        true
    } else {
        tracing::warn!(
            target: "taix_shell::service_manager",
            "{} (pid={}) exited within 2s, treating as startup failure",
            name,
            pid
        );
        false
    }
}

/// 首次失败等待 5 秒，之后每次增加 5 秒，最大 30 秒封顶。
/// 成功启动后自动重置。
struct Backoff {
    failures: u32,
}

impl Backoff {
    const BASE_SECS: u64 = 5;
    const MAX_SECS: u64 = 30;

    fn new() -> Self {
        Self { failures: 0 }
    }

    fn next_delay(&self) -> Duration {
        if self.failures == 0 {
            Duration::ZERO
        } else {
            let secs = Self::BASE_SECS.saturating_mul(self.failures as u64).min(Self::MAX_SECS);
            Duration::from_secs(secs)
        }
    }

    fn record_failure(&mut self) {
        self.failures = self.failures.saturating_add(1);
    }
}
