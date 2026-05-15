use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::FromRow;

use super::app::AppModel;

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct DailyLogModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    /// UTC 日期 (YYYY-MM-DD)，按 UTC 00:00 边界汇总。
    /// 查询时由前端传入本地日期，后端转换为 UTC 日期进行检索。
    #[sqlx(rename = "Date")]
    pub date: chrono::NaiveDate,
    #[sqlx(rename = "AppModelID")]
    pub app_model_id: i64,
    #[sqlx(rename = "Time")]
    pub time: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub app_model: Option<AppModel>,
}

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct HoursLogModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "DataTime")]
    pub data_time: DateTime<Utc>,
    #[sqlx(rename = "AppModelID")]
    pub app_model_id: i64,
    #[sqlx(rename = "Time")]
    pub time: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub app_model: Option<AppModel>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ColumnDataModel {
    #[serde(skip_serializing_if = "Option::is_none")]
    pub app_id: Option<i64>,
    #[serde(skip_serializing_if = "Option::is_none")]
    pub category_id: Option<i64>,
    pub values: Vec<f64>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct InfrastructureDataModel {
    pub id: i64,
    pub name: String,
    pub value: i64,
}

#[derive(Debug, Clone, Serialize, Deserialize, FromRow)]
#[serde(rename_all = "camelCase")]
pub struct AppSessionModel {
    #[sqlx(rename = "ID")]
    pub id: i64,
    #[sqlx(rename = "AppModelID")]
    pub app_model_id: i64,
    #[sqlx(rename = "StartTime")]
    pub start_time: DateTime<Utc>,
    #[sqlx(rename = "EndTime")]
    pub end_time: DateTime<Utc>,
    #[sqlx(rename = "Duration")]
    pub duration: i64,
    #[serde(skip_serializing_if = "Option::is_none")]
    #[sqlx(skip)]
    pub app_model: Option<AppModel>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExportDataResult {
    pub daily_logs: Vec<DailyLogModel>,
    pub hours_logs: Vec<HoursLogModel>,
}
