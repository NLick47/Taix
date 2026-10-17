use std::collections::HashSet;
use std::path::{Path, PathBuf};

use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct PluginManifest {
    /// ^[a-z0-9-]{1,32}$，不得为 webui
    pub id: String,
    pub name: String,
    /// x.y.z
    pub version: String,
    /// 目录内文件名；Windows 上可省略 .exe
    pub entry: String,
    /// 空 = 全平台
    #[serde(default)]
    pub platforms: Vec<String>,
    /// true = 常驻小窗
    #[serde(default)]
    pub display: bool,
    /// webui 表单渲染提示，不做校验
    #[serde(default)]
    pub config_schema: Vec<ConfigField>,
    /// 插件自带的配置 UI 入口模块，ui/ 下的单文件名
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub config_ui: Option<String>,
    /// 预留字段；首版不生效
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub permissions: Vec<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ConfigField {
    pub key: String,
    pub label: String,
    /// string | text | number | bool | select | json
    #[serde(rename = "type")]
    pub field_type: String,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub min: Option<f64>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub max: Option<f64>,
    #[serde(default, skip_serializing_if = "Vec::is_empty")]
    pub options: Vec<String>,
    #[serde(default, skip_serializing_if = "Option::is_none")]
    pub default: Option<serde_json::Value>,
}

/// shell 用它算 mtime、定位 entry
#[derive(Debug, Clone)]
pub struct Installed {
    pub manifest: PluginManifest,
    pub json_path: PathBuf,
}

impl Installed {
    pub fn dir(&self) -> PathBuf {
        self.json_path
            .parent()
            .map(PathBuf::from)
            .unwrap_or_default()
    }
}

const RESERVED_IDS: [&str; 1] = ["webui"];

/// 同一问题只告警一次：重扫每 5s 一次，不变量不该刷屏
///
/// server 与 shell 共用；返回 true = 首次出现，应当告警
pub fn log_once(key: &str) -> bool {
    use std::sync::{Mutex, OnceLock};
    static SEEN: OnceLock<Mutex<HashSet<String>>> = OnceLock::new();
    let seen = SEEN.get_or_init(|| Mutex::new(HashSet::new()));
    let mut seen = seen.lock().unwrap_or_else(|e| e.into_inner());
    seen.insert(key.to_string())
}

/// 单条失败只跳过并告警；平台不适用按跳过处理；同 id 保留先扫描到的
pub fn scan(plugins_dir: &Path) -> Vec<Installed> {
    let mut out: Vec<Installed> = Vec::new();
    let mut seen: HashSet<String> = HashSet::new();

    let dir_entries = match std::fs::read_dir(plugins_dir) {
        Ok(entries) => entries,
        Err(_) => return out, // 目录不存在 = 空集
    };

    for entry in dir_entries.flatten() {
        let json_path = entry.path().join("plugin.json");
        if !json_path.is_file() {
            continue;
        }

        match read_manifest(&json_path) {
            Some(manifest) => {
                if !platform_matches(&manifest) {
                    if log_once(&format!("platform|{}", json_path.display())) {
                        tracing::info!(
                            "plugin '{}' declares platforms {:?}, not applicable to {}, skipping",
                            manifest.id,
                            manifest.platforms,
                            current_platform()
                        );
                    } else {
                        tracing::debug!("plugin '{}' still not applicable, skipping", manifest.id);
                    }
                    continue;
                }
                if seen.contains(&manifest.id) {
                    if log_once(&format!("dup|{}", json_path.display())) {
                        tracing::warn!(
                            "duplicate plugin id '{}' in {}, keeping the first one",
                            manifest.id,
                            json_path.display()
                        );
                    } else {
                        tracing::debug!("duplicate plugin id '{}' still present", manifest.id);
                    }
                    continue;
                }
                seen.insert(manifest.id.clone());
                out.push(Installed {
                    manifest,
                    json_path,
                });
            }
            None => {
                if log_once(&format!("invalid|{}", json_path.display())) {
                    tracing::warn!("skipped invalid plugin manifest {}", json_path.display());
                } else {
                    tracing::debug!("invalid plugin manifest still present {}", json_path.display());
                }
            }
        }
    }

    out
}

fn read_manifest(path: &Path) -> Option<PluginManifest> {
    let mut content = std::fs::read_to_string(path).ok()?;
    if content.starts_with('\u{FEFF}') {
        content.remove(0);
    }

    let manifest: PluginManifest = serde_json::from_str(&content).ok()?;
    validate(&manifest).then_some(manifest)
}

fn validate(m: &PluginManifest) -> bool {
    valid_id(&m.id)
        && !m.name.trim().is_empty()
        && valid_version(&m.version)
        && valid_entry(&m.entry)
        // 只查平台值拼写；是否适用当前平台由 scan 过滤
        && m.platforms.iter().all(|p| p == "windows" || p == "macos")
        && m.permissions.iter().all(|p| !p.trim().is_empty() && p.len() <= 32)
        && m.config_schema.iter().all(|f| {
            !f.key.trim().is_empty()
                && matches!(
                    f.field_type.as_str(),
                    "string" | "text" | "number" | "bool" | "select" | "json"
                )
        })
        && m.config_ui.as_deref().map(valid_config_ui).unwrap_or(true)
}

/// configUi 是 ui/ 下的单文件名，规则同 entry，另排除 URL 与文件系统里需要转义的字符
fn valid_config_ui(name: &str) -> bool {
    name.len() <= 128
        && valid_entry(name)
        && !name
            .chars()
            .any(|c| matches!(c, ':' | '?' | '#' | '%') || c.is_whitespace() || c.is_control())
}

pub fn valid_id(id: &str) -> bool {
    (1..=32).contains(&id.len())
        && id.chars().all(|c| c.is_ascii_lowercase() || c.is_ascii_digit() || c == '-')
        && !RESERVED_IDS.contains(&id)
}

fn valid_version(v: &str) -> bool {
    let parts: Vec<&str> = v.split('.').collect();
    parts.len() == 3 && parts.iter().all(|p| !p.is_empty() && p.chars().all(|c| c.is_ascii_digit()))
}

/// 禁止路径分隔与 ..；纯空白名字会进列表却永远解析不到文件
fn valid_entry(entry: &str) -> bool {
    !entry.trim().is_empty()
        && !entry.contains('/')
        && !entry.contains('\\')
        && entry != "."
        && entry != ".."
}

pub fn current_platform() -> &'static str {
    if cfg!(target_os = "windows") {
        "windows"
    } else if cfg!(target_os = "macos") {
        "macos"
    } else {
        "unknown"
    }
}

