use std::path::{Path, PathBuf};
use std::process::Stdio;
use std::time::Duration;

use crate::constants::default_data_dir;
use crate::shutdown::Shutdown;

#[derive(Debug, Clone)]
pub struct MonitorConfig {
    pub inactive_threshold: i32,
    pub max_sound_duration: i32,
    pub sleep_watch: bool,
}

impl Default for MonitorConfig {
    fn default() -> Self {
        Self {
            inactive_threshold: 15,
            max_sound_duration: 120,
            sleep_watch: true,
        }
    }
}

fn build_monitor_args(config: &MonitorConfig) -> Vec<String> {
    vec![
        "run".to_owned(),
        "--inactive-threshold".to_owned(),
        config.inactive_threshold.to_string(),
        "--max-sound-duration".to_owned(),
        config.max_sound_duration.to_string(),
        "--sleep-watch".to_owned(),
        config.sleep_watch.to_string(),
    ]
}

#[derive(Clone)]
pub struct ServiceManager {
    data_dir: PathBuf,
}

impl ServiceManager {
    const MAX_MISSING_RETRIES: u32 = 6;

    pub fn new(data_dir: Option<PathBuf>) -> Self {
        Self {
            data_dir: data_dir.unwrap_or_else(default_data_dir),
        }
    }

    pub fn run(self, shutdown: Shutdown) {
        let monitor_config = crate::config::load_monitor_config(&self.data_dir);
        let monitor_args = build_monitor_args(&monitor_config);

        tracing::info!(
            target: "taix_shell::service",
            "service supervisor starting data_dir={:?} monitor(inactive_threshold={} max_sound_duration={} sleep_watch={})",
            self.data_dir,
            monitor_config.inactive_threshold,
            monitor_config.max_sound_duration,
            monitor_config.sleep_watch
        );

        let self_clone = self.clone();
        let shutdown_clone = shutdown.clone();
        let monitor_handle = std::thread::spawn(move || {
            self_clone.supervise(
                crate::constants::MONITOR_EXE_NAME,
                &monitor_args,
                shutdown_clone,
            );
        });
        tracing::debug!(target: "taix_shell::service", "monitor supervisor thread spawned");

        self.supervise(
            crate::constants::SERVER_EXE_NAME,
            &[],
            shutdown,
        );

        let _ = monitor_handle.join();
        tracing::info!(target: "taix_shell::service", "service supervisor exiting; both supervise loops done");
    }

