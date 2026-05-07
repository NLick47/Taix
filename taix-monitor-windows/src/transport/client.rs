use super::queue::MessageQueue;
use crate::models::MonitorMessage;
use chrono::{DateTime, Local};

pub struct MonitorClient {
    queue: MessageQueue,
}

impl MonitorClient {
    pub fn new(queue: MessageQueue) -> Self {
        Self { queue }
    }

    pub async fn send_app_duration(
        &self,
        process: &str,
        duration: i64,
        active_time: DateTime<Local>,
        file_path: Option<&str>,
        icon_path: Option<&str>,
        description: Option<&str>,
    ) {
        let msg = MonitorMessage::App {
            p: process,
            d: duration,
            a: active_time.timestamp(),
            f: file_path,
            i: icon_path,
            desc: description,
        };
        let mut json = match serde_json::to_string(&msg) {
            Ok(s) => s,
            Err(e) => {
                tracing::error!(target: "monitor_client", "Failed to serialize app message: {}", e);
                return;
            }
        };
        json.push('\n');
        if let Err(e) = self.queue.enqueue(json.into_bytes()).await {
            tracing::warn!(target: "monitor_client", "Failed to enqueue app duration: {}", e);
        }
    }

    pub async fn send_sleep(&self) {
        let msg = MonitorMessage::Sleep;
        let mut json = match serde_json::to_string(&msg) {
            Ok(s) => s,
            Err(e) => {
                tracing::error!(target: "monitor_client", "Failed to serialize sleep message: {}", e);
                return;
            }
        };
        json.push('\n');
        if let Err(e) = self.queue.enqueue(json.into_bytes()).await {
            tracing::warn!(target: "monitor_client", "Failed to enqueue sleep: {}", e);
        }
    }

    pub async fn send_wake(&self) {
        let msg = MonitorMessage::Wake;
        let mut json = match serde_json::to_string(&msg) {
            Ok(s) => s,
            Err(e) => {
                tracing::error!(target: "monitor_client", "Failed to serialize wake message: {}", e);
                return;
            }
        };
        json.push('\n');
        if let Err(e) = self.queue.enqueue(json.into_bytes()).await {
            tracing::warn!(target: "monitor_client", "Failed to enqueue wake: {}", e);
        }
    }

    pub async fn shutdown(self) {
        self.queue.shutdown().await;
    }
}
