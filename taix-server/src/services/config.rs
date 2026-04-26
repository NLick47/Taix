use std::path::Path;
use tokio::fs;
use tracing::{debug, info, warn};

use crate::error::AppError;
use crate::models::config::ConfigModel;

pub struct ConfigService {
    path: std::path::PathBuf,
}

impl ConfigService {
    pub fn new(data_dir: &Path) -> Self {
        let path = data_dir.join("AppConfig.json");
        Self { path }
    }

    pub async fn load(&self) -> Result<ConfigModel, AppError> {
        debug!("load config from {:?}", self.path);
        if !self.path.exists() {
            info!("config not found, create default");
            let config = ConfigModel::default();
            self.save(&config).await?;
            return Ok(config);
        }

        let mut content = fs::read_to_string(&self.path).await.map_err(|e| {
            AppError::Internal(format!("Failed to read config file: {}", e))
        })?;

        // Windows 工具生成的 UTF-8 可能带 BOM，去掉避免解析失败
        if content.starts_with('\u{FEFF}') {
            content.remove(0);
        }

        let config: ConfigModel = serde_json::from_str(&content).map_err(|e| {
            warn!("parse config failed: {}", e);
            AppError::Internal(format!("Failed to parse config file: {}", e))
        })?;

        Ok(config)
    }

    pub async fn save(&self, config: &ConfigModel) -> Result<(), AppError> {
        info!("save config to {:?}", self.path);
        if let Some(parent) = self.path.parent() {
            fs::create_dir_all(parent).await.map_err(|e| {
                AppError::Internal(format!("Failed to create config dir: {}", e))
            })?;
        }

        let content = serde_json::to_string_pretty(config).map_err(|e| {
            AppError::Internal(format!("Failed to serialize config: {}", e))
        })?;

        fs::write(&self.path, content).await.map_err(|e| {
            AppError::Internal(format!("Failed to write config file: {}", e))
        })?;

        Ok(())
    }
}
