use std::path::{Path, PathBuf};
use std::process::Stdio;
use std::sync::atomic::{AtomicBool, Ordering};
use std::time::Duration;

#[derive(Clone)]
pub struct ServiceManager {
    data_dir: Option<PathBuf>,
}

impl ServiceManager {
    const MAX_MISSING_RETRIES: u32 = 6;

    pub fn new(data_dir: Option<PathBuf>) -> Self {
        Self { data_dir }
    }

    pub fn run(self, shutdown: std::sync::Arc<AtomicBool>) {
        let self_clone = self.clone();
        let shutdown_clone = shutdown.clone();
        let monitor_handle = std::thread::spawn(move || {
            self_clone.supervise(
                crate::constants::MONITOR_EXE_NAME,
                &["run"],
                shutdown_clone,
            );
        });

        self.supervise(
            crate::constants::SERVER_EXE_NAME,
            &[],
            shutdown,
        );

        let _ = monitor_handle.join();
    }

    fn supervise(
        &self,
        exe_name: &'static str,
        extra_args: &'static [&'static str],
        shutdown: std::sync::Arc<AtomicBool>,
    ) {
        #[cfg(target_os = "windows")]
        let job = crate::platform::job_object::JobObject::new();

        let mut backoff = Backoff::new();
        let mut missing_retries: u32 = 0;

        loop {
            if shutdown.load(Ordering::Relaxed) {
                return;
            }

            let exe_path = match resolve_exe_path(exe_name) {
                Some(p) => p,
                None => {
                    missing_retries += 1;
                    if missing_retries > Self::MAX_MISSING_RETRIES {
                        return;
                    }
                    let delay = backoff.next_delay().max(Duration::from_secs(5));
                    std::thread::sleep(delay);
                    continue;
                }
            };

            if missing_retries > 0 {
                missing_retries = 0;
                backoff = Backoff::new();
            }

            let delay = backoff.next_delay();
            if delay > Duration::ZERO {
                let start = std::time::Instant::now();
                while start.elapsed() < delay {
                    if shutdown.load(Ordering::Relaxed) {
                        return;
                    }
                    std::thread::sleep(Duration::from_millis(500));
                }
            }

            #[cfg(target_os = "windows")]
            if let Some(pid) = find_existing_process(exe_name) {
                loop {
                    if shutdown.load(Ordering::Relaxed) {
                        return;
                    }
                    if !crate::platform::is_process_alive(pid) {
                        break;
                    }
                    std::thread::sleep(Duration::from_secs(1));
                }
                continue;
            }

            let args = build_args(extra_args, self.data_dir.as_ref());
            let mut child = match spawn_process(&exe_path, &args) {
                Ok(c) => c,
                Err(_) => {
                    backoff.record_failure();
                    continue;
                }
            };

            #[cfg(target_os = "windows")]
            if let (pid, Ok(ref job)) = (child.id(), &job) {
                let _ = job.assign_process(pid);
            }

            std::thread::sleep(Duration::from_secs(2));
            let pid = child.id();
            if !crate::platform::is_process_alive(pid) {
                backoff.record_failure();
                continue;
            }

            loop {
                if shutdown.load(Ordering::Relaxed) {
                    let _ = child.kill();
                    let _ = child.wait();
                    return;
                }
                match child.try_wait() {
                    Ok(Some(_)) => break,
                    Ok(None) => {
                        std::thread::sleep(Duration::from_secs(1));
                    }
                    Err(_) => break,
                }
            }

            backoff.record_failure();

            let start = std::time::Instant::now();
            while start.elapsed() < Duration::from_secs(5) {
                if shutdown.load(Ordering::Relaxed) {
                    return;
                }
                std::thread::sleep(Duration::from_millis(500));
            }
        }
    }
}

fn build_args(extra: &[&str], data_dir: Option<&PathBuf>) -> Vec<String> {
    let mut args: Vec<String> = Vec::with_capacity(4);
    args.extend(extra.iter().map(|&s| s.to_owned()));
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
fn find_existing_process(_exe_name: &str) -> Option<u32> {
    None
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
