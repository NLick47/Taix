use axum::{
    extract::{Extension, Path, Query, State},
    routing::{delete, get, post, put},
    Json, Router,
};
use chrono::NaiveDateTime;
use serde::Deserialize;
use sqlx::SqlitePool;
use std::sync::Arc;

use crate::models::log::ColumnDataModel;
use crate::models::request::{AddUrlBrowseTimeRequest, ApplyMatchRequest, UpdateSitesCategoryRequest};
use crate::models::log::InfrastructureDataModel;
use crate::models::web::{WebBrowseLogModel, WebSiteCategoryModel, WebSiteModel};
use crate::response::ApiResponse;
use crate::services::config::ConfigService;
use crate::services::web_data::WebDataService;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DateRangeQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct SiteDateRangeQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    #[serde(default = "default_neg")]
    take: i64,
    #[serde(default = "default_neg")]
    skip: i64,
    #[serde(default, deserialize_with = "crate::models::request::deserialize_bool_insensitive")]
    is_time: bool,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct CategoryQuery {
    #[serde(default)]
    category_id: Option<i64>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ClearWebQuery {
    start: Option<NaiveDateTime>,
    end: Option<NaiveDateTime>,
    site_id: Option<i64>,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct BrowseLogQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    #[serde(default)]
    site_id: i64,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DomainQuery {
    domain: String,
}

fn default_neg() -> i64 { -1 }

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/webdata/browse-time", post(add_url_browse_time))
        .route("/api/webdata/sites", get(get_web_sites))
        .route("/api/webdata/sites-count", get(get_web_sites_count))
        .route("/api/webdata/sites/:id", get(get_web_site).put(update_web_site))
        .route("/api/webdata/site-by-domain", get(get_web_site_by_domain))
        .route("/api/webdata/categories", get(get_web_site_categories).post(create_web_site_category))
        .route("/api/webdata/categories/:id", put(update_web_site_category).delete(delete_web_site_category))
        .route("/api/webdata/update-sites-category", post(update_web_sites_category))
        .route("/api/webdata/unset-category-sites", get(get_unset_category_web_sites))
        .route("/api/webdata/clear", delete(clear_web_data))
        .route("/api/webdata/range", get(get_date_range_web_site_list))
        .route("/api/webdata/categories-statistics", get(get_categories_statistics))
        .route("/api/webdata/browse-statistics", get(get_browse_data_statistics))
        .route("/api/webdata/browse-category-statistics", get(get_browse_data_by_category_statistics))
        .route("/api/webdata/browse-duration-total", get(get_browse_duration_total))
        .route("/api/webdata/browse-sites-total", get(get_browse_sites_total))
        .route("/api/webdata/browse-pages-total", get(get_browse_pages_total))
        .route("/api/webdata/browse-log-list", get(get_browse_log_list))
        .route("/api/webdata/site-log-list", get(get_web_site_log_list))
        .route("/api/webdata/export", get(get_web_export_data))
        .route("/api/webdata/apply-url-match", post(apply_url_match))
}

async fn add_url_browse_time(
    State(pool): State<SqlitePool>,
    Json(req): Json<AddUrlBrowseTimeRequest>,
) -> Json<ApiResponse<()>> {
    match WebDataService::add_url_browse_time(&pool, req, None).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_web_sites(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<CategoryQuery>) -> Json<ApiResponse<Vec<WebSiteModel>>> {
    match WebDataService::get_web_sites(&pool, q.category_id, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

#[derive(Debug, Deserialize)]
struct SitesCountQuery {
    #[serde(rename = "categoryId")]
    category_id: i64,
}

async fn get_web_sites_count(State(pool): State<SqlitePool>, Query(q): Query<SitesCountQuery>) -> Json<ApiResponse<i64>> {
    match WebDataService::get_web_sites_count(&pool, q.category_id).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_web_site(State(pool): State<SqlitePool>, Path(id): Path<i64>) -> Json<ApiResponse<Option<WebSiteModel>>> {
    match WebDataService::get_web_site(&pool, id).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_web_site_by_domain(State(pool): State<SqlitePool>, Query(q): Query<DomainQuery>) -> Json<ApiResponse<Option<WebSiteModel>>> {
    match WebDataService::get_web_site_by_domain(&pool, &q.domain).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn update_web_site(State(pool): State<SqlitePool>, Path(id): Path<i64>, Json(req): Json<WebSiteModel>) -> Json<ApiResponse<Option<WebSiteModel>>> {
    match WebDataService::update_web_site(&pool, id, req).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_web_site_categories(State(pool): State<SqlitePool>) -> Json<ApiResponse<Vec<WebSiteCategoryModel>>> {
    match WebDataService::get_web_site_categories(&pool).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn create_web_site_category(State(pool): State<SqlitePool>, Json(req): Json<WebSiteCategoryModel>) -> Json<ApiResponse<WebSiteCategoryModel>> {
    match WebDataService::create_web_site_category(&pool, req).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn update_web_site_category(State(pool): State<SqlitePool>, Path(id): Path<i64>, Json(req): Json<WebSiteCategoryModel>) -> Json<ApiResponse<()>> {
    match WebDataService::update_web_site_category(&pool, id, req).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(crate::error::AppError::Business(msg)) => Json(ApiResponse {
            code: 400,
            message: msg,
            data: None,
        }),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn delete_web_site_category(
    State(pool): State<SqlitePool>,
    Path(id): Path<i64>,
) -> Json<ApiResponse<()>> {
    match WebDataService::delete_web_site_category(&pool, id).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(crate::error::AppError::Business(msg)) => Json(ApiResponse {
            code: 400,
            message: msg,
            data: None,
        }),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn update_web_sites_category(State(pool): State<SqlitePool>, Json(req): Json<UpdateSitesCategoryRequest>) -> Json<ApiResponse<()>> {
    match WebDataService::update_web_sites_category(&pool, req).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_unset_category_web_sites(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>) -> Json<ApiResponse<Vec<WebSiteModel>>> {
    match WebDataService::get_unset_category_web_sites(&pool, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn clear_web_data(State(pool): State<SqlitePool>, Query(q): Query<ClearWebQuery>) -> Json<ApiResponse<()>> {
    match WebDataService::clear_web_data(&pool, q.start.map(|d| d.date()), q.end.map(|d| d.date()), q.site_id, &q.timezone).await {
        Ok(_) => Json(ApiResponse::ok_empty()),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_date_range_web_site_list(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<SiteDateRangeQuery>) -> Json<ApiResponse<Vec<WebSiteModel>>> {
    match WebDataService::get_date_range_web_site_list(&pool, q.start, q.end, q.take, q.skip, q.is_time, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_categories_statistics(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<Vec<InfrastructureDataModel>>> {
    match WebDataService::get_categories_statistics(&pool, q.start.date(), q.end.date(), &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_browse_data_statistics(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<BrowseLogQuery>) -> Json<ApiResponse<Vec<InfrastructureDataModel>>> {
    match WebDataService::get_browse_data_statistics(&pool, q.start, q.end, q.site_id, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_browse_data_by_category_statistics(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<Vec<ColumnDataModel>>> {
    match WebDataService::get_browse_data_by_category_statistics(&pool, q.start.date(), q.end.date(), &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_browse_duration_total(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<i64>> {
    match WebDataService::get_browse_duration_total(&pool, q.start, q.end, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_browse_sites_total(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<i64>> {
    match WebDataService::get_browse_sites_total(&pool, q.start, q.end, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_browse_pages_total(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<i64>> {
    match WebDataService::get_browse_pages_total(&pool, q.start, q.end, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_browse_log_list(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<BrowseLogQuery>) -> Json<ApiResponse<Vec<WebBrowseLogModel>>> {
    match WebDataService::get_browse_log_list(&pool, q.start, q.end, q.site_id, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_web_site_log_list(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<Vec<WebSiteModel>>> {
    match WebDataService::get_web_site_log_list(&pool, q.start, q.end, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn get_web_export_data(State(pool): State<SqlitePool>, Extension(config_service): Extension<Arc<ConfigService>>, Query(q): Query<DateRangeQuery>) -> Json<ApiResponse<crate::models::web::WebExportDataResult>> {
    match WebDataService::get_web_export_data(&pool, q.start, q.end, &q.timezone, &config_service).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

async fn apply_url_match(
    State(pool): State<SqlitePool>,
    Json(req): Json<ApplyMatchRequest>,
) -> Json<ApiResponse<usize>> {
    match WebDataService::apply_url_match(&pool, req.patterns).await {
        Ok(count) => Json(ApiResponse::ok(count)),
        Err(e) => Json(ApiResponse { code: 500, message: e.to_string(), data: None }),
    }
}