    fn supervise(
        &self,
        exe_name: &'static str,
        args: &[String],
        shutdown: Shutdown,
    ) {
        #[cfg(target_os = "windows")]
        let job = crate::platform::job_object::JobObject::new();

        #[cfg(target_os = "windows")]
        match &job {
            Ok(_) => tracing::debug!(target: "taix_shell::service", "{}: job object created", exe_name),
            Err(e) => tracing::warn!(
                target: "taix_shell::service",
                "{}: job object creation failed ({:#}); children will not be killed with this shell",
                exe_name, e
            ),
        }

        let mut backoff = Backoff::new();
        let mut missing_retries: u32 = 0;

        loop {
            if shutdown.is_requested() {
                tracing::info!(target: "taix_shell::service", "{}: shutdown observed; supervise loop exiting", exe_name);
                return;
            }

            let exe_path = match resolve_exe_path(exe_name) {
                Some(p) => p,
                None => {
                    missing_retries += 1;
                    if missing_retries > Self::MAX_MISSING_RETRIES {
                        tracing::error!(
                            target: "taix_shell::service",
                            "{}: executable not found after {} attempts; giving up supervision",
                            exe_name, Self::MAX_MISSING_RETRIES
                        );
                        return;
                    }
                    let delay = backoff.next_delay().max(Duration::from_secs(5));
                    tracing::warn!(
                        target: "taix_shell::service",
                        "{}: executable not found (attempt {}/{}), retrying in {:?}",
                        exe_name, missing_retries, Self::MAX_MISSING_RETRIES, delay
                    );
                    if !shutdown.wait_for(delay) {
                        tracing::info!(
                            target: "taix_shell::service",
                            "{}: shutdown during not-found backoff; supervise loop exiting",
                            exe_name
                        );
                        return;
                    }
                    continue;
                }
            };

            if missing_retries > 0 {
                tracing::info!(
                    target: "taix_shell::service",
                    "{}: executable found again after {} misses; resetting backoff",
                    exe_name, missing_retries
                );
                missing_retries = 0;
                backoff = Backoff::new();
            }

            let delay = backoff.next_delay();
            if delay > Duration::ZERO {
                tracing::info!(
                    target: "taix_shell::service",
                    "{}: waiting {:?} before respawn (failures={})",
                    exe_name, delay, backoff.failures
                );
                if !shutdown.wait_for(delay) {
                    tracing::info!(
                        target: "taix_shell::service",
                        "{}: shutdown during respawn backoff; supervise loop exiting",
                        exe_name
                    );
                    return;
                }
            }

            // 已经有一个同名进程在跑 —— 通常是上一个 shell 崩溃后残留的 —— 等它自己
            // 退出后再重新拉起。两端都必须等：残留的 server 占着端口/单实例资源，
            // 新起的那个起来就挂，监督循环会陷入无意义的重启风暴。
            if let Some(pid) = find_existing_process(exe_name) {
                tracing::warn!(
                    target: "taix_shell::service",
                    "{}: found a pre-existing pid={} (likely orphaned by a previous shell); waiting for it to exit",
                    exe_name, pid
                );
                #[cfg(target_os = "windows")]
                if let Ok(job) = &job {
                    match job.assign_process(pid) {
                        Ok(()) => tracing::info!(
                            target: "taix_shell::service",
                            "{}: adopted pid={} into this shell's job object",
                            exe_name, pid
                        ),
                        Err(e) => tracing::warn!(
                            target: "taix_shell::service",
                            "{}: failed to adopt pid={} into job object ({:#}); it may outlive this shell",
                            exe_name, pid, e
                        ),
                    }
                }
                loop {
                    if shutdown.is_requested() {
                        tracing::warn!(
                            target: "taix_shell::service",
                            "{}: shutdown while waiting for pre-existing pid={}; leaving it running (it is not ours to kill)",
                            exe_name, pid
                        );
                        return;
                    }
                    if !crate::platform::is_process_alive(pid) {
                        tracing::info!(target: "taix_shell::service", "{}: pre-existing pid={} exited", exe_name, pid);
                        break;
                    }
                    std::thread::sleep(Duration::from_secs(1));
                }
                continue;
            }

            let args = build_args(args);
            let mut child = match spawn_process(&exe_path, &args) {
                Ok(c) => c,
                Err(e) => {
                    tracing::warn!(
                        target: "taix_shell::service",
                        "{}: spawn failed exe={:?} error={}",
                        exe_name, exe_path, e
                    );
                    backoff.record_failure();
                    continue;
                }
            };
            let pid = child.id();
            tracing::info!(
                target: "taix_shell::service",
                "{}: spawned pid={} exe={:?} args={:?}",
                exe_name, pid, exe_path, args
            );

            #[cfg(target_os = "windows")]
            if let Ok(ref job) = &job {
                match job.assign_process(pid) {
                    Ok(()) => tracing::debug!(
                        target: "taix_shell::service",
                        "{}: pid={} assigned to job object",
                        exe_name, pid
                    ),
                    Err(e) => tracing::warn!(
                        target: "taix_shell::service",
                        "{}: failed to assign pid={} to job object ({:#})",
                        exe_name, pid, e
                    ),
                }
            }

            // 给子进程一点启动时间再确认它没立刻挂掉
            if !shutdown.wait_for(Duration::from_secs(2)) {
                tracing::info!(
                    target: "taix_shell::service",
                    "{}: shutdown during startup grace period; killing pid={}",
                    exe_name, pid
                );
                let _ = child.kill();
                let _ = child.wait();
                return;
            }
            if !crate::platform::is_process_alive(pid) {
                tracing::warn!(
                    target: "taix_shell::service",
                    "{}: pid={} died within the 2s startup grace period; will retry",
                    exe_name, pid
                );
                backoff.record_failure();
                continue;
            }
            tracing::debug!(target: "taix_shell::service", "{}: pid={} alive after grace period", exe_name, pid);

            let started = std::time::Instant::now();
            loop {
                if shutdown.is_requested() {
                    tracing::info!(
                        target: "taix_shell::service",
                        "{}: shutdown observed; killing pid={} (uptime {:?})",
                        exe_name, pid, started.elapsed()
                    );
                    let _ = child.kill();
                    let _ = child.wait();
                    return;
                }
                match child.try_wait() {
                    Ok(Some(status)) => {
                        tracing::warn!(
                            target: "taix_shell::service",
                            "{}: pid={} exited ({} uptime={:?})",
                            exe_name, pid, describe_status(&status), started.elapsed()
                        );
                        break;
                    }
                    Ok(None) => {
                        std::thread::sleep(Duration::from_secs(1));
                    }
                    Err(e) => {
                        tracing::warn!(
                            target: "taix_shell::service",
                            "{}: try_wait failed for pid={} ({}); assuming it is gone",
                            exe_name, pid, e
                        );
                        break;
                    }
                }
            }

            backoff.record_failure();
            tracing::info!(
                target: "taix_shell::service",
                "{}: failure #{} recorded; restarting soon",
                exe_name, backoff.failures
            );

            if !shutdown.wait_for(Duration::from_secs(5)) {
                tracing::info!(
                    target: "taix_shell::service",
                    "{}: shutdown during post-exit delay; supervise loop exiting",
                    exe_name
                );
                return;
            }
        }
    }
}

