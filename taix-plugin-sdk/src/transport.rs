use anyhow::{anyhow, Context};
use serde_json::Value;
use std::io::Read;
use std::time::Duration;

// 同步请求超时；SSE 流式只设连接超时
const REQUEST_TIMEOUT: Duration = Duration::from_secs(10);

/// 同步、单次尝试；重试由调用方决定
pub struct Transport {
    pub server_url: String,
    pub plugin_id: String,
    client: attohttpc::Session,
}

impl Clone for Transport {
    fn clone(&self) -> Self {
        // Session 不可 Clone，用新会话代替
        Self {
            server_url: self.server_url.clone(),
            plugin_id: self.plugin_id.clone(),
            client: attohttpc::Session::new(),
        }
    }
}

impl std::fmt::Debug for Transport {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Transport")
            .field("server_url", &self.server_url)
            .field("plugin_id", &self.plugin_id)
            .finish()
    }
}

impl Transport {
    pub fn new(server_url: &str, plugin_id: &str) -> Self {
        Self {
            server_url: server_url.to_string(),
            plugin_id: plugin_id.to_string(),
            client: attohttpc::Session::new(),
        }
    }

    fn url(&self, path: &str) -> String {
        format!("{}{}", self.server_url, path)
    }

    pub fn get_config(&self) -> anyhow::Result<(u32, Value)> {
        let mut resp = self
            .client
            .get(self.url(&format!("/api/plugins/{}/config", self.plugin_id)))
            .timeout(REQUEST_TIMEOUT)
            .send()
            .context("config 请求失败")?;
        check_status(&resp)?;
        let mut body = String::new();
        resp.read_to_string(&mut body)?;
        let api = ensure_ok(&body, "config")?;
        let data = api["data"].as_object().context("config 响应缺少 data")?;
        Ok((
            data["version"].as_u64().unwrap_or(0) as u32,
            data["values"].clone(),
        ))
    }

    pub fn poll(&self) -> anyhow::Result<(Value, Vec<Value>)> {
        let mut resp = self
            .client
            .get(self.url(&format!("/api/plugins/{}/poll", self.plugin_id)))
            .timeout(REQUEST_TIMEOUT)
            .send()
            .context("poll 请求失败")?;
        check_status(&resp)?;
        let mut body = String::new();
        resp.read_to_string(&mut body)?;
        let api = ensure_ok(&body, "poll")?;
        let data = api["data"].as_object().context("poll 响应缺少 data")?;
        let config = data["config"].clone();
        let commands = data["commands"]
            .as_array()
            .cloned()
            .unwrap_or_default();
        Ok((config, commands))
    }

    pub fn push_state(&self, state: &str, data: Option<Value>) -> anyhow::Result<()> {
        let payload = serde_json::json!({ "state": state, "data": data });
        self.post_json(&format!("/api/plugins/{}/events", self.plugin_id), &payload)
    }

    pub fn record(&self, key: &str, value: &Value) -> anyhow::Result<()> {
        let payload = serde_json::json!({ "key": key, "value": value });
        self.post_json(&format!("/api/plugins/{}/records", self.plugin_id), &payload)
    }

    /// 读 server 既有 REST，返回 data 字段
    pub fn get_json(&self, path: &str) -> anyhow::Result<Value> {
        let mut resp = self
            .client
            .get(self.url(path))
            .timeout(REQUEST_TIMEOUT)
            .send()
            .with_context(|| format!("GET {} 失败", path))?;
        check_status(&resp)?;
        let mut body = String::new();
        resp.read_to_string(&mut body)?;
        let api = ensure_ok(&body, path)?;
        Ok(api["data"].clone())
    }

    fn post_json(&self, path: &str, payload: &Value) -> anyhow::Result<()> {
        let body = serde_json::to_string(payload)?;
        let mut resp = self
            .client
            .post(self.url(path))
            .header("Content-Type", "application/json")
            .text(body)
            .timeout(REQUEST_TIMEOUT)
            .send()
            .with_context(|| format!("POST {} 失败", path))?;
        check_status(&resp)?;
        let mut resp_body = String::new();
        resp.read_to_string(&mut resp_body)?;
        ensure_ok(&resp_body, path).map(|_| ())
    }

    pub fn open_sse(&self) -> anyhow::Result<Box<dyn Read + Send>> {
        let resp = self
            .client
            .get(self.url(&format!("/api/events?sub={}", self.plugin_id)))
            .header("Accept", "text/event-stream")
            .connect_timeout(REQUEST_TIMEOUT)
            .send()
            .context("SSE 连接失败")?;
        check_status(&resp)?;
        Ok(Box::new(resp))
    }
}

fn check_status(resp: &attohttpc::Response) -> anyhow::Result<()> {
    let status = resp.status();
    if status.is_success() {
        Ok(())
    } else {
        Err(anyhow!("HTTP {}", status.as_u16()))
    }
}

/// 失败响应也是 HTTP 200，必须查 body 里的 code
fn ensure_ok(body: &str, what: &str) -> anyhow::Result<Value> {
    let api: Value = serde_json::from_str(body).with_context(|| format!("{} 响应非 JSON", what))?;
    if api["code"].as_i64() != Some(0) {
        return Err(anyhow!(
            "{} 返回错误 code={}: {}",
            what,
            api["code"],
            api["message"]
        ));
    }
    Ok(api)
}

/// 1s 起每次翻倍，封顶 30s
pub struct Backoff {
    next_secs: u64,
}

impl Backoff {
    pub fn new() -> Self {
        Self { next_secs: 1 }
    }

    pub fn next_delay(&mut self) -> Duration {
        let d = Duration::from_secs(self.next_secs);
        self.next_secs = (self.next_secs * 2).min(30);
        d
    }

    pub fn reset(&mut self) {
        self.next_secs = 1;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn backoff_sequence_caps_at_30() {
        let mut b = Backoff::new();
        let seq: Vec<u64> = (0..8).map(|_| b.next_delay().as_secs()).collect();
        assert_eq!(seq, vec![1, 2, 4, 8, 16, 30, 30, 30]);
        b.reset();
        assert_eq!(b.next_delay().as_secs(), 1);
    }

    #[test]
    fn ensure_ok_rejects_error_body() {
        let not_found = r#"{"code":404,"message":"plugin 'demo' not found"}"#;
        let err = ensure_ok(not_found, "/api/plugins/demo/events").unwrap_err();
        assert!(err.to_string().contains("404"), "{err}");

        let saved = r#"{"code":0,"message":"success","data":null}"#;
        assert!(ensure_ok(saved, "/api/plugins/demo/records").is_ok());
    }
}
