use serde::de::DeserializeOwned;
use serde_json::Value;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Mutex;
use std::time::{Duration, Instant};

use crate::state::StateStore;
use crate::transport::Transport;

/// 回调在 SDK 事件线程内同步执行，必须快速返回
pub struct PluginCtx {
    pub(crate) transport: Transport,
    pub(crate) state_store: StateStore,
    config_cache: Mutex<Option<Value>>,
    config_version: AtomicU64,
    last_state_push: Mutex<Option<(String, Instant)>>,
    /// 最近一次成功上报的状态，心跳线程用它重推做存活信号
    last_state_payload: Mutex<Option<(String, Option<Value>)>>,
}

impl PluginCtx {
    pub(crate) fn new(transport: Transport, state_store: StateStore) -> Self {
        Self {
            transport,
            state_store,
            config_cache: Mutex::new(None),
            config_version: AtomicU64::new(0),
            last_state_push: Mutex::new(None),
            last_state_payload: Mutex::new(None),
        }
    }

    pub fn id(&self) -> &str {
        &self.transport.plugin_id
    }

    pub fn server_url(&self) -> &str {
        &self.transport.server_url
    }

    pub fn state_store(&self) -> &StateStore {
        &self.state_store
    }

    pub fn config_raw(&self) -> Option<Value> {
        self.config_cache.lock().unwrap().clone()
    }

    pub fn config<T: DeserializeOwned>(&self) -> Option<T> {
        serde_json::from_value(self.config_raw()?).ok()
    }

    pub(crate) fn config_version(&self) -> u64 {
        self.config_version.load(Ordering::Relaxed)
    }

    pub(crate) fn set_config(&self, version: u64, values: Value) {
        *self.config_cache.lock().unwrap() = Some(values);
        self.config_version.store(version, Ordering::Relaxed);
    }

    /// 同一 state 5s 内不重发
    pub fn push_state(&self, state: &str, data: Option<Value>) -> bool {
        self.push_state_inner(state, data, false)
    }

    /// 跳过 5s 节流，用于必须马上让界面看到的变化
    pub fn push_state_now(&self, state: &str, data: Option<Value>) -> bool {
        self.push_state_inner(state, data, true)
    }

    fn push_state_inner(&self, state: &str, data: Option<Value>, force: bool) -> bool {
        {
            let mut last = self.last_state_push.lock().unwrap();
            if !force {
                if let Some((last_state, at)) = last.as_ref() {
                    if *last_state == state && at.elapsed() < Duration::from_secs(5) {
                        return false;
                    }
                }
            }
            *last = Some((state.to_string(), Instant::now()));
        }
        match self.transport.push_state(state, data.clone()) {
            Ok(()) => {
                *self.last_state_payload.lock().unwrap() = Some((state.to_string(), data));
                true
            }
            Err(e) => {
                tracing::warn!(target: "taix_plugin_sdk", "push_state 失败: {:#}", e);
                false
            }
        }
    }

    /// 重推上次成功上报的状态作为存活心跳；没上报过就什么都不做
    pub(crate) fn heartbeat_state(&self) {
        let payload = self.last_state_payload.lock().unwrap().clone();
        let Some((state, data)) = payload else {
            return;
        };
        match self.transport.push_state(&state, data) {
            Ok(()) => tracing::debug!(target: "taix_plugin_sdk", "状态心跳 state={}", state),
            Err(e) => tracing::warn!(target: "taix_plugin_sdk", "状态心跳失败: {:#}", e),
        }
    }

    pub fn record(&self, key: &str, value: &Value) -> bool {
        match self.transport.record(key, value) {
            Ok(()) => true,
            Err(e) => {
                tracing::warn!(target: "taix_plugin_sdk", "record '{}' 失败: {:#}", key, e);
                false
            }
        }
    }

    /// 读 server 既有 REST 的 data 字段；插件与客户端同权限
    pub fn get_json(&self, path: &str) -> anyhow::Result<Value> {
        self.transport.get_json(path)
    }

    /// 写进 records，界面按 nonce 认领
    pub fn reply(&self, key: &str, reply: &crate::Reply) -> bool {
        self.record(key, &reply.to_value())
    }
}
