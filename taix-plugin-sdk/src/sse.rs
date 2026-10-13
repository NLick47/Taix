use serde_json::Value;
use std::collections::VecDeque;
use std::io::{BufRead, BufReader};
use std::sync::{Arc, Mutex};
use std::time::{Duration, Instant};

use crate::ctx::PluginCtx;
use crate::transport::{Backoff, Transport};

pub type CommandHandler = Arc<dyn Fn(&PluginCtx, Option<Value>) + Send + Sync>;
pub type ConfigHandler = Arc<dyn Fn(&PluginCtx, &Value) + Send + Sync>;
pub type CurrentHandler = Arc<dyn Fn(&PluginCtx, &Value) + Send + Sync>;

pub struct Handlers {
    pub commands: Vec<(String, CommandHandler)>,
    pub config_changed: Option<ConfigHandler>,
    pub current: Option<CurrentHandler>,
    pub tick: Option<(Duration, Arc<dyn Fn(&PluginCtx) + Send + Sync>)>,
}

pub struct SseLoop {
    pub transport: Transport,
    pub ctx: Arc<PluginCtx>,
    pub handlers: Arc<Handlers>,
    /// 防御性去重；至多一次由 server 投递游标保证
    seen_seq: Mutex<VecDeque<u64>>,
    /// poll 降级据此启停
    sse_alive: Mutex<Instant>,
}

const SEEN_SEQ_MAX: usize = 256;
const FALLBACK_AFTER: std::time::Duration = std::time::Duration::from_secs(5 * 60);
const POLL_INTERVAL: std::time::Duration = std::time::Duration::from_secs(2);

impl SseLoop {
    pub fn new(transport: Transport, ctx: Arc<PluginCtx>, handlers: Arc<Handlers>) -> Self {
        Self {
            transport,
            ctx,
            handlers,
            seen_seq: Mutex::new(VecDeque::new()),
            sse_alive: Mutex::new(Instant::now()),
        }
    }

    /// 断线退避重连，不返回
    pub fn run_forever(&self) {
        let mut backoff = Backoff::new();
        loop {
            match self.transport.open_sse() {
                Ok(reader) => {
                    tracing::info!(target: "taix_plugin_sdk", "SSE 已连接 {}", self.transport.server_url);
                    *self.sse_alive.lock().unwrap() = Instant::now();
                    backoff.reset();
                    // 重连后主动对齐断线期间的配置变更
                    self.refresh_config();
                    self.consume_stream(reader);
                    tracing::warn!(target: "taix_plugin_sdk", "SSE 连接断开，准备重连");
                }
                Err(e) => {
                    tracing::warn!(target: "taix_plugin_sdk", "SSE 连接失败: {:#}", e);
                }
            }
            std::thread::sleep(backoff.next_delay());
        }
    }

    fn consume_stream(&self, reader: Box<dyn std::io::Read + Send>) {
        let mut reader = BufReader::new(reader);
        let mut data_buf = String::new();

        loop {
            let mut line = String::new();
            match reader.read_line(&mut line) {
                Ok(0) => return, // EOF
                Ok(_) => {
                    // 任何字节都算存活，keep-alive 也算
                    *self.sse_alive.lock().unwrap() = Instant::now();
                }
                Err(e) => {
                    tracing::debug!(target: "taix_plugin_sdk", "SSE 读取错误: {}", e);
                    return;
                }
            }

            let line = line.trim_end_matches(['\n', '\r']);
            if line.is_empty() {
                // 空行 = 事件帧结束
                if !data_buf.is_empty() {
                    self.handle_event(&data_buf);
                    data_buf.clear();
                }
                continue;
            }
            if let Some(rest) = line.strip_prefix("data:") {
                if !data_buf.is_empty() {
                    data_buf.push('\n');
                }
                data_buf.push_str(rest.trim_start());
            }
            // : 开头是 keep-alive，忽略
        }
    }

    fn handle_event(&self, data: &str) {
        let Ok(event) = serde_json::from_str::<Value>(data) else {
            let head: String = data.chars().take(120).collect();
            tracing::debug!(target: "taix_plugin_sdk", "忽略无法解析的事件: {}", head);
            return;
        };

        match event["type"].as_str() {
            Some("command") => {
                if event["id"].as_str() != Some(self.ctx.id()) {
                    return;
                }
                let seq = event["seq"].as_u64().unwrap_or(0);
                if !self.mark_seen(seq) {
                    tracing::debug!(target: "taix_plugin_sdk", "重复指令 seq={} 已忽略", seq);
                    return;
                }
                let cmd = event["cmd"].as_str().unwrap_or_default().to_string();
                let payload = event["data"].clone();
                if let Some((_, handler)) = self.handlers.commands.iter().find(|(name, _)| *name == cmd) {
                    handler(&self.ctx, if payload.is_null() { None } else { Some(payload) });
                } else {
                    tracing::debug!(target: "taix_plugin_sdk", "未注册的指令 '{}'（seq={}）", cmd, seq);
                }
            }
            Some("config") => {
                if event["id"].as_str() != Some(self.ctx.id()) {
                    return;
                }
                if event["version"].as_u64() == Some(self.ctx.config_version()) {
                    return;
                }
                self.refresh_config();
            }
            Some("current") => {
                if let Some(handler) = &self.handlers.current {
                    handler(&self.ctx, &event);
                }
            }
            _ => {}
        }
    }

    pub fn refresh_config(&self) {
        match self.transport.get_config() {
            Ok((version, values)) => {
                if version as u64 == self.ctx.config_version() {
                    return;
                }
                self.ctx.set_config(version as u64, values.clone());
                self.notify_config_changed(&values);
            }
            Err(e) => {
                tracing::warn!(target: "taix_plugin_sdk", "刷新配置失败: {:#}", e);
            }
        }
    }

    pub fn notify_config_changed(&self, values: &Value) {
        if let Some(handler) = &self.handlers.config_changed {
            handler(&self.ctx, values);
        }
    }

    fn mark_seen(&self, seq: u64) -> bool {
        let mut seen = self.seen_seq.lock().unwrap();
        if seen.contains(&seq) {
            return false;
        }
        seen.push_back(seq);
        while seen.len() > SEEN_SEQ_MAX {
            seen.pop_front();
        }
        true
    }

    /// SSE 断线超时后轮询取指令；SSE 恢复自动停
    pub fn run_poll_fallback(&self) {
        loop {
            std::thread::sleep(POLL_INTERVAL);
            if self.sse_alive.lock().unwrap().elapsed() < FALLBACK_AFTER {
                continue;
            }
            match self.transport.poll() {
                Ok((config, commands)) => {
                    let version = config["version"].as_u64().unwrap_or(0);
                    if version != 0 && version != self.ctx.config_version() {
                        self.refresh_config();
                    }
                    for command in commands {
                        let seq = command["seq"].as_u64().unwrap_or(0);
                        if !self.mark_seen(seq) {
                            continue;
                        }
                        let cmd = command["cmd"].as_str().unwrap_or_default().to_string();
                        let payload = command["data"].clone();
                        if let Some((_, handler)) =
                            self.handlers.commands.iter().find(|(name, _)| *name == cmd)
                        {
                            tracing::info!(target: "taix_plugin_sdk", "poll 降级消费指令 '{}'", cmd);
                            handler(&self.ctx, if payload.is_null() { None } else { Some(payload) });
                        }
                    }
                }
                Err(_) => {} // 仍不可达，下轮重试
            }
        }
    }
}
