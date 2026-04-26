use super::queue::ReliableMessageQueue;
use crate::models::MonitorMessage;
use chrono::{DateTime, Local};

pub struct MonitorClient {
    queue: ReliableMessageQueue,
}

impl MonitorClient {
    pub fn new(queue: ReliableMessageQueue) -> Self {
        Self { queue }
    }

    pub fn send_app_duration(
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
        let mut json = serde_json::to_string(&msg).unwrap();
        json.push('\n');
        self.queue.enqueue(json.into_bytes());
    }

    pub fn send_sleep(&self) {
        let msg = MonitorMessage::Sleep;
        let mut json = serde_json::to_string(&msg).unwrap();
        json.push('\n');
        self.queue.enqueue(json.into_bytes());
    }

    pub fn send_wake(&self) {
        let msg = MonitorMessage::Wake;
        let mut json = serde_json::to_string(&msg).unwrap();
        json.push('\n');
        self.queue.enqueue(json.into_bytes());
    }

    pub async fn shutdown(self) {
        self.queue.shutdown().await;
    }
}