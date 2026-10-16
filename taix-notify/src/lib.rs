use anyhow::Context;
use std::path::{Path, PathBuf};
use std::sync::Mutex;

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
    /// Windows 的 Toast AUMID；None = DEFAULT_AUMID，macOS 忽略
    pub aumid: Option<String>,
    /// None = 静默；Windows 声音名大小写不敏感，未知名字回退 Default
    pub sound: Option<&'static str>,
    /// 交互按钮；macOS 忽略
    pub actions: Vec<(String, String)>,
    /// 通知图标 PNG 绝对路径；Windows 用 toast 的 appLogoOverride，macOS 待接
    pub icon: Option<PathBuf>,
}

#[derive(Debug, Clone, Default)]
pub struct AppIdentity {
    pub aumid: Option<String>,
    pub icon: Option<PathBuf>,
}

#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn identity_aumid(id: &str) -> String {
    format!("{DEFAULT_AUMID}.{id}")
}

#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn identity_subkey(id: &str) -> String {
    format!(r"SOFTWARE\Classes\AppUserModelId\{}", identity_aumid(id))
}

pub fn app_identity(id: &str, display_name: &str, icon_png: &[u8]) -> AppIdentity {
    #[cfg(target_os = "windows")]
    {
        static CACHE: Mutex<Option<AppIdentity>> = Mutex::new(None);
        if let Some(cached) = CACHE.lock().unwrap().clone() {
            return cached;
        }

        let identity = match win_identity::ensure(id, display_name, icon_png) {
            Ok(identity) => identity,
            Err(e) => {
                // 未注册的 AUMID 只进通知中心不弹横幅，宁可退回共享身份也别换名字
                tracing::warn!(target: "taix_notify", "注册通知身份失败，退回共享 AUMID: {e:#}");
                AppIdentity {
                    aumid: None,
                    icon: win_identity::materialize(id, icon_png).ok(),
                }
            }
        };
        *CACHE.lock().unwrap() = Some(identity.clone());
        identity
    }
    #[cfg(not(target_os = "windows"))]
    {
        let _ = (id, display_name, icon_png);
        AppIdentity::default()
    }
}

#[cfg(target_os = "windows")]
mod win_identity {
    use super::{identity_aumid, identity_subkey, AppIdentity};
    use std::path::PathBuf;
    use windows::core::PCWSTR;
    use windows::Win32::Foundation::ERROR_SUCCESS;
    use windows::Win32::System::Registry::{
        RegCloseKey, RegCreateKeyExW, RegOpenKeyExW, RegQueryValueExW, RegSetValueExW, HKEY,
        HKEY_CURRENT_USER, KEY_READ, KEY_WRITE, REG_OPTION_NON_VOLATILE, REG_SZ, REG_VALUE_TYPE,
    };

    fn wide(text: &str) -> Vec<u16> {
        text.encode_utf16().chain(std::iter::once(0)).collect()
    }

    fn wide_bytes(text: &str) -> Vec<u8> {
        wide(text).into_iter().flat_map(u16::to_le_bytes).collect()
    }

    struct KeyGuard(HKEY);

    impl Drop for KeyGuard {
        fn drop(&mut self) {
            if self.0 != HKEY::default() {
                unsafe {
                    let _ = RegCloseKey(self.0);
                }
            }
        }
    }

    fn read_value(subkey: &str, name: &str) -> Option<String> {
        let subkey_w = wide(subkey);
        let name_w = wide(name);
        let mut raw = HKEY::default();
        unsafe {
            let status = RegOpenKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR(subkey_w.as_ptr()),
                None,
                KEY_READ,
                &mut raw,
            );
            if status != ERROR_SUCCESS {
                return None;
            }
        }
        let key = KeyGuard(raw);

