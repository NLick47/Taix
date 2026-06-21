use crate::constants;
use serde::{Deserialize, Serialize};

pub const CURRENT_CONFIG_VERSION: u32 = 3;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct ConfigModel {
    #[serde(rename = "Version", default)]
    pub version: u32,
    #[serde(rename = "General")]
    pub general: GeneralModel,
    #[serde(rename = "Behavior")]
    pub behavior: BehaviorModel,
    #[serde(rename = "Shortcut")]
    pub shortcut: ShortcutModel,
}

impl Default for ConfigModel {
    fn default() -> Self {
        Self {
            version: CURRENT_CONFIG_VERSION,
            general: GeneralModel::default(),
            behavior: BehaviorModel::default(),
            shortcut: ShortcutModel::default(),
        }
    }
}

impl ConfigModel {
    pub fn validate(&self) -> Result<(), String> {
        if self.version == 0 {
            return Err("配置版本缺失或无效".to_string());
        }

        let sync_url = self.general.sync_url.trim();
        if !sync_url.is_empty()
            && !sync_url.starts_with("http://") && !sync_url.starts_with("https://") {
                return Err("同步地址(SyncUrl)必须以 http:// 或 https:// 开头".to_string());
            }

        if self.general.data_retention_days < 1 || self.general.data_retention_days > 9999 {
            return Err("数据保存天数必须在 1 到 9999 之间".to_string());
        }

        Ok(())
    }

    pub fn migrate(&mut self) {
        if self.version >= CURRENT_CONFIG_VERSION {
            return;
        }

        if self.version < 3 {
            self.general.data_retention_days = 31;
        }

        self.version = CURRENT_CONFIG_VERSION;
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct GeneralModel {
    #[serde(rename = "Theme")]
    pub theme: i32,
    #[serde(rename = "ThemeColor")]
    pub theme_color: String,
    #[serde(rename = "Language")]
    pub language: i32,
    #[serde(rename = "StartPage")]
    pub start_page: i32,
    #[serde(rename = "IsAutoUpdate")]
    pub is_auto_update: bool,
    #[serde(rename = "IndexPageFrequentUseNum")]
    pub index_page_frequent_use_num: i32,
    #[serde(rename = "IndexPageMoreNum")]
    pub index_page_more_num: i32,
    #[serde(rename = "IsWebEnabled")]
    pub is_web_enabled: bool,
    #[serde(rename = "IsEnableTray")]
    pub is_enable_tray: bool,
    #[serde(rename = "SyncUrl")]
    pub sync_url: String,
    #[serde(rename = "DataRetentionDays")]
    pub data_retention_days: i32,
    #[serde(rename = "WindowGradientScheme")]
    pub window_gradient_scheme: i32,
}

impl Default for GeneralModel {
    fn default() -> Self {
        Self {
            theme: 0,
            theme_color: "#FFFF1BBC".to_string(),
            language: 0,
            start_page: 0,
            is_auto_update: true,
            index_page_frequent_use_num: constants::DEFAULT_FREQUENT_USE_NUM,
            index_page_more_num: constants::DEFAULT_MORE_NUM,
            is_web_enabled: false,
            is_enable_tray: true,
            sync_url: String::new(),
            data_retention_days: 31,
            window_gradient_scheme: 3,
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct BehaviorModel {
    #[serde(rename = "IsSleepWatch")]
    pub is_sleep_watch: bool,
    #[serde(rename = "IgnoreProcessList")]
    pub ignore_process_list: Vec<String>,
    #[serde(rename = "IgnoreUrlList", alias = "IgnoreURLList")]
    pub ignore_url_list: Vec<String>,
    #[serde(rename = "IsWhiteList")]
    pub is_white_list: bool,
    #[serde(rename = "ProcessWhiteList")]
    pub process_white_list: Vec<String>,
}

impl Default for BehaviorModel {
    fn default() -> Self {
        Self {
            is_sleep_watch: true,
            ignore_process_list: vec!["Taix".to_string(), "Tai".to_string()],
            ignore_url_list: Vec::new(),
            is_white_list: false,
            process_white_list: Vec::new(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct ShortcutModel {
    #[serde(rename = "Refresh")]
    pub refresh: String,
    #[serde(rename = "Search")]
    pub search: String,
    #[serde(rename = "NavigateBack")]
    pub navigate_back: String,
}

impl Default for ShortcutModel {
    fn default() -> Self {
        Self {
            refresh: "F5".to_string(),
            search: "Ctrl+K".to_string(),
            navigate_back: "Alt+Left".to_string(),
        }
    }
}


