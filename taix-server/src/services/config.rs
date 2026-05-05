use std::path::Path;
use std::sync::Arc;
use tokio::fs;
use tokio::sync::RwLock;
use tracing::{debug, info, warn};

use regex::RegexSet;

use crate::error::AppError;
use crate::models::config::{ConfigModel, CURRENT_CONFIG_VERSION};

pub struct ConfigService {
    path: std::path::PathBuf,
    cache: RwLock<Option<Arc<ConfigModel>>>,
    filters: RwLock<CompiledFilters>,
}

struct CompiledFilters {
    app_ignore: Option<RegexSet>,
    app_whitelist: Option<RegexSet>,
    url_ignore: Option<RegexSet>,
}

impl ConfigService {
    pub fn new(data_dir: &Path) -> Self {
        let path = data_dir.join("AppConfig.json");
        Self {
            path,
            cache: RwLock::new(None),
            filters: RwLock::new(CompiledFilters {
                app_ignore: None,
                app_whitelist: None,
                url_ignore: None,
            }),
        }
    }

    pub async fn load(&self) -> Result<Arc<ConfigModel>, AppError> {
        {
            let cache = self.cache.read().await;
            if let Some(config) = cache.as_ref() {
                debug!("load config from cache");
                return Ok(Arc::clone(config));
            }
        }

        let mut cache = self.cache.write().await;
        if let Some(config) = cache.as_ref() {
            debug!("load config from cache (double-checked)");
            return Ok(Arc::clone(config));
        }

        let config = self.load_from_disk().await?;
        let arc_config = Arc::new(config);
        *cache = Some(Arc::clone(&arc_config));
        drop(cache);

        self.recompile_filters(&arc_config).await;
        debug!("config loaded into cache");
        Ok(arc_config)
    }

    pub async fn save(&self, config: &ConfigModel) -> Result<(), AppError> {
        config.validate().map_err(AppError::Business)?;
        self.persist(config).await?;

        let mut cache = self.cache.write().await;
        *cache = Some(Arc::new(config.clone()));
        drop(cache);

        self.recompile_filters(config).await;

        info!("config saved and cache updated");
        Ok(())
    }

    pub async fn get_cached(&self) -> Result<Arc<ConfigModel>, AppError> {
        let cache = self.cache.read().await;
        match cache.as_ref() {
            Some(config) => {
                debug!("get config from cache");
                Ok(Arc::clone(config))
            }
            None => Err(AppError::Business("Config not loaded yet".to_string())),
        }
    }

    pub async fn get_or_load(&self) -> Result<Arc<ConfigModel>, AppError> {
        self.load().await
    }

    pub async fn should_ignore_app(&self, process_name: &str, file_path: Option<&str>) -> bool {
        let Ok(config) = self.get_or_load().await else {
            tracing::warn!(
                "Config not available for app filter check, allowing: {}",
                process_name
            );
            return false;
        };

        let behavior = &config.behavior;
        let filters = self.filters.read().await;

        if behavior.is_white_list {
            let in_whitelist =
                match_any(process_name, file_path, &filters.app_whitelist, &behavior.process_white_list);
            if !in_whitelist {
                debug!(
                    "App not in whitelist, ignoring: process={}, path={:?}",
                    process_name, file_path
                );
            }
            !in_whitelist
        } else {
            let ignored =
                match_any(process_name, file_path, &filters.app_ignore, &behavior.ignore_process_list);
            if ignored {
                debug!(
                    "App in ignore list, ignoring: process={}, path={:?}",
                    process_name, file_path
                );
            }
            ignored
        }
    }

    pub async fn should_ignore_url(&self, url: &str) -> bool {
        let Ok(config) = self.get_or_load().await else {
            tracing::warn!(
                "Config not available for URL filter check, allowing: {}",
                url
            );
            return false;
        };

        let filters = self.filters.read().await;
        let ignored = match_text(url, &filters.url_ignore, &config.behavior.ignore_url_list);
        if ignored {
            debug!("URL in ignore list, ignoring: {}", url);
        }
        ignored
    }