        unsafe {
            let mut kind = REG_VALUE_TYPE::default();
            let mut len = 0u32;
            let status = RegQueryValueExW(
                key.0,
                PCWSTR(name_w.as_ptr()),
                None,
                Some(&mut kind),
                None,
                Some(&mut len),
            );
            if status != ERROR_SUCCESS || kind != REG_SZ || len == 0 {
                return None;
            }

            let mut buf = vec![0u8; len as usize];
            let status = RegQueryValueExW(
                key.0,
                PCWSTR(name_w.as_ptr()),
                None,
                Some(&mut kind),
                Some(buf.as_mut_ptr()),
                Some(&mut len),
            );
            if status != ERROR_SUCCESS {
                return None;
            }

            buf.truncate(len as usize);
            let text = String::from_utf16_lossy(
                &buf.chunks_exact(2)
                    .map(|c| u16::from_le_bytes([c[0], c[1]]))
                    .collect::<Vec<u16>>(),
            );
            Some(text.trim_end_matches('\0').to_string())
        }
    }

    /// 只注册 DisplayName 与 IconUri；IconBackgroundColor 留给 Windows 自定
    fn write_identity(subkey: &str, display_name: &str, icon_uri: &str) -> anyhow::Result<()> {
        let subkey_w = wide(subkey);
        let mut key = HKEY::default();
        unsafe {
            let status = RegCreateKeyExW(
                HKEY_CURRENT_USER,
                PCWSTR(subkey_w.as_ptr()),
                None,
                PCWSTR::null(),
                REG_OPTION_NON_VOLATILE,
                KEY_WRITE,
                None,
                &mut key,
                None,
            );
            anyhow::ensure!(status == ERROR_SUCCESS, "创建 {subkey} 失败 win32={}", status.0);
        }
        let key = KeyGuard(key);

        for (name, value) in [("DisplayName", display_name), ("IconUri", icon_uri)] {
            let name_w = wide(name);
            let data = wide_bytes(value);
            unsafe {
                let status = RegSetValueExW(key.0, PCWSTR(name_w.as_ptr()), None, REG_SZ, Some(&data));
                anyhow::ensure!(status == ERROR_SUCCESS, "写入 {name} 失败 win32={}", status.0);
            }
        }

        // 立刻回读，写进去却读不到等于没写
        anyhow::ensure!(
            read_value(subkey, "DisplayName").as_deref() == Some(display_name),
            "注册后回读不一致：{subkey}"
        );
        Ok(())
    }

    pub fn materialize(id: &str, png: &[u8]) -> std::io::Result<PathBuf> {
        const PNG_MAGIC: [u8; 8] = [0x89, b'P', b'N', b'G', 0x0D, 0x0A, 0x1A, 0x0A];
        if png.len() < PNG_MAGIC.len() || png[..PNG_MAGIC.len()] != PNG_MAGIC {
            return Err(std::io::Error::new(
                std::io::ErrorKind::InvalidData,
                "通知图标必须是 PNG",
            ));
        }

        let dir = match std::env::var_os("LOCALAPPDATA") {
            Some(base) => PathBuf::from(base).join("Taix").join("plugin-icons"),
            None => std::env::temp_dir(),
        };
        std::fs::create_dir_all(&dir)?;
        let path = dir.join(format!("{id}.png"));
        let current = std::fs::metadata(&path).map(|m| m.len()).unwrap_or(0);
        if current != png.len() as u64 {
            std::fs::write(&path, png)?;
        }
        Ok(path)
    }

    pub fn ensure(id: &str, display_name: &str, png: &[u8]) -> anyhow::Result<AppIdentity> {
        let icon = materialize(id, png)?;
        let icon_uri = icon.display().to_string();
        let subkey = identity_subkey(id);

        let up_to_date = read_value(&subkey, "DisplayName").as_deref() == Some(display_name)
            && read_value(&subkey, "IconUri").as_deref() == Some(icon_uri.as_str());
        if !up_to_date {
            write_identity(&subkey, display_name, &icon_uri)?;
            tracing::info!(
                target: "taix_notify",
                "已注册通知身份 {} ({})",
                identity_aumid(id),
                subkey
            );
        }

        Ok(AppIdentity {
            aumid: Some(identity_aumid(id)),
            icon: Some(icon),
        })
    }
}

/// Windows 声音名只认精确大小写，解析失败会被静默变成静音
#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
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

/// 声音名 → WinRT 事件名；Alarm/Call 系列是循环音，XML 里要带 loop
#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn sound_event(name: &str) -> String {
    if name.starts_with("Alarm") || name.starts_with("Call") {
        format!("ms-winsoundevent:Notification.Looping.{name}")
    } else {
        format!("ms-winsoundevent:Notification.{name}")
    }
}

#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn xml_escape(text: &str) -> String {
    let mut out = String::with_capacity(text.len());
    for ch in text.chars() {
        match ch {
            '&' => out.push_str("&amp;"),
            '<' => out.push_str("&lt;"),
            '>' => out.push_str("&gt;"),
            '"' => out.push_str("&quot;"),
            '\'' => out.push_str("&apos;"),
            _ => out.push(ch),
        }
    }
    out
}

/// toast 的 src 只认 URI，本地路径要转成 file:/// 形式
#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn file_uri(path: &Path) -> String {
    let text = path.to_string_lossy().replace('\\', "/");
    format!("file:///{}", text.trim_start_matches('/'))
}

/// 自己拼 XML 的原因：notify-rust 既给不了 appLogoOverride，也给不了 activationType/launch
/// 以后要接「点击打开插件页」就在这里补 launch 与 activationType="protocol"
#[cfg_attr(not(target_os = "windows"), allow(dead_code))]
fn build_toast_xml(title: &str, body: &str, opts: &NotifyOptions) -> String {
    let mut xml =
        String::from("<toast duration=\"short\"><visual><binding template=\"ToastGeneric\">");

    if let Some(icon) = opts.icon.as_deref() {
        xml.push_str(&format!(
            "<image src=\"{}\" placement=\"appLogoOverride\" hint-crop=\"none\"/>",
            xml_escape(&file_uri(icon))
        ));
    }

    xml.push_str(&format!("<text id=\"1\">{}</text>", xml_escape(title)));
    xml.push_str(&format!("<text id=\"2\">{}</text>", xml_escape(body)));
    xml.push_str("</binding></visual>");

    match opts.sound.map(normalize_sound) {
        // 循环音靠事件名区分（.Looping.*），不加 loop 属性，与 notify-rust 的 Single 变体一致
        Some(name) => xml.push_str(&format!("<audio src=\"{}\"/>", sound_event(&name))),
        None => xml.push_str("<audio silent=\"true\"/>"),
    }

    xml.push_str("</toast>");
    xml
}