/// 把 `ExitStatus` 拼成可读文本。Unix 下被信号杀死时 `code()` 返回 `None` ——
/// 这正是区分「自己崩了」和「被 shell 杀了」的关键信息。
fn describe_status(status: &std::process::ExitStatus) -> String {
    match status.code() {
        Some(code) => format!("exit code {code}"),
        None => "terminated by signal".to_owned(),
    }
}

fn build_args(args: &[String]) -> Vec<String> {
    args.to_vec()
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

fn spawn_process(exe: &Path, args: &[String]) -> std::io::Result<std::process::Child> {
    std::process::Command::new(exe)
        .current_dir(exe.parent().unwrap_or_else(|| Path::new(".")))
        .args(args)
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null())
        .spawn()
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

    unsafe {
        if EnumProcesses(pids.as_mut_ptr(), (pids.len() * std::mem::size_of::<u32>()) as u32, &mut needed).is_err() {
            return None;
        }
    }

    let count = ((needed / std::mem::size_of::<u32>() as u32) as usize).min(pids.len());
    let current_pid = std::process::id();

    let exe_bytes = exe_name.as_bytes();
    let mut exe_wide = [0u16; 64];
    for (i, &b) in exe_bytes.iter().enumerate() {
        exe_wide[i] = b as u16;
    }
    let exe_wide = &exe_wide[..exe_bytes.len()];

    for &pid in &pids[..count] {
        if pid == 0 || pid == current_pid {
            continue;
        }

        let handle = match unsafe { OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid) } {
            Ok(h) => h,
            Err(_) => continue,
        };

        let mut name_buf = [0u16; 260];
        let mut name_len = name_buf.len() as u32;
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

        if file_name == exe_wide {
            return Some(pid);
        }
    }

    None
}

#[cfg(not(target_os = "windows"))]
fn find_existing_process(exe_name: &str) -> Option<u32> {
    use std::process::Command;

    let output = Command::new("pgrep")
        .args(["-x", exe_name])
        .output()
        .ok()?;

    if output.status.success() {
        String::from_utf8_lossy(&output.stdout)
            .lines()
            .next()
            .and_then(|s| s.parse::<u32>().ok())
    } else {
        None
    }
}

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
