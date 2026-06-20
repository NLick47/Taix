use axum::{
    extract::{Extension, Path, Query, State},
    routing::get,
    Json, Router,
};
use chrono::NaiveDateTime;
use serde::Deserialize;
use sqlx::SqlitePool;
use std::sync::Arc;

use crate::models::category_summary::{CategoryMemberModel, CategorySummaryModel};
use crate::response::ApiResponse;
use crate::services::category_summary::CategorySummaryService;
use crate::services::config::ConfigService;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SummaryQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    /// 上一等长周期起点，用于环比。可选，缺省则不计算环比。
    prev_start: Option<NaiveDateTime>,
    /// 上一等长周期终点。
    prev_end: Option<NaiveDateTime>,
    timezone: String,
}

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route(
            "/api/category/app/:id/summary",
            get(get_app_category_summary),
        )
        .route(
            "/api/category/web/:id/summary",
            get(get_web_category_summary),
        )
        .route(
            "/api/category/app/:id/members",
            get(get_app_category_members),
        )
        .route(
            "/api/category/web/:id/members",
            get(get_web_category_members),
        )
}

async fn get_app_category_summary(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Path(id): Path<i64>,
    Query(q): Query<SummaryQuery>,
) -> Json<ApiResponse<CategorySummaryModel>> {
    match CategorySummaryService::get_app_category_summary(
        &pool,
        id,
        q.start.date(),
        q.end.date(),
        q.prev_start.map(|d| d.date()),
        q.prev_end.map(|d| d.date()),
        &q.timezone,
        &config_service,
    )
    .await
    {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_web_category_summary(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Path(id): Path<i64>,
    Query(q): Query<SummaryQuery>,
) -> Json<ApiResponse<CategorySummaryModel>> {
    match CategorySummaryService::get_web_category_summary(
        &pool,
        id,
        q.start.date(),
        q.end.date(),
        q.prev_start.map(|d| d.date()),
        q.prev_end.map(|d| d.date()),
        &q.timezone,
        &config_service,
    )
    .await
    {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_app_category_members(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Path(id): Path<i64>,
    Query(q): Query<SummaryQuery>,
) -> Json<ApiResponse<Vec<CategoryMemberModel>>> {
    match CategorySummaryService::get_app_category_members(
        &pool,
        id,
        q.start,
        q.end,
        &q.timezone,
        &config_service,
    )
    .await
    {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}

async fn get_web_category_members(
    State(pool): State<SqlitePool>,
    Extension(_config_service): Extension<Arc<ConfigService>>,
    Path(id): Path<i64>,
    Query(q): Query<SummaryQuery>,
) -> Json<ApiResponse<Vec<CategoryMemberModel>>> {
    match CategorySummaryService::get_web_category_members(
        &pool,
        id,
        q.start,
        q.end,
        &q.timezone,
    )
    .await
    {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse {
            code: 500,
            message: e.to_string(),
            data: None,
        }),
    }
}
