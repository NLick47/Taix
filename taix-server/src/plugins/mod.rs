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

/// 有界指令日志 + poll 降级游标；投递是「每个订阅者各读一份」，不再取走即丢
#[derive(Default)]
pub struct PluginQueue {
    log: VecDeque<PluginCommand>,
    /// /poll 无状态，降级通道的游标存这里；SSE 泵的游标在泵自己手里
    poll_cursor: u64,
}

pub struct PluginState {
    /// 启动时扫描；热安装靠 404 路径限量重扫。保留安装目录，ui 资源与 shell 启动用同一份路径
    manifests: tokio::sync::RwLock<Vec<manifest::Installed>>,
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
    /// 插件 UI 资源按 <plugins_dir>/<id>/ui/ 解析
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
            .find(|m| m.manifest.id == id)
            .map(|m| m.manifest.clone())
    }

    /// 未命中时限量重扫，支持热安装
    async fn rescan_throttled(&self) {
        let now = std::time::SystemTime::now()
            .duration_since(std::time::UNIX_EPOCH)
            .map(|d| d.as_millis() as u64)
            .unwrap_or(0);
        let last = self.last_scan_ms.swap(now, Ordering::Relaxed);
        if now.saturating_sub(last) >= 1000 {
            self.rescan().await;
        }
    }

    /// 404 路径用；未命中限量重扫，支持热安装
    pub async fn manifest_or_rescan(&self, id: &str) -> Option<manifest::PluginManifest> {
        if let Some(m) = self.manifest(id).await {
            return Some(m);
        }
        self.rescan_throttled().await;
        self.manifest(id).await
    }

    /// 插件安装目录；与 shell 拉起进程用的是同一份路径
    pub async fn plugin_dir_or_rescan(&self, id: &str) -> Option<PathBuf> {
        let dir_of = |manifests: &Vec<manifest::Installed>| {
            manifests
                .iter()
                .find(|m| m.manifest.id == id)
                .map(|m| m.dir())
        };
        if let Some(dir) = dir_of(&*self.manifests.read().await) {
            return Some(dir);
        }
        self.rescan_throttled().await;
        dir_of(&*self.manifests.read().await)
    }

    pub async fn list_manifests(&self) -> Vec<manifest::PluginManifest> {
        self.manifests
            .read()
            .await
            .iter()
            .map(|m| m.manifest.clone())
            .collect()
    }

    pub async fn rescan(&self) {
        let scanned = manifest::scan(&self.plugins_dir);
        let n = scanned.len();
        *self.manifests.write().await = scanned;
        tracing::debug!(target: "taix_server::plugins", "rescanned {} manifest(s)", n);
    }

    /// seq 大于 after 的指令；多个订阅者用各自游标读同一份日志，互不偷抢
    pub fn commands_after(&self, id: &str, after: u64) -> Vec<PluginCommand> {
        self.queues
            .lock()
            .unwrap()
            .get(id)
            .map(|q| q.log.iter().filter(|c| c.seq > after).cloned().collect())
            .unwrap_or_default()
    }

    /// poll 降级通道：取游标之后的指令并推进自己的游标；不影响 SSE 泵
    pub fn take_poll_commands(&self, id: &str) -> Vec<PluginCommand> {
        let mut queues = self.queues.lock().unwrap();
        let queue = queues.entry(id.to_string()).or_default();
        let taken: Vec<PluginCommand> = queue
            .log
            .iter()
            .filter(|c| c.seq > queue.poll_cursor)
            .cloned()
            .collect();
        if let Some(last) = taken.last() {
            queue.poll_cursor = queue.poll_cursor.max(last.seq);
        }
        taken
    }

    pub fn log_len(&self, id: &str) -> usize {
        self.queues
            .lock()
            .unwrap()
            .get(id)
            .map(|q| q.log.len())
            .unwrap_or(0)
    }

    pub fn latest_seq(&self, id: &str) -> u64 {
        self.queues
            .lock()
            .unwrap()
            .get(id)
            .and_then(|q| q.log.back().map(|c| c.seq))
            .unwrap_or(0)
    }

    /// 唤醒投递泵；Notify 只唤醒一个等待者，所以先广播再补许可
    pub fn notify_commands(&self, id: &str) {
        let notifier = {
            let mut notifiers = self.notify.lock().unwrap();
            notifiers
                .entry(id.to_string())
                .or_insert_with(|| Arc::new(tokio::sync::Notify::new()))
                .clone()
        };
        notifier.notify_waiters();
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
        self.log_len(id)
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    async fn test_state(tag: &str) -> Arc<PluginState> {
        let root = std::env::temp_dir().join(format!("taix-cmdlog-{}-{}", tag, std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        std::fs::create_dir_all(root.join("plugins")).unwrap();
        std::fs::create_dir_all(root.join("data")).unwrap();
        Arc::new(
            PluginState::init(&root.join("plugins"), &root.join("data"))
                .await
                .unwrap(),
        )
    }

    /// 多个消费端各读一份日志，不再互相偷抢
    #[tokio::test]
    async fn command_log_serves_every_consumer() {
        let state = test_state("multi").await;
        service::push_command(&state, "reminder", "dry-run".to_string(), None);

        assert_eq!(state.commands_after("reminder", 0).len(), 1);
        assert_eq!(
            state.commands_after("reminder", 0).len(),
            1,
            "第二个订阅者用同样游标也应取到"
        );

        // poll 降级通道推进自己的游标，不影响 SSE 泵
        assert_eq!(state.take_poll_commands("reminder").len(), 1);
        assert!(state.take_poll_commands("reminder").is_empty());
        assert_eq!(state.commands_after("reminder", 0).len(), 1);
    }

    /// 游标按 seq 前进后不重复投递；日志有界，超出丢最旧
    #[tokio::test]
    async fn command_log_cursor_and_capacity() {
        let state = test_state("cursor").await;
        for i in 0..3 {
            service::push_command(&state, "reminder", format!("c{i}"), None);
        }
        let all = state.commands_after("reminder", 0);
        assert_eq!(all.len(), 3);
        assert!(all.windows(2).all(|w| w[0].seq < w[1].seq), "seq 必须递增");
        let last = all.last().unwrap().seq;
        assert_eq!(state.latest_seq("reminder"), last);

        for _ in 0..constants::PLUGIN_COMMAND_QUEUE_MAX + 5 {
            service::push_command(&state, "reminder", "x".to_string(), None);
        }
        assert_eq!(state.log_len("reminder"), constants::PLUGIN_COMMAND_QUEUE_MAX);
        assert!(
            state.commands_after("reminder", last).len()
                <= constants::PLUGIN_COMMAND_QUEUE_MAX,
            "有界日志不得无限增长"
        );
    }

    /// 陈旧状态按「未上报」返回，避免进程已死界面还显示运行中
    #[tokio::test]
    async fn stale_status_is_reported_as_none() {        let state = test_state("stale").await;
        let status = |ts: chrono::DateTime<chrono::Utc>| PluginStatus {
            state: "running".to_string(),
            data: None,
            ts: ts.to_rfc3339_opts(chrono::SecondsFormat::Secs, true),
        };

        state.states.write().await.insert(
            "reminder".to_string(),
            status(chrono::Utc::now() - chrono::Duration::seconds(300)),
        );
        assert!(
            service::get_status(&state, "reminder").await.is_none(),
            "300s 前的状态应视为陈旧"
        );

        state
            .states
            .write()
            .await
            .insert("reminder".to_string(), status(chrono::Utc::now()));
        assert!(service::get_status(&state, "reminder").await.is_some());
    }

    /// records 写入即裁：超过上限时按 UpdatedAt/写入顺序淘汰最旧
    #[tokio::test]
    async fn records_are_trimmed_on_write() {
        let pool = sqlx::SqlitePool::connect("sqlite::memory:").await.unwrap();
        crate::migrations::run(&pool, "Asia/Shanghai").await.unwrap();

        let cap = constants::PLUGIN_RECORDS_MAX_PER_PLUGIN as usize;
        for i in 0..cap + 8 {
            service::upsert_record(&pool, "reminder", &format!("k{i}"), &serde_json::json!(i))
                .await
                .unwrap();
        }

        let rows = service::get_records(&pool, "reminder", None).await.unwrap();
        assert_eq!(rows.len(), cap, "写入路径必须即时裁剪到上限");
        let keys: Vec<String> = rows.iter().map(|r| r.key.clone()).collect();
        assert!(keys.contains(&format!("k{}", cap + 7)), "最新 key 必须保留");
        assert!(!keys.contains(&"k0".to_string()), "最旧 key 应被淘汰");
    }
}
