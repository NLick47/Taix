use serde::{Deserialize, Serialize};

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CategorySummaryModel {
    pub category_id: i64,
    pub category_name: String,
    pub total_seconds: i64,
    /// 上一等长周期（昨日/上周/上月/去年）的总时长，用于环比；无对比时为 0
    pub previous_total_seconds: i64,
    pub active_days: i64,
    pub average_daily_seconds: i64,
    pub daily_trend: Vec<DailyPointModel>,
    /// 按本地时区小时的时长分布，固定 24 个元素，索引 0..23
    pub hourly_distribution: Vec<i64>,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DailyPointModel {
    pub date: chrono::NaiveDate,
    pub seconds: i64,
}

#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CategoryMemberModel {
    // id == -1 表示 "其他" 聚合行
    pub id: i64,
    pub name: String,
    pub icon_file: Option<String>,
    pub seconds: i64,
}
