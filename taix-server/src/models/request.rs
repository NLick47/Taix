use chrono::NaiveDateTime;
use serde::{Deserialize, Serialize};

pub fn deserialize_bool_insensitive<'de, D>(deserializer: D) -> Result<bool, D::Error>
where
    D: serde::Deserializer<'de>,
{
    let s = String::deserialize(deserializer)?;
    match s.to_lowercase().as_str() {
        "true" | "1" => Ok(true),
        "false" | "0" => Ok(false),
        _ => Err(serde::de::Error::custom(format!("invalid boolean: {}", s))),
    }
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateAppDurationRequest {
    pub process_name: String,
    pub duration: i64,
    pub start_date_time: NaiveDateTime,
    pub file: Option<String>,
    pub icon_file: Option<String>,
    pub description: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateAppRequest {
    pub name: String,
    pub description: Option<String>,
    pub file: Option<String>,
    pub icon_file: Option<String>,
    #[serde(alias = "categoryID")]
    pub category_id: i64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateAppRequest {
    pub id: i64,
    pub name: String,
    pub alias: Option<String>,
    pub description: Option<String>,
    pub file: Option<String>,
    pub icon_file: Option<String>,
    #[serde(alias = "categoryID")]
    pub category_id: i64,
    pub total_time: i64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateCategoryRequest {
    pub name: String,
    pub icon_file: Option<String>,
    pub color: Option<String>,
    #[serde(default)]
    pub is_directory_math: bool,
    pub directories: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateCategoryRequest {
    pub id: i64,
    pub name: String,
    pub icon_file: Option<String>,
    pub color: Option<String>,
    #[serde(default)]
    pub is_directory_math: bool,
    pub directories: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AddUrlBrowseTimeRequest {
    pub url: String,
    pub title: Option<String>,
    pub duration: i64,
    pub date_time: Option<NaiveDateTime>,
    pub icon_url: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateSitesCategoryRequest {
    pub site_ids: Vec<i64>,
    pub category_id: i64,
}


