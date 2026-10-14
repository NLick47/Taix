use std::collections::HashMap;
use std::path::Path;

use serde::{Deserialize, Serialize};
use tokio::fs;

use crate::error::AppError;

/// 启用状态与配置的唯一权威存储；仅 server 写，shell 只读 enabled
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PluginRegistry {
    pub version: u32,
    /// 缺省 = 未启用
    #[serde(default)]
    pub enabled: HashMap<String, bool>,
    #[serde(default)]
    pub configs: HashMap<String, PluginConfig>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PluginConfig {
    pub version: u32,
    pub values: serde_json::Value,
}

impl Default for PluginConfig {
    fn default() -> Self {
        Self {
            version: 0,
            values: serde_json::Value::Object(serde_json::Map::new()),
        }
    }
}

impl Default for PluginRegistry {
    fn default() -> Self {
        Self {
            version: 1,
            enabled: HashMap::new(),
            configs: HashMap::new(),
        }
    }
}

pub fn registry_path(data_dir: &Path) -> std::path::PathBuf {
    data_dir.join("plugins").join("registry.json")
}

/// 不存在或损坏时回退默认；损坏时备份 .corrupt
pub async fn load_or_default(data_dir: &Path) -> Result<PluginRegistry, AppError> {
    let path = registry_path(data_dir);

    let mut content = match fs::read_to_string(&path).await {
        Ok(content) => content,
        Err(e) if e.kind() == std::io::ErrorKind::NotFound => {
            let registry = PluginRegistry::default();
            // 首次运行立即落盘
            persist(data_dir, &registry).await?;
            return Ok(registry);
        }
        Err(e) => return Err(AppError::Internal(format!("读取 registry 失败: {}", e))),
    };

    if content.starts_with('\u{FEFF}') {
        content.remove(0);
    }

    match serde_json::from_str(&content) {
        Ok(registry) => Ok(registry),
        Err(e) => {
            let corrupt = path.with_extension("json.corrupt");
            tracing::warn!(
                target: "taix_server::plugins",
                "registry.json 解析失败（{}），已备份至 {} 并使用默认值",
                e,
                corrupt.display()
            );
            let _ = fs::rename(&path, &corrupt).await;
            Ok(PluginRegistry::default())
        }
    }
}

/// 原子落盘；调用方负责串行化
pub async fn persist(data_dir: &Path, registry: &PluginRegistry) -> Result<(), AppError> {
    let path = registry_path(data_dir);
    if let Some(parent) = path.parent() {
        fs::create_dir_all(parent)
            .await
            .map_err(|e| AppError::Internal(format!("创建 plugins 数据目录失败: {}", e)))?;
    }

    let content = serde_json::to_string_pretty(registry)
        .map_err(|e| AppError::Internal(format!("序列化 registry 失败: {}", e)))?;

    let temp_path = path.with_extension(format!("tmp.{}", std::process::id()));
    fs::write(&temp_path, content)
        .await
        .map_err(|e| AppError::Internal(format!("写入 registry 临时文件失败: {}", e)))?;

    fs::rename(&temp_path, &path)
        .await
        .map_err(|e| {
            let _ = std::fs::remove_file(&temp_path);
            AppError::Internal(format!("替换 registry 失败: {}", e))
        })?;

    Ok(())
}
