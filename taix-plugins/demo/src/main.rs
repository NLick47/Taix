use serde::Deserialize;
use serde_json::json;
use taix_plugin_sdk::PluginApp;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DemoConfig {
    #[serde(default = "default_work_minutes")]
    work_minutes: i64,
}

fn default_work_minutes() -> i64 {
    25
}

fn main() {
    PluginApp::new("demo")
        .on_config_changed(|ctx, config: &DemoConfig| {
            log_config(config);
            // 演示配置变更即写入 records
            ctx.record("config/workMinutes", &json!(config.work_minutes));
        })
        .on_command("start", |ctx, data| {
            ctx.push_state("running", data);
        })
        .on_command("pause", |ctx, _| {
            ctx.push_state("paused", None);
        })
        .on_command("reset", |ctx, _| {
            ctx.push_state("idle", None);
        })
        .run()
        .unwrap();
}

fn log_config(config: &DemoConfig) {
    tracing::info!(target: "taix_plugin_demo", "config: workMinutes={}", config.work_minutes);
}
