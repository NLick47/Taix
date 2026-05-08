use axum::{
    extract::{Path, Query, State},
    routing::{delete, get},
    Json, Router,
};
use chrono::NaiveDateTime;
use serde::Deserialize;
use sqlx::SqlitePool;

use crate::models::log::{ColumnDataModel, DailyLogModel, ExportDataResult, HoursLogModel};
use crate::response::ApiResponse;
use crate::services::data::DataService;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct RangeQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    #[serde(default = "default_neg")]
    take: i64,
    #[serde(default = "default_neg")]
    skip: i64,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ProcessMonthQuery {
    app_id: i64,
    month: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ClearQuery {
    month: Option<NaiveDateTime>,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ClearRangeQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct TimeRangeQuery {
    time: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DateQuery {
    date: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct YearQuery {
    year: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct AppDateQuery {
    app_id: i64,
    date: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct AppRangeQuery {
    app_id: i64,
    start: NaiveDateTime,
    end: NaiveDateTime,
    timezone: String,
}

fn default_neg() -> i64 { -1 }

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/data/range", get(get_date_range_log_list))
        .route("/api/data/process-month", get(get_process_month_log_list))
        .route("/api/data/clear/:app_id", delete(clear_app_data))
        .route("/api/data/clear-range", delete(clear_range))
        .route("/api/data/time-range", get(get_time_range_log_list))
        .route("/api/data/range-total", get(get_range_total_data))
        .route("/api/data/month-total", get(get_month_total_data))
        .route("/api/data/category-hours", get(get_category_hours_data))
        .route("/api/data/category-range", get(get_category_range_data))
        .route("/api/data/category-year", get(get_category_year_data))
        .route("/api/data/app-day", get(get_app_day_data))
        .route("/api/data/app-range", get(get_app_range_data))
        .route("/api/data/app-year", get(get_app_year_data))
        .route("/api/data/range-app-count", get(get_date_range_app_count))
        .route("/api/data/export", get(get_export_data))
}

async fn get_date_range_log_list(State(pool): State<SqlitePool>, Query(q): Query<RangeQuery>) -> Json<ApiResponse<Vec<DailyLogModel>>> {
    match DataService::get_date_range_log_list(&pool, q.start.date(), q.end.date(), q.take, q.skip, &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_process_month_log_list(State(pool): State<SqlitePool>, Query(q): Query<ProcessMonthQuery>) -> Json<ApiResponse<Vec<DailyLogModel>>> {
    match DataService::get_process_month_log_list(&pool, q.app_id, q.month.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn clear_app_data(State(pool): State<SqlitePool>, Path(app_id): Path<i64>, Query(q): Query<ClearQuery>) -> Json<ApiResponse<()>> {
    match DataService::clear_app_data(&pool, app_id, q.month.map(|d| d.date()), &q.timezone).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn clear_range(State(pool): State<SqlitePool>, Query(q): Query<ClearRangeQuery>) -> Json<ApiResponse<()>> {
    match DataService::clear_range(&pool, q.start.date(), q.end.date(), &q.timezone).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_time_range_log_list(State(pool): State<SqlitePool>, Query(q): Query<TimeRangeQuery>) -> Json<ApiResponse<Vec<HoursLogModel>>> {
    match DataService::get_time_range_log_list(&pool, q.time, &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_range_total_data(State(pool): State<SqlitePool>, Query(q): Query<RangeQuery>) -> Json<ApiResponse<Vec<f64>>> {
    match DataService::get_range_total_data(&pool, q.start.date(), q.end.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_month_total_data(State(pool): State<SqlitePool>, Query(q): Query<YearQuery>) -> Json<ApiResponse<Vec<f64>>> {
    match DataService::get_month_total_data(&pool, q.year.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_category_hours_data(State(pool): State<SqlitePool>, Query(q): Query<DateQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match DataService::get_category_hours_data(&pool, q.date.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_category_range_data(State(pool): State<SqlitePool>, Query(q): Query<RangeQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match DataService::get_category_range_data(&pool, q.start.date(), q.end.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_category_year_data(State(pool): State<SqlitePool>, Query(q): Query<DateQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match DataService::get_category_year_data(&pool, q.date.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_app_day_data(State(pool): State<SqlitePool>, Query(q): Query<AppDateQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match DataService::get_app_day_data(&pool, q.app_id, q.date.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_app_range_data(State(pool): State<SqlitePool>, Query(q): Query<AppRangeQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match DataService::get_app_range_data(&pool, q.app_id, q.start.date(), q.end.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_app_year_data(State(pool): State<SqlitePool>, Query(q): Query<AppDateQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match DataService::get_app_year_data(&pool, q.app_id, q.date.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_date_range_app_count(State(pool): State<SqlitePool>, Query(q): Query<RangeQuery>) -> Json<ApiResponse<i64>> {
    match DataService::get_date_range_app_count(&pool, q.start.date(), q.end.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_export_data(State(pool): State<SqlitePool>, Query(q): Query<RangeQuery>) -> Json<ApiResponse<ExportDataResult>> {
    match DataService::get_export_data(&pool, q.start.date(), q.end.date(), &q.timezone).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

