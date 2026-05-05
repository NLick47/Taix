use serde::{Deserialize, Serialize};

pub const CURRENT_CONFIG_VERSION: u32 = 1;

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct ConfigModel {
    #[serde(rename = "Version", default)]
    pub version: u32,
    #[serde(rename = "Links")]
    pub links: Vec<LinkModel>,
    #[serde(rename = "General")]
    pub general: GeneralModel,
    #[serde(rename = "Behavior")]
    pub behavior: BehaviorModel,
}

impl Default for ConfigModel {
    fn default() -> Self {
        Self {
            version: CURRENT_CONFIG_VERSION,
            links: Vec::new(),
            general: GeneralModel::default(),
            behavior: BehaviorModel::default(),
        }
    }
}

impl ConfigModel {
    pub fn validate(&self) -> Result<(), String> {
        if self.version == 0 {
            return Err("配置版本缺失或无效".to_string());
        }

        let sync_url = self.general.sync_url.trim();
        if !sync_url.is_empty() {
            if !sync_url.starts_with("http://") && !sync_url.starts_with("https://") {
                return Err("同步地址(SyncUrl)必须以 http:// 或 https:// 开头".to_string());
            }
        }

        Ok(())
    }

    pub fn migrate(&mut self) {
        if self.version >= CURRENT_CONFIG_VERSION {
            return;
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
}

impl Default for GeneralModel {
    fn default() -> Self {
        Self {
            theme: 0,
            theme_color: "#FFFF1BBC".to_string(),
            language: 0,
            start_page: 0,
            is_auto_update: true,
            index_page_frequent_use_num: 2,
            index_page_more_num: 11,
            is_web_enabled: false,
            is_enable_tray: false,
            sync_url: String::new(),
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
            ignore_process_list: Vec::new(),
            ignore_url_list: Vec::new(),
            is_white_list: false,
            process_white_list: Vec::new(),
        }
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(default)]
pub struct LinkModel {
    #[serde(rename = "Name")]
    pub name: String,
    #[serde(rename = "ProcessList")]
    pub process_list: Vec<String>,
}

impl Default for LinkModel {
    fn default() -> Self {
        Self {
            name: "新的关联".to_string(),
            process_list: Vec::new(),
        }
    }
}
