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

    pub fn resolve(self) -> Self {
        if self == Self::Auto {
            #[cfg(target_os = "windows")]
            {
                crate::platform::detect_system_language()
            }
            #[cfg(not(target_os = "windows"))]
            Self::EnUs
        } else {
            self
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


