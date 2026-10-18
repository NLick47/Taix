use std::fmt;
use std::time::SystemTime;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum SleepStatus {
    Wake,
    Sleep,
}

/// Monitor 配置，从 AppConfig.json 读取
#[derive(Debug, Clone)]
pub struct MonitorConfig {
    /// 空闲阈值（秒），默认 900 (15分钟)
    pub inactive_threshold_secs: u64,
    /// 声音持续时间（秒），默认 7200 (120分钟)
    pub max_sound_duration_secs: u64,
    /// 是否启用睡眠检测，默认 true
    pub sleep_watch: bool,
}

impl Default for MonitorConfig {
    fn default() -> Self {
        Self {
            #[cfg(debug_assertions)]
            inactive_threshold_secs: 10,
            #[cfg(not(debug_assertions))]
            inactive_threshold_secs: 900,
            max_sound_duration_secs: 7200,
            sleep_watch: true,
        }
    }
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
    pub session_id: String,
}

#[derive(Debug, Clone)]
pub struct AppDurationEvent {
    pub duration_secs: i64,
    pub app: AppInfo,
    pub start_time: SystemTime,
    pub session_id: String,
}

/// 会话 id 由时间戳 + 进程内序号组成，monitor 重启也不会撞
pub fn next_session_id() -> String {
    use std::sync::atomic::{AtomicU64, Ordering};
    static SEQ: AtomicU64 = AtomicU64::new(0);
    let seq = SEQ.fetch_add(1, Ordering::Relaxed);
    let nanos = SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .unwrap_or_default()
        .as_nanos();
    format!("{nanos}-{seq}")
}

// 通过命名管道发给 daemon 的格式
pub enum MonitorMessage<'a> {
    App {
        p: &'a str,
        d: i64,
        a: i64,
        sid: &'a str,
        f: Option<&'a str>,
        i: Option<&'a str>,
        desc: Option<&'a str>,
    },
    Sleep,
    Wake,
    /// M4 心跳：当前前台应用
    Current {
        p: &'a str,
        f: Option<&'a str>,
        i: Option<&'a str>,
        s: i64,
        sid: &'a str,
    },
}

impl<'a> fmt::Display for MonitorMessage<'a> {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            MonitorMessage::App {
                p,
                d,
                a,
                sid,
                f: file_path,
                i: icon_path,
                desc,
            } => {
                write!(f, "{{\"t\":\"app\",\"p\":{},\"d\":{},\"a\":{},\"sid\":{}", json_escape(p), d, a, json_escape(sid))?;
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
            MonitorMessage::Current { p, f: file_path, i: icon_path, s, sid } => {
                write!(f, "{{\"t\":\"current\",\"p\":{},\"s\":{},\"sid\":{}", json_escape(p), s, json_escape(sid))?;
                if let Some(v) = file_path {
                    write!(f, ",\"f\":{}", json_escape(v))?;
                }
                if let Some(v) = icon_path {
                    write!(f, ",\"i\":{}", json_escape(v))?;
                }
                write!(f, "}}")
            }
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
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SessionCheckpoint {
    pub process: String,
    pub exe_path: String,
    pub icon_path: String,
    pub desc: String,
    pub since_ts: i64,
    /// 会话 id。恢复时复用，server 按 SessionId 去重，避免把已结算会话再记一遍
    pub session_id: String,
    /// 最后一次观测到该会话仍活跃的时间。恢复只补到此刻，不把停机时间算成使用
    pub last_seen: i64,
}

/// 恢复时实际要上报的内容
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct RecoveryPlan {
    pub session_id: String,
    pub since_ts: i64,
    pub duration_secs: i64,
}

impl fmt::Display for SessionCheckpoint {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        // 简单 key=value 格式，用 \0 分隔避免路径中的特殊字符问题
        // process\0<process>\0path\0<exe_path>\0icon\0<icon_path>\0desc\0<desc>\0since\0<ts>\0sid\0<id>\0last\0<ts>
        write!(
            f,
            "process\x00{}\x00path\x00{}\x00icon\x00{}\x00desc\x00{}\x00since\x00{}\x00sid\x00{}\x00last\x00{}",
            self.process,
            self.exe_path,
            self.icon_path,
            self.desc,
            self.since_ts,
            self.session_id,
            self.last_seen
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
        let mut session_id = "";
        let mut last_seen = 0i64;
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
                "sid" => session_id = chunk[1],
                "last" => last_seen = chunk[1].parse().ok()?,
                _ => {}
            }
        }
        Some(Self {
            process: process.to_owned(),
            exe_path: exe_path.to_owned(),
            icon_path: icon_path.to_owned(),
            desc: desc.to_owned(),
            since_ts,
            session_id: session_id.to_owned(),
            last_seen,
        })
    }

