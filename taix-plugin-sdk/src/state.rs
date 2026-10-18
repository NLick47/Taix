use anyhow::Context;
use serde::{de::DeserializeOwned, Serialize};
use std::io::Write;
use std::path::PathBuf;

/// 损坏时备份 .corrupt 并回退默认
#[derive(Clone)]
pub struct StateStore {
    path: Option<PathBuf>,
}

impl StateStore {
    pub fn new(data_dir: Option<&std::path::Path>, plugin_id: &str) -> Self {
        let path = data_dir.map(|d| d.join("plugins").join(plugin_id).join("state.json"));
        if path.is_none() {
            tracing::warn!(
                target: "taix_plugin_sdk",
                "no data-dir configured; state persistence disabled"
            );
        }
        Self { path }
    }

    pub fn available(&self) -> bool {
        self.path.is_some()
    }

    fn cursor_path(&self) -> Option<PathBuf> {
        self.path
            .as_ref()
            .map(|path| path.with_file_name("sse_cursor"))
    }

    /// 已处理到的 SSE 指令 seq；重启后从它继续，避免旧指令重放
    pub fn load_cursor(&self) -> u64 {
        let Some(path) = self.cursor_path() else {
            return 0;
        };
        std::fs::read_to_string(path)
            .ok()
            .and_then(|text| text.trim().parse::<u64>().ok())
            .unwrap_or(0)
    }

    pub fn save_cursor(&self, seq: u64) -> anyhow::Result<()> {
        let Some(path) = self.cursor_path() else {
            return Ok(());
        };
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent)
                .with_context(|| format!("创建插件游标目录 {}", parent.display()))?;
        }
        let tmp = path.with_extension(format!("tmp.{}", std::process::id()));
        {
            let mut f = std::fs::File::create(&tmp)
                .with_context(|| format!("创建临时游标文件 {}", tmp.display()))?;
            f.write_all(seq.to_string().as_bytes())?;
            f.flush()?;
        }
        std::fs::rename(&tmp, &path)
            .with_context(|| format!("替换游标文件 {}", path.display()))?;
        Ok(())
    }

    pub fn load<T: DeserializeOwned>(&self) -> Option<T> {
        let path = self.path.as_ref()?;
        let mut content = std::fs::read_to_string(path).ok()?;
        if content.starts_with('\u{FEFF}') {
            content.remove(0);
        }
        match serde_json::from_str(&content) {
            Ok(v) => Some(v),
            Err(e) => {
                tracing::warn!(
                    target: "taix_plugin_sdk",
                    "state.json 解析失败（{}），备份为 .corrupt 并回退默认",
                    e
                );
                let _ = std::fs::rename(path, path.with_extension("json.corrupt"));
                None
            }
        }
    }

    pub fn save<T: Serialize>(&self, value: &T) -> anyhow::Result<()> {
        let path = self
            .path
            .as_ref()
            .context("state persistence disabled (no data-dir)")?;
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent)
                .with_context(|| format!("创建插件数据目录 {}", parent.display()))?;
        }
        let content = serde_json::to_string_pretty(value)?;
        let tmp = path.with_extension(format!("tmp.{}", std::process::id()));
        {
            let mut f = std::fs::File::create(&tmp)
                .with_context(|| format!("创建临时状态文件 {}", tmp.display()))?;
            f.write_all(content.as_bytes())?;
            f.flush()?;
        }
        std::fs::rename(&tmp, path)
            .with_context(|| format!("替换状态文件 {}", path.display()))?;
        Ok(())
    }
}

// 单实例锁

#[cfg(target_os = "windows")]
pub struct SingleInstance {
    _handle: windows_sys::Win32::Foundation::HANDLE,
}

#[cfg(target_os = "windows")]
impl SingleInstance {
    pub fn acquire(plugin_id: &str) -> anyhow::Result<Self> {
        use windows_sys::Win32::Foundation::{CloseHandle, GetLastError, ERROR_ALREADY_EXISTS};
        use windows_sys::Win32::System::Threading::CreateMutexW;

        // Global\ 额外要求权限，用 Local\ 即可
        let name = format!("Local\\TaixPluginMutex-{}\0", plugin_id);
        let name_wide: Vec<u16> = name.encode_utf16().collect();
        let handle = unsafe { CreateMutexW(std::ptr::null(), 0, name_wide.as_ptr()) };
        if handle.is_null() {
            anyhow::bail!("CreateMutexW failed for {}", plugin_id);
        }
        if unsafe { GetLastError() } == ERROR_ALREADY_EXISTS {
            unsafe { CloseHandle(handle) };
            anyhow::bail!("plugin '{}' 已有实例在运行", plugin_id);
        }
        Ok(Self { _handle: handle })
    }
}

#[cfg(target_os = "windows")]
impl Drop for SingleInstance {
    fn drop(&mut self) {
        use windows_sys::Win32::Foundation::CloseHandle;
        unsafe { CloseHandle(self._handle) };
    }
}

#[cfg(not(target_os = "windows"))]
pub struct SingleInstance;

#[cfg(not(target_os = "windows"))]
impl SingleInstance {
    pub fn acquire(_plugin_id: &str) -> anyhow::Result<Self> {
        // macOS/Linux 由 shell 侧进程名判重保证
        Ok(Self)
    }
}

#[cfg(test)]
mod tests {
    use super::StateStore;

    fn temp_store(tag: &str) -> (std::path::PathBuf, StateStore) {
        let root = std::env::temp_dir().join(format!(
            "taix-sdk-cursor-{}-{}",
            tag,
            std::process::id()
        ));
        let _ = std::fs::remove_dir_all(&root);
        std::fs::create_dir_all(&root).unwrap();
        let store = StateStore::new(Some(&root), "demo");
        (root, store)
    }

    #[test]
    fn cursor_roundtrip_and_missing_defaults_to_zero() {
        let (root, store) = temp_store("roundtrip");
        assert_eq!(store.load_cursor(), 0, "首次启动游标为 0");
        store.save_cursor(42).unwrap();
        assert_eq!(store.load_cursor(), 42);
        store.save_cursor(7).unwrap();
        assert_eq!(store.load_cursor(), 7, "游标以最后一次写入为准");
        let _ = std::fs::remove_dir_all(&root);
    }

    #[test]
    fn cursor_without_data_dir_is_disabled() {
        let store = StateStore::new(None, "demo");
        assert_eq!(store.load_cursor(), 0);
        assert!(store.save_cursor(9).is_ok(), "无 data-dir 时静默不持久化");
    }
}
