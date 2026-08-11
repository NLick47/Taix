use std::collections::HashSet;
use std::path::Path;
use std::sync::Arc;
use std::time::{Duration, Instant};
use tokio::fs;
use tokio::sync::RwLock;
use tracing::{debug, info, warn};

use regex::RegexSet;
use sqlx::SqlitePool;

use crate::error::AppError;
use crate::models::config::{ConfigModel, CURRENT_CONFIG_VERSION};

pub struct ConfigService {
    path: std::path::PathBuf,
    cache: RwLock<Option<Arc<ConfigModel>>>,
    filters: RwLock<CompiledFilters>,
    excluded_apps_cache: RwLock<Option<(HashSet<i64>, Instant)>>,
}

struct CompiledFilters {
    app_ignore: Option<RegexSet>,
    app_whitelist: Option<RegexSet>,
    url_ignore: Option<RegexSet>,
    url_literals: HashSet<String>,
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
                url_literals: HashSet::new(),
            }),
            excluded_apps_cache: RwLock::new(None),
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

        let mut excluded_cache = self.excluded_apps_cache.write().await;
        *excluded_cache = None;

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

    pub async fn get_excluded_app_ids(&self, apps: &[(i64, &str, Option<&str>)]) -> Vec<i64> {
        let Ok(config) = self.get_or_load().await else {
            return Vec::new();
        };
        let filters = self.filters.read().await;
        let behavior = &config.behavior;
        let mut excluded = Vec::new();

        if behavior.is_white_list {
            for (id, name, file) in apps {
                if name.is_empty() { continue; }
                if !match_any(name, *file, &filters.app_whitelist, &behavior.process_white_list) {
                    excluded.push(*id);
                }
            }
        } else {
            for (id, name, file) in apps {
                if name.is_empty() { continue; }
                if match_any(name, *file, &filters.app_ignore, &behavior.ignore_process_list) {
                    excluded.push(*id);
                }
            }
        }
        excluded
    }

    /// 从数据库查询 AppModels 并计算应排除的应用 ID 集合（带 5 秒缓存）
    pub async fn get_excluded_app_id_set(&self, pool: &SqlitePool) -> HashSet<i64> {
        const MAX_EXCLUDED_IDS: usize = 900;

        {
            let cache = self.excluded_apps_cache.read().await;
            if let Some((set, timestamp)) = cache.as_ref() {
                if timestamp.elapsed() < Duration::from_secs(5) {
                    return set.clone();
                }
            }
        }

        let apps: Vec<(i64, Option<String>, Option<String>)> = sqlx::query_as(
            "SELECT ID, Name, File FROM AppModels"
        )
        .fetch_all(pool)
        .await
        .unwrap_or_default();

        let app_refs: Vec<(i64, &str, Option<&str>)> = apps
            .iter()
            .map(|(id, name, file)| (*id, name.as_deref().unwrap_or(""), file.as_deref()))
            .collect();

        let mut excluded_ids = self.get_excluded_app_ids(&app_refs).await;
        if excluded_ids.len() > MAX_EXCLUDED_IDS {
            warn!(
                "排除应用 ID 数量 {} 超过 SQLite 参数上限保护值 {}，已截断；超出的应用将不会被过滤",
                excluded_ids.len(),
                MAX_EXCLUDED_IDS
            );
            excluded_ids.truncate(MAX_EXCLUDED_IDS);
        }
        let set: HashSet<i64> = excluded_ids.into_iter().collect();

        let mut cache = self.excluded_apps_cache.write().await;
        *cache = Some((set.clone(), Instant::now()));

        set
    }

    /// 批量匹配应排除的域名
    pub async fn get_excluded_domains(&self, domains: &[&str]) -> Vec<String> {
        if self.get_or_load().await.is_err() {
            return Vec::new();
        }

        let filters = self.filters.read().await;

        let mut excluded = Vec::new();

        for domain in domains {
            if domain.is_empty() {
                continue;
            }

            let mut ignored = false;

            {
                let domain_lower = domain.to_lowercase();
                let mut suffix = domain_lower.as_str();
                loop {
                    if filters.url_literals.contains(suffix) {
                        ignored = true;
                        break;
                    }
                    match suffix.find('.') {
                        Some(i) => suffix = &suffix[i + 1..],
                        None => break,
                    }
                }
            }

            if !ignored {
                if let Some(set) = &filters.url_ignore {
                    if set.is_match(domain) {
                        ignored = true;
                    } else {
                        let full_url = format!("https://{}", domain);
                        if set.is_match(&full_url) {
                            ignored = true;
                        }
                    }
                }
            }

            if ignored {
                excluded.push(domain.to_string());
            }
        }
        excluded
    }

    async fn recompile_filters(&self, config: &ConfigModel) {
        let behavior = &config.behavior;
        let mut filters = self.filters.write().await;

        filters.app_ignore = compile_patterns(&behavior.ignore_process_list);
        filters.app_whitelist = compile_patterns(&behavior.process_white_list);
        filters.url_ignore = compile_patterns(&behavior.ignore_url_list);
        // 纯字面量条目（不含通配符）进哈希集合供 O 后缀匹配；
        // 含通配符的条目语义上只能靠正则命中，不放进集合
        filters.url_literals = compile_url_literals(&behavior.ignore_url_list);

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

/// 从 ignore_url_list 提取纯字面量条目（无通配符，小写去空），供 O(层级) 后缀匹配
fn compile_url_literals(patterns: &[String]) -> HashSet<String> {
    patterns
        .iter()
        .map(|p| p.trim().to_lowercase())
        .filter(|p| !p.is_empty() && !p.contains('*') && !p.contains('?'))
        .collect()
}

fn compile_patterns(patterns: &[String]) -> Option<RegexSet> {
    let regex_patterns: Vec<String> = patterns
        .iter()
        .map(|p| p.trim())
        .filter(|p| !p.is_empty())
        .map(|p| wildcard_to_regex(p))
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

/// 将通配符模式转换为正则表达式
/// * 匹配任意数量字符 -> .*
/// ? 匹配单个字符 -> .
/// 其他字符转义为字面量
#[inline]
fn wildcard_to_regex(pattern: &str) -> String {
    let mut result = String::with_capacity(pattern.len() * 2);
    result.push_str("(?i)^"); // (?i) 启用大小写不敏感

    for c in pattern.chars() {
        match c {
            '*' => result.push_str(".*"),
            '?' => result.push('.'),
            // 转义正则特殊字符
            '.' | '^' | '$' | '+' | '[' | ']' | '(' | ')' | '{' | '}' | '\\' | '|' => {
                result.push('\\');
                result.push(c);
            }
            _ => result.push(c),
        }
    }

    result.push('$');
    result
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
