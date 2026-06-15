use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::FromRow;

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct WebSiteModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "Title")]
    pub title: Option<String>,
    #[sqlx(rename = "Domain")]
    pub domain: Option<String>,
    #[sqlx(rename = "Alias")]
    pub alias: Option<String>,
    #[serde(alias = "categoryID")]
    #[sqlx(rename = "CategoryID")]
    pub category_id: i64,
    #[sqlx(rename = "IconFile")]
    pub icon_file: Option<String>,
    #[sqlx(rename = "Duration")]
    pub duration: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub category: Option<WebSiteCategoryModel>,
}

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct WebSiteCategoryModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "Name")]
    pub name: String,
    #[sqlx(rename = "IconFile")]
    pub icon_file: Option<String>,
    #[sqlx(rename = "Color")]
    pub color: Option<String>,
    #[serde(default)]
    #[sqlx(default)]
    #[sqlx(rename = "IsUrlMatch")]
    pub is_url_match: bool,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(default)]
    #[sqlx(rename = "UrlPatterns")]
    pub url_patterns: Option<String>,
    #[serde(default)]
    #[sqlx(default)]
    #[sqlx(rename = "IsSystem")]
    pub is_system: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct WebUrlModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "Title")]
    pub title: Option<String>,
    #[sqlx(rename = "Url")]
    pub url: Option<String>,
    #[sqlx(rename = "IconFile")]
    pub icon_file: Option<String>,
}

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct WebBrowseLogModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "UrlId")]
    pub url_id: i64,
    #[sqlx(rename = "LogTime")]
    pub log_time: DateTime<Utc>,
    #[sqlx(rename = "Duration")]
    pub duration: i64,
    #[sqlx(rename = "SiteId")]
    pub site_id: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub site: Option<WebSiteModel>,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub url: Option<WebUrlModel>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct WebExportDataResult {
    pub logs: Vec<WebBrowseLogModel>,
}
