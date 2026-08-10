use serde::{Deserialize, Serialize};
use sqlx::FromRow;

use super::category::CategoryModel;

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct AppModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "Name")]
    pub name: Option<String>,
    #[sqlx(rename = "Alias")]
    pub alias: Option<String>,
    #[sqlx(rename = "Description")]
    pub description: Option<String>,
    #[sqlx(rename = "File")]
    pub file: Option<String>,
    #[sqlx(rename = "CategoryID")]
    pub category_id: i64,
    #[sqlx(rename = "IconFile")]
    pub icon_file: Option<String>,
    #[sqlx(rename = "TotalTime")]
    pub total_time: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub category: Option<CategoryModel>,
}


#[derive(Debug, Clone, FromRow)]
pub struct AppJoinCols {
    pub app_id: Option<i64>,
    pub app_name: Option<String>,
    pub app_alias: Option<String>,
    pub app_description: Option<String>,
    pub app_file: Option<String>,
    pub app_category_id: Option<i64>,
    pub app_icon_file: Option<String>,
    pub app_total_time: Option<i64>,
    pub cat_id: Option<i64>,
    pub cat_name: Option<String>,
    pub cat_icon_file: Option<String>,
    pub cat_color: Option<String>,
    pub cat_is_directory_match: Option<bool>,
    pub cat_directories: Option<String>,
    pub cat_is_system: Option<bool>,
}

impl AppJoinCols {
    pub fn to_app_model(&self) -> Option<AppModel> {
        let id = self.app_id?;
        Some(AppModel {
            id,
            name: self.app_name.clone(),
            alias: self.app_alias.clone(),
            description: self.app_description.clone(),
            file: self.app_file.clone(),
            category_id: self.app_category_id.unwrap_or(0),
            icon_file: self.app_icon_file.clone(),
            total_time: self.app_total_time.unwrap_or(0),
            category: self.cat_id.map(|cid| CategoryModel {
                id: cid,
                name: self.cat_name.clone(),
                icon_file: self.cat_icon_file.clone(),
                color: self.cat_color.clone(),
                is_directory_match: self.cat_is_directory_match.unwrap_or(false),
                directories: self.cat_directories.clone(),
                is_system: self.cat_is_system.unwrap_or(false),
            }),
        })
    }
}


pub const APP_JOIN_COLS_SQL: &str = r#"
a.ID AS app_id, a.Name AS app_name, a.Alias AS app_alias, a.Description AS app_description,
a.File AS app_file, a.CategoryID AS app_category_id, a.IconFile AS app_icon_file, a.TotalTime AS app_total_time,
c.ID AS cat_id, c.Name AS cat_name, c.IconFile AS cat_icon_file, c.Color AS cat_color,
c.IsDirectoryMatch AS cat_is_directory_match, c.Directories AS cat_directories, c.IsSystem AS cat_is_system
"#;
