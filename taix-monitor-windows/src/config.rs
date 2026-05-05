use std::path::Path;
use serde::{Deserialize, Serialize};

/// 运行时配置。配置文件是唯一来源，首次启动时若缺失会自动生成默认配置。
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct MonitorConfig {
    pub ignore_processes: Vec<String>,
}

impl Default for MonitorConfig {
    fn default() -> Self {
        Self {
            ignore_processes: vec![
                "Taix".to_string(),
                "Tai".to_string(),
                "SearchHost".to_string(),
                "Taskmgr".to_string(),
                "ApplicationFrameHost".to_string(),
                "StartMenuExperienceHost".to_string(),
                "ShellExperienceHost".to_string(),
                "OpenWith".to_string(),
                "Updater".to_string(),
                "LockApp".to_string(),
                "dwm".to_string(),
                "SystemSettingsAdminFlows".to_string(),
            ],
        }
    }
}

/// 加载配置。若 `config.json` 不存在或解析失败，自动生成默认配置并写入磁盘。
pub fn load_or_create(data_dir: Option<&Path>) -> MonitorConfig {
    if let Some(dir) = data_dir {
        let path = dir.join("config.json");
        if path.exists() {
            match std::fs::read_to_string(&path) {
                Ok(content) => match serde_json::from_str::<MonitorConfig>(&content) {
                    Ok(config) => {
                        tracing::info!("[Config] Loaded config from {:?}", path);
                        return config;
                    }
                    Err(e) => {
                        tracing::error!("[Config] Failed to parse config.json: {}", e);
                    }
                },
                Err(e) => {
                    tracing::error!("[Config] Failed to read config.json: {}", e);
                }
            }
        }

        // 配置文件不存在或损坏：生成默认配置并写入
        let default_config = MonitorConfig::default();
        match serde_json::to_string_pretty(&default_config) {
            Ok(json) => {
                if let Err(e) = std::fs::write(&path, json) {
                    tracing::warn!("[Config] Failed to write default config.json: {}", e);
                } else {
                    tracing::info!("[Config] Created default config at {:?}", path);
                }
            }
            Err(e) => {
                tracing::warn!("[Config] Failed to serialize default config: {}", e);
            }
        }
        return default_config;
    }

    // 没有 data_dir：仅内存中使用默认配置，不写入文件
    tracing::warn!("[Config] No data-dir provided, using default config in memory");
    MonitorConfig::default()
}
