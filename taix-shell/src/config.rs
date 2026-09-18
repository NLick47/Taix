use serde::Deserialize;
use std::path::Path;

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Theme {
    System,
    Light,
    Dark,
}

impl Theme {
    pub fn from_i32(v: i32) -> Self {
        match v {
            1 | 3 => Self::Light,
            2 => Self::Dark,
            _ => Self::System,
        }
    }

    pub fn from_name(v: &str) -> Option<Self> {
        match v.to_ascii_lowercase().as_str() {
            "system" => Some(Self::System),
            "light" => Some(Self::Light),
            "dark" => Some(Self::Dark),
            _ => None,
        }
    }
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Language {
    Auto,
    ZhCn,
    EnUs,
}

impl Language {
    pub fn from_i32(v: i32) -> Self {
        match v {
            1 => Self::ZhCn,
            2 => Self::EnUs,
            _ => Self::Auto,
        }
    }

    pub fn from_name(v: &str) -> Option<Self> {
        match v.to_ascii_lowercase().as_str() {
            "auto" => Some(Self::Auto),
            "zh" | "zh-cn" => Some(Self::ZhCn),
            "en" | "en-us" => Some(Self::EnUs),
            _ => None,
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TrayConfig {
    pub theme: Theme,
    pub language: Language,
    pub is_visible: bool,
}

impl Default for TrayConfig {
    fn default() -> Self {
        Self {
            theme: Theme::System,
            language: Language::Auto,
            is_visible: true,
        }
    }
}

#[derive(Debug, Clone, Deserialize)]
struct AppConfigFile {
    #[serde(rename = "General")]
    general: GeneralFile,
    #[serde(rename = "Behavior", default)]
    behavior: Option<BehaviorFile>,
}

fn default_true() -> bool {
    true
}

#[derive(Debug, Clone, Deserialize)]
struct GeneralFile {
    #[serde(rename = "Theme")]
    theme: i32,
    #[serde(rename = "Language")]
    language: i32,
    #[serde(rename = "IsEnableTray", default = "default_true")]
    is_enable_tray: bool,
}

#[derive(Debug, Clone, Deserialize, Default)]
struct BehaviorFile {
    #[serde(rename = "InactiveThreshold", default = "default_inactive_threshold")]
    inactive_threshold: i32,
    #[serde(rename = "MaxSoundDuration", default = "default_max_sound_duration")]
    max_sound_duration: i32,
    #[serde(rename = "IsSleepWatch", default = "default_true")]
    is_sleep_watch: bool,
}

fn default_inactive_threshold() -> i32 { 15 }
fn default_max_sound_duration() -> i32 { 120 }

pub fn load_tray_config(data_dir: &Path) -> Option<TrayConfig> {
    let path = data_dir.join("AppConfig.json");
    if !path.exists() {
        return None;
    }
    let mut content = match std::fs::read_to_string(&path) {
        Ok(content) => content,
        Err(e) => {
            tracing::warn!(target: "taix_shell::config", "failed to read {:?}: {}", path, e);
            return None;
        }
    };
    if content.starts_with('\u{FEFF}') {
        content.remove(0);
    }
    let config: AppConfigFile = match serde_json::from_str(&content) {
        Ok(config) => config,
        Err(e) => {
            tracing::warn!(target: "taix_shell::config", "failed to parse {:?}: {}", path, e);
            return None;
        }
    };

    let tray = TrayConfig {
        theme: Theme::from_i32(config.general.theme),
        language: Language::from_i32(config.general.language),
        is_visible: config.general.is_enable_tray,
    };
    tracing::info!(
        target: "taix_shell::config",
        "AppConfig.json tray: Theme={}→{:?} Language={}→{:?} IsEnableTray={}→is_visible={}",
        config.general.theme,
        tray.theme,
        config.general.language,
        tray.language,
        config.general.is_enable_tray,
        tray.is_visible
    );
    Some(tray)
}

pub fn load_monitor_config(data_dir: &Path) -> crate::service_manager::MonitorConfig {
    let path = data_dir.join("AppConfig.json");
    let default = crate::service_manager::MonitorConfig::default();

    let mut content = match std::fs::read_to_string(&path) {
        Ok(content) => content,
        Err(e) => {
            tracing::debug!(
                target: "taix_shell::config",
                "monitor config unavailable ({:?}: {}); using defaults",
                path, e
            );
            return default;
        }
    };
    if content.starts_with('\u{FEFF}') {
        content.remove(0);
    }
    let config: AppConfigFile = match serde_json::from_str(&content) {
        Ok(config) => config,
        Err(e) => {
            tracing::debug!(
                target: "taix_shell::config",
                "monitor config unparsable ({:?}: {}); using defaults",
                path, e
            );
            return default;
        }
    };

    let behavior = config.behavior.unwrap_or_default();
    let monitor = crate::service_manager::MonitorConfig {
        inactive_threshold: behavior.inactive_threshold.clamp(1, 60),
        max_sound_duration: behavior.max_sound_duration.clamp(15, 480),
        sleep_watch: behavior.is_sleep_watch,
    };
    tracing::info!(
        target: "taix_shell::config",
        "AppConfig.json behavior: InactiveThreshold={}→{} MaxSoundDuration={}→{} IsSleepWatch={}→{} (values shown post-clamp)",
        behavior.inactive_threshold,
        monitor.inactive_threshold,
        behavior.max_sound_duration,
        monitor.max_sound_duration,
        behavior.is_sleep_watch,
        monitor.sleep_watch
    );
    monitor
}
