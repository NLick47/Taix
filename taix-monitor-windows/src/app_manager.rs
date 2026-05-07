use crate::models::{AppInfo, AppType};
use crate::win32::process::{get_file_description, get_process_exe_path, get_process_name};
use crate::win32::window::{enum_child_windows, extract_icon_to_png, get_window_class_name};
use dashmap::DashMap;
use std::hash::Hash;
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::time::{Duration, Instant};
use tracing::{debug, error};
use windows::Win32::Foundation::HWND;

const MAX_CACHE_SIZE: usize = 500;
const CACHE_TTL_SECS: u64 = 3600;

static SYS_CLASS_NAMES: &[&str] = &[
    "Progman", "WorkerW", "Shell_TrayWnd", "XamlExplorerHostIslandWindow",
    "TopLevelWindowForOverflowXamlIsland", "Shell_InputSwitchTopLevelWindow",
    "LockScreenControllerProxyWindow", "ForegroundStaging", "DV2ControlHost", "Button",
];
static SYS_PROCESSES: &[&str] = &["ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "LockApp"];

#[derive(Clone)]
struct CachedApp {
    info: AppInfo,
    time: Instant,
}


#[derive(Debug, Clone, Eq, PartialEq, Hash)]
struct CacheKey {
    process: String,
    app_type: AppType,
    path: String,
}

impl CacheKey {
    fn new(process: &str, app_type: AppType, path: &str) -> Self {
        Self {
            process: process.to_lowercase(),
            app_type,
            path: path.to_lowercase(),
        }
    }
}

pub struct AppManager {
    cache: Arc<DashMap<CacheKey, CachedApp>>,
    data_dir: PathBuf,
}

#[derive(Debug)]
pub enum AppInfoError {
    ProcessNotFound { _pid: u32 },
}

impl AppManager {
    pub fn new(data_dir: Option<PathBuf>) -> Self {
        let data_dir = data_dir
            .or_else(|| std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from))
            .unwrap_or_else(|| {
                std::env::current_exe()
                    .ok()
                    .and_then(|p| p.parent().map(|d| d.to_path_buf()))
                    .unwrap_or_else(|| PathBuf::from("."))
            });
        Self {
            cache: Arc::new(DashMap::with_capacity(MAX_CACHE_SIZE)),
            data_dir,
        }
    }

    pub fn get_app_info(&self, hwnd: HWND) -> Result<AppInfo, AppInfoError> {
        let (_tid, pid) = crate::win32::window::get_window_thread_process_id(hwnd);

        let name = match get_process_name(pid) {
            Some(n) => n,
            None => {
                error!(target: "app_manager", "Failed to get process name for hwnd={:?}, pid={}", hwnd, pid);
                return Err(AppInfoError::ProcessNotFound { _pid: pid });
            }
        };

        let (exe_path, base_app_type, process_name) = if name == "ApplicationFrameHost" {
            let path = get_uwp_path(hwnd, pid).or_else(|| {
                let cls = get_window_class_name(hwnd);
                if cls.is_empty() { None } else { Some(cls) }
            });
            let proc = path.as_deref()
                .and_then(|p| Path::new(p).file_stem())
                .map(|s| s.to_string_lossy().to_string())
                .unwrap_or_else(|| {
                    let cls = get_window_class_name(hwnd);
                    if !cls.is_empty() {
                        cls
                    } else {
                        let title = crate::win32::window::get_window_text(hwnd);
                        if !title.is_empty() { title } else { name.clone() }
                    }
                });
            (path, AppType::Uwp, proc)
        } else {
            (get_process_exe_path(pid), AppType::Win32, name.clone())
        };

        let app_type = if is_system(&process_name, hwnd) {
            AppType::SystemComponent
        } else {
            base_app_type
        };

        let path_key = exe_path.as_deref().unwrap_or("");
        let cache_key = CacheKey::new(&process_name, app_type, path_key);

        // DashMap 提供真正的并发无锁读
        if let Some(entry) = self.cache.get(&cache_key) {
            if entry.time.elapsed() < Duration::from_secs(CACHE_TTL_SECS) {
                debug!(target: "app_manager", "Cache hit for key={:?}", cache_key);
                return Ok(entry.info.clone());
            }
            // TTL 过期：删除旧项，继续走新建逻辑
            drop(entry);
            self.cache.remove(&cache_key);
        }

        // 同步提取文件描述（通常很快）
        let description = exe_path
            .as_deref()
            .and_then(get_file_description)
            .unwrap_or_default();

        let info = AppInfo {
            process: process_name.to_string(),
            description,
            executable_path: exe_path.clone().unwrap_or_default(),
            icon_path: String::new(),
            app_type,
        };

        // 插入缓存（DashMap 自动处理并发写）
        self.evict_if_needed();
        self.cache.insert(cache_key.clone(), CachedApp {
            info: info.clone(),
            time: Instant::now(),
        });

        // 后台异步提取图标，完成后回填缓存
        let cache = Arc::clone(&self.cache);
        let data_dir = self.data_dir.clone();
        let exe_path_for_icon = exe_path.clone();
        let process_name_for_icon = process_name.clone();
        let cache_key_for_fill = cache_key.clone();
        std::thread::spawn(move || {
            let icon_path = exe_path_for_icon
                .as_deref()
                .and_then(|p| extract_icon(&data_dir, p, &process_name_for_icon))
                .unwrap_or_default();

            if let Some(mut entry) = cache.get_mut(&cache_key_for_fill) {
                entry.info.icon_path = icon_path;
                entry.time = Instant::now();
                debug!(
                    target: "app_manager",
                    "Async filled icon for key={:?}, icon_len={}",
                    cache_key_for_fill,
                    entry.info.icon_path.len()
                );
            }
        });

        debug!(target: "app_manager", "Resolved app: name={}, type={}, path={}",
            info.process, info.app_type, info.executable_path);

        Ok(info)
    }

    /// 当缓存超过阈值时，淘汰最老的条目。
    fn evict_if_needed(&self) {
        const CLEANUP_THRESHOLD: usize = MAX_CACHE_SIZE + MAX_CACHE_SIZE / 2;
        let len = self.cache.len();
        if len <= CLEANUP_THRESHOLD {
            return;
        }


        let to_remove = len - MAX_CACHE_SIZE;
        let keys: Vec<_> = self.cache.iter()
            .map(|e| (e.key().clone(), e.value().time))
            .collect();

        let mut sorted: Vec<_> = keys.into_iter().map(|(k, t)| (k, t)).collect();
        sorted.sort_by(|a, b| a.1.cmp(&b.1));

        for (key, _) in sorted.into_iter().take(to_remove) {
            self.cache.remove(&key);
        }
        debug!(target: "app_manager", "Cache LRU evicted {} entries, remaining={}", to_remove, self.cache.len());
    }
}

