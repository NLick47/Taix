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

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct AppModelRow {
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
}
