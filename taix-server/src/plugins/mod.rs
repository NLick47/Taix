pub mod current;
pub mod manifest;
pub mod registry;
pub mod service;

use std::collections::{HashMap, VecDeque};
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::{Arc, Mutex};

use tokio::sync::{broadcast, RwLock};

use crate::constants;
use crate::error::AppError;
use registry::PluginRegistry;

/// 队列 + 投递游标；取走即已投递（至多一次）
#[derive(Default)]
pub struct PluginQueue {
    pending: VecDeque<PluginCommand>,
    cursor: u64,
}

pub struct PluginState {
    /// 启动时扫描；热安装靠 404 路径限量重扫
    manifests: tokio::sync::RwLock<Vec<manifest::PluginManifest>>,
    last_scan_ms: AtomicU64,
    pub registry: RwLock<PluginRegistry>,
    /// 写盘串行化，避免交错 rename
    pub registry_write: tokio::sync::Mutex<()>,
    pub queues: Mutex<HashMap<String, PluginQueue>>,
    notify: Mutex<HashMap<String, Arc<tokio::sync::Notify>>>,
    /// 用启动毫秒做种子；从 1 起数会与插件端残留 seq 撞车被去重
    pub seq: AtomicU64,
    pub states: RwLock<HashMap<String, PluginStatus>>,
    pub tx: broadcast::Sender<String>,
    /// monitor 心跳写入
    pub current_app: RwLock<Option<serde_json::Value>>,
    pub sleep_flag: std::sync::atomic::AtomicBool,
    /// 平台级数据：连续使用段起点；0 = 无段
    pub continuous_since: std::sync::atomic::AtomicI64,
    pub data_dir: PathBuf,
    #[allow(dead_code)]
    pub plugins_dir: PathBuf,
}

#[derive(Debug, Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PluginCommand {
    pub seq: u64,
    pub cmd: String,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub data: Option<serde_json::Value>,
}

#[derive(Debug, Clone, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PluginStatus {
    pub state: String,
    pub data: Option<serde_json::Value>,
    pub ts: String,
}

impl PluginState {
    pub async fn init(plugins_dir: &Path, data_dir: &Path) -> Result<Self, AppError> {
        let manifests = manifest::scan(plugins_dir);
        let (tx, _) = broadcast::channel(constants::PLUGIN_EVENT_CHANNEL_CAPACITY);

        tracing::info!(
            target: "taix_server::plugins",
            "scanned {} plugin manifest(s) from {}",
            manifests.len(),
            plugins_dir.display()
        );

        Ok(Self {
            manifests: tokio::sync::RwLock::new(manifests),
            last_scan_ms: AtomicU64::new(0),
            registry: RwLock::new(registry::load_or_default(data_dir).await?),
            registry_write: tokio::sync::Mutex::new(()),
            queues: Mutex::new(HashMap::new()),
            notify: Mutex::new(HashMap::new()),
            seq: AtomicU64::new(
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .map(|d| d.as_millis() as u64)
                    .unwrap_or(1),
            ),
            states: RwLock::new(HashMap::new()),
            tx,
            current_app: RwLock::new(None),
            sleep_flag: std::sync::atomic::AtomicBool::new(false),
            continuous_since: std::sync::atomic::AtomicI64::new(
                std::time::SystemTime::now()
                    .duration_since(std::time::UNIX_EPOCH)
                    .map(|d| d.as_secs() as i64)
                    .unwrap_or(0),
            ),
            data_dir: data_dir.to_path_buf(),
            plugins_dir: plugins_dir.to_path_buf(),
        })
    }

    /// 清单快照（clone，清单条目很小）
    pub async fn manifest(&self, id: &str) -> Option<manifest::PluginManifest> {
        self.manifests
            .read()
            .await
            .iter()
            .find(|m| m.id == id)
            .cloned()
    }

    /// 404 路径用；未命中限量重扫，支持热安装
    pub async fn manifest_or_rescan(&self, id: &str) -> Option<manifest::PluginManifest> {
        if let Some(m) = self.manifest(id).await {
            return Some(m);
        }
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_millis() as u64)
            .unwrap_or(0);
        let last = self.last_scan_ms.swap(now, Ordering::Relaxed);
        if now.saturating_sub(last) >= 1000 {
            self.rescan().await;
        }
        self.manifest(id).await
    }

    pub async fn list_manifests(&self) -> Vec<manifest::PluginManifest> {
        self.manifests.read().await.clone()
    }

    pub async fn rescan(&self) {
        let scanned = manifest::scan(&self.plugins_dir);
        let n = scanned.len();
        *self.manifests.write().await = scanned;
        tracing::debug!(target: "taix_server::plugins", "rescanned {} manifest(s)", n);
    }

    /// 取走全部未投递并推进游标；投递泵与 poll 共用
    pub fn take_pending(&self, id: &str) -> Vec<PluginCommand> {
        let mut queues = self.queues.lock().unwrap();
        let queue = queues.entry(id.to_string()).or_default();
        let taken: Vec<PluginCommand> = queue.pending.drain(..).collect();
        if let Some(last) = taken.last() {
            queue.cursor = queue.cursor.max(last.seq);
        }
        taken
    }

    pub fn cursor_of(&self, id: &str) -> u64 {
        self.queues
            .lock()
            .unwrap()
            .get(id)
            .map(|q| q.cursor)
            .unwrap_or(0)
    }

    /// 唤醒投递泵；Notify 留存许可，早于等待也不丢
    pub fn notify_commands(&self, id: &str) {
        let notifier = {
            let mut notifiers = self.notify.lock().unwrap();
            notifiers
                .entry(id.to_string())
                .or_insert_with(|| Arc::new(tokio::sync::Notify::new()))
                .clone()
        };
        notifier.notify_one();
    }

    pub fn command_notifier(&self, id: &str) -> Arc<tokio::sync::Notify> {
        let mut notifiers = self.notify.lock().unwrap();
        notifiers
            .entry(id.to_string())
            .or_insert_with(|| Arc::new(tokio::sync::Notify::new()))
            .clone()
    }

    pub fn pending_len(&self, id: &str) -> usize {
        self.queues
            .lock()
            .unwrap()
            .get(id)
            .map(|q| q.pending.len())
            .unwrap_or(0)
    }
}
