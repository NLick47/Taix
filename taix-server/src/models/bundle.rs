use serde::{Deserialize, Serialize};

use crate::models::category::CategoryModel;
use crate::models::log::{AppSessionModel, ColumnDataModel, DailyLogModel, InfrastructureDataModel};
use crate::models::web::{WebSiteCategoryModel, WebSiteModel};

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CategoryColumn {
    pub category_id: i64,
    pub name: String,
    pub color: Option<String>,
    pub icon_file: Option<String>,
    pub values: Vec<f64>,
}

#[derive(Debug, Clone, Default, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WebTotals {
    pub duration_total: i64,
    pub sites_total: i64,
    pub pages_total: i64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct OverviewBundle {
    pub apps: Vec<DailyLogModel>,
    pub sites: Vec<WebSiteModel>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub app_category_hours: Vec<ColumnDataModel>,
    #[serde(skip_serializing_if = "Vec::is_empty")]
    pub web_category_hours: Vec<ColumnDataModel>,
}

/// 图表页应用侧一次拉齐的 bundle；环比对比卡片需要应用与网站两侧的上期总量，
/// 因此 `web_prev` 一并内嵌，避免客户端再发一轮请求
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ChartBundle {
    pub columns: Vec<CategoryColumn>,
    pub top: Vec<DailyLogModel>,
    pub app_count: i64,
    pub app_count_prev: i64,
    pub total_seconds_prev: f64,
    pub web_prev: WebTotals,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WebChartBundle {
    /// 完整站点分类表（含无数据分类），客户端渲染列/饼图颜色直接取用，免单独请求
    pub categories: Vec<WebSiteCategoryModel>,
    pub category_statistics: Vec<InfrastructureDataModel>,
    pub category_columns: Vec<ColumnDataModel>,
    pub top_sites: Vec<WebSiteModel>,
    pub duration_total: i64,
    pub sites_total: i64,
    pub pages_total: i64,
}

#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DayDetailBundle {
    pub apps: Vec<DailyLogModel>,
    pub sessions: Vec<AppSessionModel>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct CategoryWithCount {
    #[serde(flatten)]
    pub category: CategoryModel,
    pub count: i64,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct WebCategoryWithCount {
    #[serde(flatten)]
    pub category: WebSiteCategoryModel,
    pub count: i64,
}

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BatchCategoryRequest {
    pub app_ids: Vec<i64>,
    pub category_id: i64,
}