fn extract_icon(data_dir: &Path, exe_path: &str, process_name: &str) -> Option<String> {
    let icon_dir = data_dir.join("AppIcons");
    std::fs::create_dir_all(&icon_dir).ok()?;

    let safe_name: String = process_name.chars().filter(|c| !r#"<>:"/\?*"#.contains(*c)).collect();
    let path_hash = format!("{:08x}", crc32fast::hash(exe_path.as_bytes()));
    let icon_path = icon_dir.join(format!("{}_{}.png", safe_name, path_hash));

    if icon_path.exists() {
        return relative_path(&icon_path, data_dir);
    }

    if let Err(e) = extract_icon_to_png(exe_path, &icon_path) {
        tracing::warn!(target: "app_manager", "Failed to extract icon for {}: {}", process_name, e);
        return None;
    }
    relative_path(&icon_path, data_dir)
}

fn get_uwp_path(hwnd: HWND, owner_pid: u32) -> Option<String> {
    for child in enum_child_windows(hwnd) {
        let (_, pid) = crate::win32::window::get_window_thread_process_id(child);
        if pid != owner_pid {
            return get_process_exe_path(pid);
        }
    }
    None
}

fn relative_path(path: &std::path::Path, base: &std::path::Path) -> Option<String> {
    path.strip_prefix(base)
        .ok()
        .map(|p| p.to_string_lossy().to_string())
}

fn is_system(name: &str, hwnd: HWND) -> bool {
    if name.eq_ignore_ascii_case("explorer") {
        let cls = get_window_class_name(hwnd);
        if SYS_CLASS_NAMES.iter().any(|&s| s.eq_ignore_ascii_case(&cls)) {
            return true;
        }
        if cls.eq_ignore_ascii_case("CabinetWClass") {
            return false;
        }
        return crate::win32::window::get_window_text(hwnd).is_empty();
    }
    SYS_PROCESSES.iter().any(|&s| s.eq_ignore_ascii_case(name))
}
