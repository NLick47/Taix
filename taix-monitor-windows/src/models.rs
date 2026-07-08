use std::fmt;
use std::time::SystemTime;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SleepStatus {
    Wake,
    Sleep,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
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
    pub start_time: SystemTime,
}

// 通过命名管道发给 daemon 的格式
pub enum MonitorMessage<'a> {
    App {
        p: &'a str,
        d: i64,
        a: i64,
        f: Option<&'a str>,
        i: Option<&'a str>,
        desc: Option<&'a str>,
    },
    Sleep,
    Wake,
}

impl<'a> fmt::Display for MonitorMessage<'a> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            MonitorMessage::App {
                p,
                d,
                a,
                f: file_path,
                i: icon_path,
                desc,
            } => {
                write!(f, "{{\"t\":\"app\",\"p\":{},\"d\":{},\"a\":{}", json_escape(p), d, a)?;
                if let Some(v) = file_path {
                    write!(f, ",\"f\":{}", json_escape(v))?;
                }
                if let Some(v) = icon_path {
                    write!(f, ",\"i\":{}", json_escape(v))?;
                }
                if let Some(v) = desc {
                    write!(f, ",\"desc\":{}", json_escape(v))?;
                }
                write!(f, "}}")
            }
            MonitorMessage::Sleep => write!(f, "{{\"t\":\"sleep\"}}"),
            MonitorMessage::Wake => write!(f, "{{\"t\":\"wake\"}}"),
        }
    }
}

fn json_escape(s: &str) -> String {
    let mut out = String::with_capacity(s.len() + 2);
    out.push('"');
    for c in s.chars() {
        match c {
            '"' => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            '\n' => out.push_str("\\n"),
            '\r' => out.push_str("\\r"),
            '\t' => out.push_str("\\t"),
            c if (c as u32) < 0x20 => {
                use std::fmt::Write;
                let _ = write!(out, "\\u{:04x}", c as u32);
            }
            c => out.push(c),
        }
    }
    out.push('"');
    out
}

// 崩溃恢复用的会话检查点
#[derive(Debug, Clone)]
pub struct SessionCheckpoint {
    pub process: String,
    pub exe_path: String,
    pub icon_path: String,
    pub desc: String,
    pub since_ts: i64,
}

impl fmt::Display for SessionCheckpoint {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        // 简单 key=value 格式，用 \0 分隔避免路径中的特殊字符问题
        // process\0<process>\0path\0<exe_path>\0icon\0<icon_path>\0desc\0<desc>\0since\0<ts>
        write!(
            f,
            "process\x00{}\x00path\x00{}\x00icon\x00{}\x00desc\x00{}\x00since\x00{}",
            self.process, self.exe_path, self.icon_path, self.desc, self.since_ts
        )
    }
}

impl SessionCheckpoint {
    pub fn from_str(s: &str) -> Option<Self> {
        let parts: Vec<&str> = s.split('\x00').collect();
        if parts.len() < 10 {
            return None;
        }
        // 按 key 查找
        let mut process = "";
        let mut exe_path = "";
        let mut icon_path = "";
        let mut desc = "";
        let mut since_ts = 0i64;
        for chunk in parts.chunks(2) {
            if chunk.len() < 2 {
                break;
            }
            match chunk[0] {
                "process" => process = chunk[1],
                "path" => exe_path = chunk[1],
                "icon" => icon_path = chunk[1],
                "desc" => desc = chunk[1],
                "since" => since_ts = chunk[1].parse().ok()?,
                _ => {}
            }
        }
        Some(Self {
            process: process.to_owned(),
            exe_path: exe_path.to_owned(),
            icon_path: icon_path.to_owned(),
            desc: desc.to_owned(),
            since_ts,
        })
    }
}
