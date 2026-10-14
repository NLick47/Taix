use anyhow::Context;

/// 必须与 script/install.ps1 注册值一致
pub const DEFAULT_AUMID: &str = "Taix.Plugin";

#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn effective_aumid(explicit: Option<&str>) -> &str {
    explicit.unwrap_or(DEFAULT_AUMID)
}

/// 换行与制表拆段拼 linefeed/tab，否则脚本被打断
#[cfg_attr(target_os = "windows", allow(dead_code))]
fn as_script_str(text: &str) -> String {
    let escape = |s: &str| s.replace('\\', "\\\\").replace('"', "\\\"");
    let mut parts: Vec<String> = Vec::new();
    for line in text.replace("\r\n", "\n").replace('\r', "\n").split('\n') {
        let mut segs = line.split('\t');
        let mut piece = escape(segs.next().unwrap_or_default());
        for tab_seg in segs {
            piece.push_str("\" & tab & \"");
            piece.push_str(&escape(tab_seg));
        }
        parts.push(piece);
    }
    format!("\"{}\"", parts.join("\" & linefeed & \""))
}

#[derive(Debug, Clone, Default)]
pub struct NotifyOptions {
    /// 缺省 DEFAULT_AUMID
    #[cfg(target_os = "windows")]
    pub aumid: Option<String>,
    /// None = 静默；Windows 声音名大小写不敏感，未知名字回退 Default
    pub sound: Option<&'static str>,
    /// 交互按钮；macOS 忽略
    pub actions: Vec<(String, String)>,
}

/// notify-rust 只认精确大小写的 Windows 声音名，解析失败会被静默变成静音
#[cfg(target_os = "windows")]
fn normalize_sound(name: &str) -> String {
    const FIXED: [&str; 5] = ["Default", "IM", "Mail", "Reminder", "SMS"];

    let trimmed = name.trim();
    if let Some(hit) = FIXED.iter().find(|c| c.eq_ignore_ascii_case(trimmed)) {
        return (*hit).to_string();
    }

    // Alarm / Alarm2..Alarm10 / Call / Call2..Call10 只有首字母大小写之差
    let mut candidate = trimmed.to_ascii_lowercase();
    if let Some(first) = candidate.get_mut(0..1) {
        first.make_ascii_uppercase();
        for prefix in ["Alarm", "Call"] {
            if let Some(rest) = candidate.strip_prefix(prefix) {
                let numbered = (2..=10).any(|n| rest == n.to_string());
                if rest.is_empty() || numbered {
                    return candidate;
                }
            }
        }
    }

    tracing::warn!(target: "taix_notify", "未知 Windows 声音名 '{}'，回退 Default", name);
    "Default".to_string()
}

/// 失败只记日志，不 panic
pub fn notify(title: &str, body: &str, opts: &NotifyOptions) -> anyhow::Result<()> {
    tracing::debug!(target: "taix_notify", "notify: {title} / {body}");

    #[cfg(target_os = "windows")]
    {
        let mut n = notify_rust::Notification::new();
        n.app_id(effective_aumid(opts.aumid.as_deref()));
        n.summary(title).body(body);
        let sound = opts.sound.map(normalize_sound);
        if let Some(sound) = sound.as_deref() {
            n.sound_name(sound);
        }
        n.show().context("Windows Toast 发送失败")?;
        tracing::info!(
            target: "taix_notify",
            "toast 已提交 aumid={} sound={:?}",
            effective_aumid(opts.aumid.as_deref()),
            sound
        );
    }

    #[cfg(not(target_os = "windows"))]
    {
        let _ = &opts.actions; // macOS 无交互按钮
        // 无 bundle 进程下 notify-rust 不弹横幅，必须走 osascript
        let mut script = format!(
            "display notification {} with title {}",
            as_script_str(body),
            as_script_str(title),
        );
        if let Some(sound) = opts.sound {
            script.push_str(&format!(" sound name {}", as_script_str(sound)));
        }
        let status = std::process::Command::new("osascript")
            .arg("-e")
            .arg(&script)
            .status()
            .context("osascript 启动失败")?;
        anyhow::ensure!(
            status.success(),
            "osascript 返回失败（exit {:?}）",
            status.code()
        );
    }

    Ok(())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn aumid_default_matches_installer() {
        let installer = include_str!("../../script/install.ps1");
        assert!(
            installer.contains(&format!("\"{DEFAULT_AUMID}\"")),
            "install.ps1 必须注册 Toast AUMID {DEFAULT_AUMID}"
        );
    }

    #[test]
    fn script_str_escapes_user_text() {
        assert_eq!(as_script_str("普通文本"), "\"普通文本\"");
        assert_eq!(as_script_str("带\"引号"), "\"带\\\"引号\"");
        assert_eq!(as_script_str("反\\斜杠"), "\"反\\\\斜杠\"");
    }

    #[test]
    fn script_str_handles_newlines_and_tabs() {
        assert_eq!(as_script_str("a\nb"), "\"a\" & linefeed & \"b\"");
        assert_eq!(as_script_str("a\r\nb\rc"), "\"a\" & linefeed & \"b\" & linefeed & \"c\"");
        assert_eq!(as_script_str("a\tb"), "\"a\" & tab & \"b\"");
        assert_eq!(
            as_script_str("第\"一\"行\n第二\t行"),
            "\"第\\\"一\\\"行\" & linefeed & \"第二\" & tab & \"行\""
        );
    }

    #[cfg(target_os = "windows")]
    #[test]
    fn sound_name_is_normalized_for_notify_rust() {
        assert_eq!(normalize_sound("default"), "Default");
        assert_eq!(normalize_sound(" Default "), "Default");
        assert_eq!(normalize_sound("im"), "IM");
        assert_eq!(normalize_sound("SMS"), "SMS");
        assert_eq!(normalize_sound("alarm3"), "Alarm3");
        assert_eq!(normalize_sound("CALL10"), "Call10");
        // 未知名字回退，而不是静默静音
        assert_eq!(normalize_sound("gong"), "Default");
        assert_eq!(normalize_sound("alarm11"), "Default");
        assert_eq!(normalize_sound(""), "Default");
    }
}