/// 失败只记日志，不 panic
pub fn notify(title: &str, body: &str, opts: &NotifyOptions) -> anyhow::Result<()> {
    tracing::debug!(target: "taix_notify", "notify: {title} / {body}");

    #[cfg(target_os = "windows")]
    {
        use windows::core::HSTRING;
        use windows::Data::Xml::Dom::XmlDocument;
        use windows::UI::Notifications::{ToastNotification, ToastNotificationManager};

        let xml = build_toast_xml(title, body, opts);
        let document = XmlDocument::new().context("创建 Toast XML 文档失败")?;
        document
            .LoadXml(&HSTRING::from(xml.as_str()))
            .context("Toast XML 解析失败")?;
        let toast =
            ToastNotification::CreateToastNotification(&document).context("构造 Toast 失败")?;
        let aumid = HSTRING::from(effective_aumid(opts.aumid.as_deref()));
        ToastNotificationManager::CreateToastNotifierWithId(&aumid)
            .context("创建 ToastNotifier 失败")?
            .Show(&toast)
            .context("Windows Toast 发送失败")?;

        tracing::info!(
            target: "taix_notify",
            "toast 已提交 aumid={} sound={:?} icon={}",
            effective_aumid(opts.aumid.as_deref()),
            opts.sound,
            opts.icon.is_some()
        );
    }

    #[cfg(not(target_os = "windows"))]
    {
        // macOS 无交互按钮，也没有图标参数
        let _ = (&opts.actions, &opts.icon);
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

    #[test]
    fn sound_name_is_normalized() {
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

    #[test]
    fn sound_events_keep_looping_names() {
        assert_eq!(sound_event("Default"), "ms-winsoundevent:Notification.Default");
        assert_eq!(sound_event("SMS"), "ms-winsoundevent:Notification.SMS");
        assert_eq!(
            sound_event("Alarm2"),
            "ms-winsoundevent:Notification.Looping.Alarm2"
        );
        assert_eq!(
            sound_event("Call10"),
            "ms-winsoundevent:Notification.Looping.Call10"
        );
    }

    #[test]
    fn identity_is_per_plugin() {
        assert_eq!(identity_aumid("reminder"), "Taix.Plugin.reminder");
        assert_eq!(
            identity_subkey("reminder"),
            r"SOFTWARE\Classes\AppUserModelId\Taix.Plugin.reminder"
        );
    }

    #[test]
    fn icon_bytes_must_be_png() {
        let png = std::fs::read(concat!(
            env!("CARGO_MANIFEST_DIR"),
            "/../taix-plugins/reminder/assets/icon.png"
        ))
        .expect("reminder 图标应可读取");
        let path = win_materialize("png-check", &png).expect("PNG 应能落盘");
        assert_eq!(
            std::fs::read(&path).expect("落盘后应可读").len(),
            png.len()
        );

        let not_png = b"not an image at all";
        let err = win_materialize("png-check", not_png).expect_err("非 PNG 必须被拒");
        assert_eq!(err.kind(), std::io::ErrorKind::InvalidData);
        let _ = std::fs::remove_file(path);
    }

    #[cfg(target_os = "windows")]
    fn win_materialize(id: &str, png: &[u8]) -> std::io::Result<std::path::PathBuf> {
        win_identity::materialize(id, png)
    }

    #[cfg(not(target_os = "windows"))]
    fn win_materialize(_: &str, _: &[u8]) -> std::io::Result<std::path::PathBuf> {
        Err(std::io::Error::new(
            std::io::ErrorKind::Unsupported,
            "非 Windows 不走注册表",
        ))
    }

    #[test]
    fn toast_xml_carries_icon_text_and_sound() {
        let opts = NotifyOptions {
            sound: Some("alarm2"),
            icon: Some(PathBuf::from(r"C:\Icons\reminder.png")),
            ..Default::default()
        };
        let xml = build_toast_xml("用了<2>小时 & \"久\"", "该休息了\n站起来走走", &opts);

        assert!(xml.contains(
            "<image src=\"file:///C:/Icons/reminder.png\" placement=\"appLogoOverride\" hint-crop=\"none\"/>"
        ));
        assert!(xml.contains("<text id=\"1\">用了&lt;2&gt;小时 &amp; &quot;久&quot;</text>"));
        assert!(xml.contains("<text id=\"2\">该休息了\n站起来走走</text>"));
        // 循环音只体现在事件名上，不加 loop 属性
        assert!(xml.contains("<audio src=\"ms-winsoundevent:Notification.Looping.Alarm2\"/>"));
    }

    #[test]
    fn toast_xml_without_icon_is_silent_when_asked() {
        let xml = build_toast_xml("t", "b", &NotifyOptions::default());
        assert!(!xml.contains("<image"));
        assert!(xml.contains("<audio silent=\"true\"/>"));
        assert!(xml.contains("<binding template=\"ToastGeneric\">"));
    }
}
