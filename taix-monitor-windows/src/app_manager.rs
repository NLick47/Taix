use crate::models::{AppInfo, AppType};
use crate::win32::process::{get_file_description, get_process_exe_path, get_process_name};
use crate::win32::window::{enum_child_windows, extract_icon_to_png, get_window_class_name};
use parking_lot::Mutex;
use std::collections::HashMap;
use std::hash::Hasher;
use std::path::{Path, PathBuf};
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

pub struct AppManager {
    cache: Mutex<HashMap<String, CachedApp>>,
    data_dir: PathBuf,
}

impl AppManager {
    pub fn new(data_dir: Option<PathBuf>) -> Self {
        let data_dir = data_dir
            .or_else(|| std::env::var("TAIX_DATA_DIR").ok().map(PathBuf::from))
            .unwrap_or_else(|| {
                std::env::current_exe()
                    .ok()
                    .and_then(|p| p.parent().map(|d| d.join("Data")))
                    .unwrap_or_else(|| PathBuf::from("Data"))
            });
        Self {
            cache: Mutex::new(HashMap::new()),
            data_dir,
        }
    }

    pub fn get_app_info(&self, hwnd: HWND) -> AppInfo {
        let hwnd_ptr = hwnd.0;
        let (_tid, pid) = crate::win32::window::get_window_thread_process_id(hwnd);

        let name = match get_process_name(pid) {
            Some(n) => n,
            None => {
                error!("[AppManager] Failed to get process name for hwnd={:?}, pid={}", hwnd, pid);
                return AppInfo::empty();
            }
        };

        let (exe_path, base_app_type, process_name) = if name == "ApplicationFrameHost" {
            let path = get_uwp_path(hwnd, pid);
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
        let cache_key = format!("{}:{:?}:{}", process_name, app_type, path_key);

        {
            let mut cache = self.cache.lock();
            if let Some(cached) = cache.get_mut(&cache_key) {
                if cached.time.elapsed() < Duration::from_secs(CACHE_TTL_SECS) {
                    cached.time = Instant::now();
                    debug!("[AppManager] Cache hit for key={}", cache_key);
                    return cached.info.clone();
                }
            }
        }

        let description = exe_path.as_deref().and_then(get_file_description).unwrap_or_default();
        let icon_path = exe_path.as_deref().and_then(|p| self.extract_icon(p, &process_name)).unwrap_or_default();

        let info = AppInfo {
            window_handle: hwnd_ptr as isize,
            pid,
            process: process_name.to_string(),
            description,
            executable_path: exe_path.unwrap_or_default(),
            icon_path,
            app_type,
        };

        {
            let mut cache = self.cache.lock();
            if cache.len() > MAX_CACHE_SIZE {
                let cutoff = Instant::now() - Duration::from_secs(CACHE_TTL_SECS);
                let to_remove: Vec<String> = cache
                    .iter()
                    .filter(|(_, v)| v.time < cutoff)
                    .map(|(k, _)| k.clone())
                    .collect();
                let removed = to_remove.len();
                for k in to_remove {
                    cache.remove(&k);
                }
                debug!("[AppManager] Cache cleaned {} entries, remaining={}", removed, cache.len());

                if cache.len() > MAX_CACHE_SIZE {
                    let mut items: Vec<_> = cache.iter().map(|(k, v)| (k.clone(), v.time)).collect();
                    items.sort_by(|a, b| a.1.cmp(&b.1));
                    let to_remove = cache.len() - MAX_CACHE_SIZE;
                    for i in 0..to_remove {
                        cache.remove(&items[i].0);
                    }
                    debug!("[AppManager] Cache LRU evicted {} entries, remaining={}", to_remove, cache.len());
                }
            }
            cache.insert(cache_key, CachedApp { info: info.clone(), time: Instant::now() });
        }

        debug!("[AppManager] Resolved app: name={}, type={}, path={}, icon={}, pid={}",
            info.process, info.app_type, info.executable_path, info.icon_path, info.pid);

        info
    }

    fn extract_icon(&self, exe_path: &str, process_name: &str) -> Option<String> {
        let icon_dir = self.data_dir.join("AppIcons");
        std::fs::create_dir_all(&icon_dir).ok()?;

        let safe_name: String = process_name.chars().filter(|c| !r#"<>:"/\|?*"#.contains(*c)).collect();

        let mut hasher = std::collections::hash_map::DefaultHasher::new();
        std::hash::Hash::hash(exe_path, &mut hasher);
        let path_hash = format!("{:08x}", hasher.finish());

        let icon_path = icon_dir.join(format!("{}_{}.png", safe_name, path_hash));

        if icon_path.exists() {
            return relative_path(&icon_path, &self.data_dir);
        }

        if let Err(e) = extract_icon_to_png(exe_path, &icon_path) {
            tracing::warn!("[AppManager] Failed to extract icon for {}: {}", process_name, e);
            return None;
        }
        relative_path(&icon_path, &self.data_dir)
    }
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