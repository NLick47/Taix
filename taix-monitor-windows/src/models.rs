use serde::Serialize;
use std::fmt;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SleepStatus {
    Wake,
    Sleep,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
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

#[derive(Debug, Clone)]
#[allow(dead_code)]
pub struct AppInfo {
    pub window_handle: isize,
    pub pid: u32,
    pub process: String,
    pub description: String,
    pub executable_path: String,
    pub icon_path: String,
    pub app_type: AppType,
}

impl AppInfo {
    pub fn empty() -> Self {
        Self {
            window_handle: 0,
            pid: 0,
            process: String::new(),
            description: String::new(),
            executable_path: String::new(),
            icon_path: String::new(),
            app_type: AppType::Win32,
        }
    }

    pub fn is_empty(&self) -> bool {
        self.process.is_empty()
    }
}

#[derive(Debug, Clone)]
#[allow(dead_code)]
pub struct WindowInfo {
    pub class_name: String,
    pub title: String,
    pub handle: isize,
    pub width: i32,
    pub height: i32,
    pub x: i32,
    pub y: i32,
}

impl WindowInfo {
    pub fn empty() -> Self {
        Self {
            class_name: String::new(),
            title: String::new(),
            handle: 0,
            width: 0,
            height: 0,
            x: 0,
            y: 0,
        }
    }

    pub fn is_empty(&self) -> bool {
        self.class_name.is_empty() && self.title.is_empty()
    }
}

#[derive(Debug, Clone)]
#[allow(dead_code)]
pub struct AppActiveEvent {
    pub app: AppInfo,
    pub window: WindowInfo,
    pub active_time: chrono::DateTime<chrono::Local>,
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