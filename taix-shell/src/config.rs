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
            1 => Self::Light,
            2 => Self::Dark,
            _ => Self::System,
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
    let mut content = std::fs::read_to_string(&path).ok()?;
    if content.starts_with('\u{FEFF}') {
        content.remove(0);
    }
    let config: AppConfigFile = serde_json::from_str(&content).ok()?;
    Some(TrayConfig {
        theme: Theme::from_i32(config.general.theme),
        language: Language::from_i32(config.general.language),
        is_visible: config.general.is_enable_tray,
    })
}

pub fn load_monitor_config(data_dir: &Path) -> crate::service_manager::MonitorConfig {
    let path = data_dir.join("AppConfig.json");
    if !path.exists() {
        return crate::service_manager::MonitorConfig::default();
    }
    let mut content = match std::fs::read_to_string(&path) {
        Ok(c) => c,
        Err(_) => return crate::service_manager::MonitorConfig::default(),
    };
    if content.starts_with('\u{FEFF}') {
        content.remove(0);
    }
    let config: AppConfigFile = match serde_json::from_str(&content) {
        Ok(c) => c,
        Err(_) => return crate::service_manager::MonitorConfig::default(),
    };

    let behavior = config.behavior.unwrap_or_default();
    crate::service_manager::MonitorConfig {
        inactive_threshold: behavior.inactive_threshold.clamp(1, 60),
        max_sound_duration: behavior.max_sound_duration.clamp(15, 480),
        sleep_watch: behavior.is_sleep_watch,
    }
}