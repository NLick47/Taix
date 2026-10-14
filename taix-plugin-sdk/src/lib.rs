mod ctx;
mod options;
mod request;
mod sse;
mod state;
mod transport;

use anyhow::Context;
use serde_json::Value;
use std::sync::Arc;
use std::time::Duration;

pub use ctx::PluginCtx;
pub use request::{Reply, Request};
pub use state::StateStore;

use sse::{Handlers, SseLoop};
use state::SingleInstance;
use transport::Transport;

pub struct PluginApp {
    id: String,
    handlers: Handlers,
}

impl PluginApp {
    pub fn new(id: &str) -> Self {
        Self {
            id: id.to_string(),
            handlers: Handlers {
                commands: Vec::new(),
                config_changed: None,
                current: None,
                tick: None,
            },
        }
    }

    pub fn on_command<F>(mut self, cmd: &str, f: F) -> Self
    where
        F: Fn(&PluginCtx, Option<Value>) + Send + Sync + 'static,
    {
        self.handlers
            .commands
            .push((cmd.to_string(), Arc::new(f)));
        self
    }

    /// 首次加载即触发，此后仅 version 变化时触发
    pub fn on_config_changed<T, F>(mut self, f: F) -> Self
    where
        T: serde::de::DeserializeOwned + Send + Sync + 'static,
        F: Fn(&PluginCtx, &T) + Send + Sync + 'static,
    {
        self.handlers.config_changed = Some(Arc::new(move |ctx: &PluginCtx, values: &Value| {
            match serde_json::from_value::<T>(values.clone()) {
                Ok(config) => f(ctx, &config),
                Err(e) => {
                    tracing::warn!(target: "taix_plugin_sdk", "配置反序列化失败: {}", e);
                }
            }
        }));
        self
    }

    pub fn on_current<F>(mut self, f: F) -> Self
    where
        F: Fn(&PluginCtx, &Value) + Send + Sync + 'static,
    {
        self.handlers.current = Some(Arc::new(f));
        self
    }

    /// 专用线程调用，回调可以阻塞
    pub fn on_tick<F>(mut self, interval: Duration, f: F) -> Self
    where
        F: Fn(&PluginCtx) + Send + Sync + 'static,
    {
        self.handlers.tick = Some((interval, Arc::new(f)));
        self
    }

    /// 阻塞运行；停用由外部 kill 完成，插件不主动退出
    pub fn run(self) -> anyhow::Result<()> {
        let args: Vec<String> = std::env::args().skip(1).collect();
        self.run_with_args(&args)
    }

    pub fn run_with_args(self, args: &[String]) -> anyhow::Result<()> {
        // 先起日志：启动期错误（参数、单实例锁）也要落盘
        let _logging = taix_logging::init(
            &format!("taix-plugin-{}", self.id),
            "info",
            taix_logging::PanicMode::SyncFile,
            7,
        );

        let opts = options::parse(args).context("解析启动参数失败")?;

        tracing::info!(
            target: "taix_plugin_sdk",
            "plugin '{}' starting, server={} data_dir={:?} filter={}",
            self.id,
            opts.server_url,
            opts.data_dir,
            std::env::var("RUST_LOG").unwrap_or_else(|_| "info".to_string())
        );

        let _single = SingleInstance::acquire(&self.id)
            .context("获取插件单实例锁失败")?;
        tracing::info!(target: "taix_plugin_sdk", "单实例锁已就绪 id={}", self.id);

        let transport = Transport::new(&opts.server_url, &self.id);
        let ctx = Arc::new(PluginCtx::new(
            transport.clone(),
            StateStore::new(opts.data_dir.as_deref(), &self.id),
        ));

        let handlers = Arc::new(self.handlers);

        // 等 server 就绪并加载首次配置
        let sse = Arc::new(SseLoop::new(transport, ctx.clone(), handlers.clone()));
        wait_for_initial_config(&sse, &ctx);

        let sse_thread = Arc::clone(&sse);
        std::thread::Builder::new()
            .name("sse".into())
            .spawn(move || sse_thread.run_forever())?;

        let poll_thread = Arc::clone(&sse);
        std::thread::Builder::new()
            .name("poll-fallback".into())
            .spawn(move || poll_thread.run_poll_fallback())?;

        if let Some((interval, tick_handler)) = handlers.tick.clone() {
            let tick_ctx = ctx.clone();
            std::thread::Builder::new()
                .name("tick".into())
                .spawn(move || loop {
                    tick_handler(&tick_ctx);
                    std::thread::sleep(interval);
                })?;
        }

        // 状态存活心跳：server 端超过 90s 没收到上报会按「未上报」处理，插件不必自己实现
        let heartbeat_ctx = ctx.clone();
        std::thread::Builder::new()
            .name("state-heartbeat".into())
            .spawn(move || loop {
                std::thread::sleep(std::time::Duration::from_secs(30));
                heartbeat_ctx.heartbeat_state();
            })?;

        loop {
            std::thread::park();
        }
    }
}

fn wait_for_initial_config(sse: &SseLoop, ctx: &Arc<PluginCtx>) {
    let mut backoff = transport::Backoff::new();
    loop {
        match sse.transport.get_config() {
            Ok((version, values)) => {
                ctx.set_config(version as u64, values.clone());
                tracing::info!(
                    target: "taix_plugin_sdk",
                    "初始配置加载成功 version={}",
                    version
                );
                sse.notify_config_changed(&values);
                return;
            }
            Err(e) => {
                let delay = backoff.next_delay();
                tracing::warn!(
                    target: "taix_plugin_sdk",
                    "server 未就绪（{:#}），{:?} 后重试",
                    e,
                    delay
                );
                std::thread::sleep(delay);
            }
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn options_rejects_unknown_args() {
        let err = options::parse(&["--bogus".to_string()]).unwrap_err();
        assert!(err.to_string().contains("未知参数"));
    }

    #[test]
    fn options_resolves_dir_and_server_url() {
        let opts = options::parse_with_env(
            &[
                "run".into(),
                "--plugin".into(),
                "demo".into(),
                "--data-dir".into(),
                "/tmp/x".into(),
            ],
            Some("http://127.0.0.1:9999/".into()),
            None,
        )
        .unwrap();
        assert_eq!(opts.server_url, "http://127.0.0.1:9999");
        assert_eq!(opts.data_dir, Some("/tmp/x".into()));

        let opts =
            options::parse_with_env(&["--data-dir".into(), "/cli".into()], None, Some("/env".into()))
                .unwrap();
        assert_eq!(opts.data_dir, Some("/cli".into()));

        let opts = options::parse_with_env(&[], None, None).unwrap();
        assert_eq!(opts.data_dir, None);
        assert_eq!(opts.server_url, "http://127.0.0.1:37091");
    }
}