    async fn recompile_filters(&self, config: &ConfigModel) {
        let behavior = &config.behavior;
        let mut filters = self.filters.write().await;

        filters.app_ignore = compile_patterns(&behavior.ignore_process_list);
        filters.app_whitelist = compile_patterns(&behavior.process_white_list);
        filters.url_ignore = compile_patterns(&behavior.ignore_url_list);

        info!(
            "Filters recompiled: app_ignore={}, app_whitelist={}, url_ignore={}",
            behavior.ignore_process_list.len(),
            behavior.process_white_list.len(),
            behavior.ignore_url_list.len()
        );
    }

    async fn load_from_disk(&self) -> Result<ConfigModel, AppError> {
        if !self.path.exists() {
            info!("config not found, create default");
            let config = ConfigModel::default();
            self.persist(&config).await?;
            return Ok(config);
        }

        debug!("load config from {:?}", self.path);
        let mut content = fs::read_to_string(&self.path).await.map_err(|e| {
            AppError::Internal(format!("Failed to read config file: {}", e))
        })?;

        if content.starts_with('\u{FEFF}') {
            content.remove(0);
        }

        let mut config: ConfigModel = serde_json::from_str(&content).map_err(|e| {
            warn!("parse config failed: {}", e);
            AppError::Internal(format!("Failed to parse config file: {}", e))
        })?;

        let original_version = config.version;

        // 需要迁移时，先备份原配置文件
        if original_version < CURRENT_CONFIG_VERSION {
            let backup_path = format!(
                "{}.backup.{}",
                self.path.display(),
                chrono::Utc::now().format("%Y%m%d_%H%M%S")
            );
            fs::copy(&self.path, &backup_path).await.map_err(|e| {
                AppError::Internal(format!("Failed to backup config file: {}", e))
            })?;
            info!("旧配置已备份至: {}", backup_path);
        }

        config.migrate();
        config.validate().map_err(AppError::Business)?;

        if original_version != config.version {
            self.persist(&config).await?;
        }

        Ok(config)
    }

    async fn persist(&self, config: &ConfigModel) -> Result<(), AppError> {
        if let Some(parent) = self.path.parent() {
            fs::create_dir_all(parent).await.map_err(|e| {
                AppError::Internal(format!("Failed to create config dir: {}", e))
            })?;
        }

        let content = serde_json::to_string_pretty(config).map_err(|e| {
            AppError::Internal(format!("Failed to serialize config: {}", e))
        })?;

        let temp_path = self
            .path
            .with_extension(format!("tmp.{}", std::process::id()));
        fs::write(&temp_path, content).await.map_err(|e| {
            AppError::Internal(format!("Failed to write temp config file: {}", e))
        })?;

        fs::rename(&temp_path, &self.path).await.map_err(|e| {
            let _ = std::fs::remove_file(&temp_path);
            AppError::Internal(format!("Failed to rename config file: {}", e))
        })?;

        Ok(())
    }
}

fn compile_patterns(patterns: &[String]) -> Option<RegexSet> {
    let regex_patterns: Vec<String> = patterns
        .iter()
        .map(|p| p.trim())
        .filter(|p| !p.is_empty())
        .map(|p| {
            match regex::Regex::new(p) {
                Ok(_) => p.to_string(),
                Err(_) => {
                    debug!("Pattern '{}' is not a valid regex, treating as literal", p);
                    regex::escape(p)
                }
            }
        })
        .collect();

    if regex_patterns.is_empty() {
        return None;
    }

    match RegexSet::new(&regex_patterns) {
        Ok(set) => Some(set),
        Err(e) => {
            warn!("Failed to compile regex set: {}", e);
            None
        }
    }
}

fn match_text(text: &str, regex_set: &Option<RegexSet>, exact_list: &[String]) -> bool {
    let text_lower = text.to_lowercase();
    if exact_list
        .iter()
        .any(|p| p.trim().eq_ignore_ascii_case(&text_lower))
    {
        return true;
    }
    if let Some(set) = regex_set {
        return set.is_match(text);
    }
    false
}

fn match_any(
    name: &str,
    path: Option<&str>,
    regex_set: &Option<RegexSet>,
    exact_list: &[String],
) -> bool {
    if match_text(name, regex_set, exact_list) {
        return true;
    }
    if let Some(p) = path {
        if match_text(p, regex_set, exact_list) {
            return true;
        }
    }
    false
}
