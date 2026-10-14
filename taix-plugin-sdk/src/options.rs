use anyhow::{anyhow, Context};
use std::path::PathBuf;

/// `--data-dir` 缺省回退 TAIX_DATA_DIR
#[derive(Debug, Clone)]
pub struct PluginOptions {
    pub data_dir: Option<PathBuf>,
    pub server_url: String,
}

pub fn parse(args: &[String]) -> anyhow::Result<PluginOptions> {
    parse_with_env(
        args,
        std::env::var("TAIX_SERVER").ok(),
        std::env::var("TAIX_DATA_DIR").ok(),
    )
}

/// 测试用；环境变量由调用方注入
pub fn parse_with_env(
    args: &[String],
    env_server: Option<String>,
    env_data_dir: Option<String>,
) -> anyhow::Result<PluginOptions> {
    let mut iter = args.iter().peekable();

    // run 可省，便于手动调试
    if iter.peek().map(|s| s.as_str()) == Some("run") {
        iter.next();
    }

    let mut data_dir: Option<PathBuf> = None;
    while let Some(arg) = iter.next() {
        match arg.as_str() {
            "--plugin" => {
                // 只消费参数；id 以 PluginApp::new 为准
                iter.next().context("--plugin 缺少参数值")?;
            }
            "--data-dir" => {
                let v = iter.next().context("--data-dir 缺少参数值")?;
                data_dir = Some(PathBuf::from(v));
            }
            other => return Err(anyhow!("未知参数 '{}'（合法：run / --plugin / --data-dir）", other)),
        }
    }

    let data_dir = data_dir.or_else(|| env_data_dir.map(PathBuf::from));
    let server_url = env_server.unwrap_or_else(|| "http://127.0.0.1:37091".to_string());
    let server_url = server_url.trim_end_matches('/').to_string();

    Ok(PluginOptions {
        data_dir,
        server_url,
    })
}