    /// 旧格式没有 sid/last，无法安全恢复：宁可丢一小段，也不重复记账
    pub fn recovery_plan(&self) -> Option<RecoveryPlan> {
        if self.session_id.trim().is_empty() || self.since_ts <= 0 || self.last_seen <= 0 {
            return None;
        }
        let duration_secs = self.last_seen.max(self.since_ts) - self.since_ts;
        if duration_secs <= 0 {
            return None;
        }
        Some(RecoveryPlan {
            session_id: self.session_id.clone(),
            since_ts: self.since_ts,
            duration_secs,
        })
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    /// 手拼 JSON 没有 serde 兜底，sid 字段的格式靠测试锁死
    #[test]
    fn app_message_json_carries_session_id() {
        let msg = MonitorMessage::App {
            p: "chrome.exe",
            d: 120,
            a: 1_792_302_696,
            sid: "nano-1",
            f: Some(r"C:\app\chrome.exe"),
            i: None,
            desc: Some("Chrome"),
        };
        let json = msg.to_string();
        assert!(json.starts_with(r#"{"t":"app""#), "{json}");
        assert!(json.contains(r#""p":"chrome.exe""#), "{json}");
        assert!(json.contains(r#""d":120"#), "{json}");
        assert!(json.contains(r#""a":1792302696"#), "{json}");
        assert!(json.contains(r#""sid":"nano-1""#), "{json}");
        assert!(json.contains(r#""f":"C:\\app\\chrome.exe""#), "{json}");
        assert!(json.contains(r#""desc":"Chrome""#), "{json}");
        assert!(!json.contains(r#""i":"#), "None 字段不得出现: {json}");
        assert!(json.ends_with('}'), "{json}");
    }

    #[test]
    fn current_message_json_carries_session_id() {
        let msg = MonitorMessage::Current {
            p: "code.exe",
            f: None,
            i: Some("icon.png"),
            s: 1_792_302_696,
            sid: "1792302696000000000-3",
        };
        let json = msg.to_string();
        assert!(json.starts_with(r#"{"t":"current""#), "{json}");
        assert!(json.contains(r#""p":"code.exe""#), "{json}");
        assert!(json.contains(r#""s":1792302696"#), "{json}");
        assert!(json.contains(r#""sid":"1792302696000000000-3""#), "{json}");
        assert!(!json.contains(r#""f":"#), "None 字段不得出现: {json}");
        assert!(json.contains(r#""i":"icon.png""#), "{json}");
        assert!(json.ends_with('}'), "{json}");
    }

    #[test]
    fn lifecycle_messages_unchanged() {
        assert_eq!(MonitorMessage::Sleep.to_string(), r#"{"t":"sleep"}"#);
        assert_eq!(MonitorMessage::Wake.to_string(), r#"{"t":"wake"}"#);
    }

    #[test]
    fn next_session_id_is_unique_and_shaped() {
        let a = next_session_id();
        let b = next_session_id();
        assert_ne!(a, b);
        assert!(a.contains('-'), "{a}");
        assert!(a.chars().all(|c| c.is_ascii_digit() || c == '-'), "{a}");
    }

    #[test]
    fn checkpoint_roundtrip_keeps_session_id_and_last_seen() {
        let cp = SessionCheckpoint {
            process: "chrome.exe".to_string(),
            exe_path: r"C:\app\chrome.exe".to_string(),
            icon_path: "icon.png".to_string(),
            desc: "Chrome".to_string(),
            since_ts: 1_792_300_000,
            session_id: "1792300000000000000-7".to_string(),
            last_seen: 1_792_300_120,
        };
        let parsed = SessionCheckpoint::from_str(&cp.to_string()).unwrap();
        assert_eq!(parsed, cp);
        assert_eq!(
            parsed.recovery_plan(),
            Some(RecoveryPlan {
                session_id: "1792300000000000000-7".to_string(),
                since_ts: 1_792_300_000,
                duration_secs: 120,
            })
        );
    }

    #[test]
    fn legacy_checkpoint_is_not_recovered() {
        // 旧格式没有 sid/last：宁可跳过，也不能用新 sid 重复记账
        let legacy = "process\x00old.exe\x00path\x00C:\\old.exe\x00icon\x00\x00desc\x00Old\x00since\x001792300000";
        let parsed = SessionCheckpoint::from_str(legacy).unwrap();
        assert!(parsed.session_id.is_empty());
        assert_eq!(parsed.last_seen, 0);
        assert!(parsed.recovery_plan().is_none());
    }

    #[test]
    fn recovery_uses_last_seen_not_now() {
        let cp = SessionCheckpoint {
            process: "a.exe".to_string(),
            exe_path: String::new(),
            icon_path: String::new(),
            desc: String::new(),
            since_ts: 1_792_300_000,
            session_id: "sid-1".to_string(),
            last_seen: 1_792_300_090,
        };
        let plan = cp.recovery_plan().unwrap();
        assert_eq!(plan.duration_secs, 90);
        assert_eq!(plan.session_id, "sid-1");
    }

    #[test]
    fn checkpoint_without_elapsed_time_is_not_recovered() {
        let cp = SessionCheckpoint {
            process: "a.exe".to_string(),
            exe_path: String::new(),
            icon_path: String::new(),
            desc: String::new(),
            since_ts: 1_792_300_000,
            session_id: "sid-1".to_string(),
            last_seen: 1_792_300_000,
        };
        assert!(cp.recovery_plan().is_none());
    }
}
