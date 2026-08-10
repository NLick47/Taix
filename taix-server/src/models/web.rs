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


#[derive(Debug, Clone, FromRow)]
pub struct WebSiteJoinCols {
    pub site_id: Option<i64>,
    pub site_title: Option<String>,
    pub site_domain: Option<String>,
    pub site_alias: Option<String>,
    pub site_category_id: Option<i64>,
    pub site_icon_file: Option<String>,
    pub site_duration: Option<i64>,
    pub cat_id: Option<i64>,
    pub cat_name: Option<String>,
    pub cat_icon_file: Option<String>,
    pub cat_color: Option<String>,
    pub cat_is_url_match: Option<bool>,
    pub cat_url_patterns: Option<String>,
    pub cat_is_system: Option<bool>,
}

impl WebSiteJoinCols {
    pub fn to_site_model(&self) -> Option<WebSiteModel> {
        let id = self.site_id?;
        Some(WebSiteModel {
            id,
            title: self.site_title.clone(),
            domain: self.site_domain.clone(),
            alias: self.site_alias.clone(),
            category_id: self.site_category_id.unwrap_or(0),
            icon_file: self.site_icon_file.clone(),
            duration: self.site_duration.unwrap_or(0),
            category: self.cat_id.map(|cid| WebSiteCategoryModel {
                id: cid,
                name: self.cat_name.clone().unwrap_or_default(),
                icon_file: self.cat_icon_file.clone(),
                color: self.cat_color.clone(),
                is_url_match: self.cat_is_url_match.unwrap_or(false),
                url_patterns: self.cat_url_patterns.clone(),
                is_system: self.cat_is_system.unwrap_or(false),
            }),
        })
    }
}

/// WebSiteModels 与 WebSiteCategoryModels 的 JOIN 列 SQL 片段（不含 s.Duration，
/// 避免与聚合查询的 SUM 列重名；需要站点静态时长的查询自行前置）
/// 查询需满足：FROM ... s LEFT JOIN WebSiteCategoryModels c ON s.CategoryID = c.ID
pub const WEB_SITE_JOIN_COLS_SQL: &str = r#"
s.ID AS site_id, s.Title AS site_title, s.Domain AS site_domain, s.Alias AS site_alias,
s.CategoryID AS site_category_id, s.IconFile AS site_icon_file,
c.ID AS cat_id, c.Name AS cat_name, c.IconFile AS cat_icon_file, c.Color AS cat_color,
c.IsUrlMatch AS cat_is_url_match, c.UrlPatterns AS cat_url_patterns, c.IsSystem AS cat_is_system
"#;