pub fn platform_matches(m: &PluginManifest) -> bool {
    m.platforms.is_empty() || m.platforms.iter().any(|p| p == current_platform())
}

/// 先按字面找；Windows 上再补一次 .exe
pub fn resolve_entry(dir: &Path, entry: &str) -> Option<PathBuf> {
    let literal = dir.join(entry);
    if literal.exists() {
        return Some(literal);
    }
    #[cfg(target_os = "windows")]
    {
        if !entry.ends_with(".exe") {
            let with_ext = dir.join(format!("{entry}.exe"));
            if with_ext.exists() {
                return Some(with_ext);
            }
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    fn manifest(id: &str, entry: &str) -> PluginManifest {
        PluginManifest {
            id: id.to_string(),
            name: "测试".to_string(),
            version: "1.0.0".to_string(),
            entry: entry.to_string(),
            platforms: Vec::new(),
            display: false,
            config_schema: Vec::new(),
            config_ui: None,
            permissions: Vec::new(),
        }
    }

    #[test]
    fn id_rules() {
        assert!(valid_id("my-plugin"));
        assert!(!valid_id("webui"));
        assert!(!valid_id("My_Plugin"));
        assert!(!valid_id(""));
        assert!(!valid_id(&"a".repeat(33)));
    }

    #[test]
    fn entry_is_required() {
        let raw = r#"{"id":"demo","name":"Demo","version":"1.0.0"}"#;
        assert!(
            serde_json::from_str::<PluginManifest>(raw).is_err(),
            "缺 entry 的清单不得通过解析"
        );
    }

    #[test]
    fn entry_rules() {
        assert!(valid_entry("taix-plugin-demo.exe"));
        assert!(valid_entry("taix-plugin-demo"));
        assert!(!valid_entry("../evil"));
        assert!(!valid_entry("a\\b"));
        assert!(!valid_entry("."));
        assert!(!valid_entry(""));
    }

    #[test]
    fn version_and_platform_rules() {
        let other_platform = if current_platform() == "windows" {
            "macos"
        } else {
            "windows"
        };
        let mut m = manifest("demo", "taix-plugin-demo");
        assert!(validate(&m));

        m.version = "1.0".to_string();
        assert!(!validate(&m), "两位版本号应被拒绝");
        m.version = "1.0.0".to_string();

        m.platforms = vec!["linux".to_string()];
        assert!(!validate(&m), "非 windows/macos 平台值应被拒绝");

        m.platforms = vec![other_platform.to_string()];
        assert!(validate(&m), "只声明其他平台仍是合法清单");
        assert!(!platform_matches(&m), "但不应匹配当前平台");

        m.platforms = vec![current_platform().to_string()];
        assert!(validate(&m), "包含当前平台应当通过");
        assert!(platform_matches(&m));
    }

    #[test]
    fn json_field_type_is_allowed() {
        let mut m = manifest("demo", "taix-plugin-demo");
        m.config_schema = vec![ConfigField {
            key: "rules".to_string(),
            label: "规则".to_string(),
            field_type: "json".to_string(),
            min: None,
            max: None,
            options: Vec::new(),
            default: Some(serde_json::json!([{ "type": "continuous" }])),
        }];
        assert!(validate(&m));

        m.config_schema[0].field_type = "date".to_string();
        assert!(!validate(&m), "未知字段类型应被拒绝");
    }

    #[test]
    fn config_ui_is_optional_single_file_name() {
        let mut m = manifest("demo", "taix-plugin-demo");
        assert!(validate(&m), "缺省无自定义配置 UI 应合法");

        m.config_ui = Some("config.js".to_string());
        assert!(validate(&m));

        for bad in ["", "ui/config.js", "a\\b.js", ".", "..", "C:config.js", "a b.js", "a#b.js", "a%20.js"] {
            m.config_ui = Some(bad.to_string());
            assert!(!validate(&m), "非法 configUi 应被拒绝: {bad:?}");
        }
        m.config_ui = Some(format!("{}.js", "a".repeat(130)));
        assert!(!validate(&m), "超长 configUi 应被拒绝");
    }

    #[test]
    fn permissions_are_length_checked() {
        let mut m = manifest("demo", "taix-plugin-demo");
        m.permissions = vec!["notify".to_string(), "network".to_string()];
        assert!(validate(&m), "未知权限名不影响合法性");

        m.permissions = vec![String::new()];
        assert!(!validate(&m), "空 permission 应被拒绝");
        m.permissions = vec!["a".repeat(33)];
        assert!(!validate(&m), "超过 32 字符应被拒绝");
    }

    // 随包清单必须始终通过校验；手改 plugin.json 踩坏规则时这里会红
    #[test]
    fn shipped_manifests_are_valid() {
        let demo: PluginManifest =
            serde_json::from_str(include_str!("../../taix-plugins/demo/plugin.json")).unwrap();
        assert_eq!(demo.id, "demo");
        assert!(validate(&demo), "demo 清单未通过校验: {demo:?}");
        assert!(platform_matches(&demo), "demo 缺省 platforms 应全平台可用");

        let reminder: PluginManifest =
            serde_json::from_str(include_str!("../../taix-plugins/reminder/plugin.json")).unwrap();
        assert_eq!(reminder.id, "reminder");
        assert!(validate(&reminder), "reminder 清单未通过校验: {reminder:?}");
        assert!(platform_matches(&reminder), "reminder 缺省 platforms 应全平台可用");
        assert_eq!(
            reminder.config_ui.as_deref(),
            Some("config.js"),
            "reminder 自带配置 UI"
        );
    }

    #[test]
    fn scan_filters_by_platform_without_treating_it_as_invalid() {
        let other_platform = if current_platform() == "windows" {
            "macos"
        } else {
            "windows"
        };
        let dir = std::env::temp_dir().join(format!("taix-manifest-scan-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&dir);
        for (id, platforms) in [
            ("good", "[]"),
            ("other", &format!("[\"{other_platform}\"]")),
        ] {
            let d = dir.join(id);
            std::fs::create_dir_all(&d).unwrap();
            std::fs::write(
                d.join("plugin.json"),
                format!(
                    r#"{{"id":"{id}","name":"{id}","version":"1.0.0","entry":"taix-plugin-{id}","platforms":{platforms}}}"#
                ),
            )
            .unwrap();
        }

        let found = scan(&dir);
        std::fs::remove_dir_all(&dir).ok();

        let ids: Vec<&str> = found.iter().map(|i| i.manifest.id.as_str()).collect();
        assert_eq!(ids, vec!["good"], "平台不适用的插件应被 scan 跳过，其余保留");
    }

    #[test]
    fn repeated_problems_are_logged_once() {
        assert!(log_once("invalid|C:/plugins/bad/plugin.json"));
        assert!(!log_once("invalid|C:/plugins/bad/plugin.json"));
        assert!(log_once("invalid|C:/plugins/other/plugin.json"));
    }
}
