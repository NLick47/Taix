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
