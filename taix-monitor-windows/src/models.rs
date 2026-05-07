use serde::{Deserialize, Serialize};
use std::fmt;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SleepStatus {
    Wake,
    Sleep,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
pub enum AppType {
    Win32,
    Uwp,
    SystemComponent,
}

impl fmt::Display for AppType {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            AppType::Win32 => write!(f, "Win32"),
            AppType::Uwp => write!(f, "UWP"),
            AppType::SystemComponent => write!(f, "SystemComponent"),
        }
    }
}

/// 应用信息。process 字段为空是不合法状态，应由构造者保证。
#[derive(Debug, Clone)]
pub struct AppInfo {
    pub process: String,
    pub description: String,
    pub executable_path: String,
    pub icon_path: String,
    pub app_type: AppType,
}

#[derive(Debug, Clone)]
pub struct WindowInfo {
    pub class_name: String,
    pub title: String,
    pub _handle: isize,
}

impl WindowInfo {
    pub fn new(class_name: String, title: String, handle: isize) -> Self {
        Self {
            class_name,
            title,
            _handle: handle,
        }
    }
}

#[derive(Debug, Clone)]
pub struct AppActiveEvent {
    pub app: AppInfo,
    pub window: WindowInfo,
}

#[derive(Debug, Clone)]
pub struct AppDurationEvent {
    pub duration_secs: i64,
    pub app: AppInfo,
    pub start_time: chrono::DateTime<chrono::Local>,
}

// 通过命名管道发给 daemon 的格式
#[derive(Serialize)]
#[serde(tag = "t")]
pub enum MonitorMessage<'a> {
    #[serde(rename = "app")]
    App {
        p: &'a str,
        d: i64,
        a: i64,
        #[serde(skip_serializing_if = "Option::is_none")]
        f: Option<&'a str>,
        #[serde(skip_serializing_if = "Option::is_none")]
        i: Option<&'a str>,
        #[serde(skip_serializing_if = "Option::is_none")]
        desc: Option<&'a str>,
    },
    #[serde(rename = "sleep")]
    Sleep,
    #[serde(rename = "wake")]
    Wake,
}

// 崩溃恢复用的会话检查点，记录最近一次刷盘时的活跃应用状态
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct SessionCheckpoint {
    pub process: String,
    pub exe_path: String,
    pub icon_path: String,
    pub desc: String,
    pub since_ts: i64,
}
