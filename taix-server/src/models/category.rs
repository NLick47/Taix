use serde::{Deserialize, Serialize};
use sqlx::FromRow;

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct CategoryModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "Name")]
    pub name: Option<String>,
    #[sqlx(rename = "IconFile")]
    pub icon_file: Option<String>,
    #[sqlx(rename = "Color")]
    pub color: Option<String>,
    #[serde(default)]
    #[sqlx(rename = "IsDirectoryMatch")]
    pub is_directory_match: bool,
    #[sqlx(rename = "Directories")]
    pub directories: Option<String>,
    #[serde(default)]
    #[sqlx(default)]
    #[sqlx(rename = "IsSystem")]
    pub is_system: bool,
}
