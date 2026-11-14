use std::sync::Arc;

use axum::{
    extract::{Extension, Query, State},
    routing::{get, put},
    Json, Router,
};
use chrono::NaiveDateTime;
use serde::Deserialize;
use sqlx::SqlitePool;

use crate::error::AppError;
use crate::models::app::AppModel;
use crate::models::bundle::{
    BatchCategoryRequest, CategoryWithCount, ChartBundle, DayDetailBundle, OverviewBundle,
    WebCategoryWithCount, WebChartBundle,
};
use crate::response::ApiResponse;
use crate::services::app_data::AppDataService;
use crate::services::category::CategoryService;
use crate::services::config::ConfigService;
use crate::services::data::DataService;
use crate::services::web_data::WebDataService;

pub fn router() -> Router<SqlitePool> {
    Router::new()
        .route("/api/data/overview", get(data_overview))
        .route("/api/data/chart-bundle", get(data_chart_bundle))
        .route("/api/data/day-detail", get(data_day_detail))
        .route("/api/webdata/chart-bundle", get(web_chart_bundle))
        .route("/api/webdata/categories-with-counts", get(web_categories_with_counts))
        .route("/api/category/with-counts", get(categories_with_counts))
        .route("/api/appdata/batch-category", put(batch_category))
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct OverviewQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    #[serde(default)]
    take: Option<i64>,
    #[serde(default)]
    with_columns: bool,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct ChartBundleQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    prev_start: Option<NaiveDateTime>,
    prev_end: Option<NaiveDateTime>,
    bucket: Option<String>,
    #[serde(default)]
    take: Option<i64>,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct WebChartBundleQuery {
    start: NaiveDateTime,
    end: NaiveDateTime,
    #[serde(default)]
    take: Option<i64>,
    timezone: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DayDetailQuery {
    date: NaiveDateTime,
    timezone: String,
}

async fn data_overview(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Query(q): Query<OverviewQuery>,
) -> Json<ApiResponse<OverviewBundle>> {
    let tz_id = &q.timezone;
    let start_d = q.start.date();
    let end_d = q.end.date();
    let take = q.take.unwrap_or(0);

    let apps = match DataService::get_date_range_log_list(&pool, start_d, end_d, take, 0, tz_id, &config_service).await {
        Ok(data) => data,
        Err(e) => return err_json(e),
    };
    let sites = match WebDataService::get_date_range_web_site_list(
        &pool,
        start_d.and_hms_opt(0, 0, 0).unwrap(),
        end_d.and_hms_opt(0, 0, 0).unwrap(),
        take,
        0,
        false,
        tz_id,
        &config_service,
    )
    .await
    {
        Ok(data) => data,
        Err(e) => return err_json(e),
    };

    let mut bundle = OverviewBundle {
        apps,
        sites,
        app_category_hours: Vec::new(),
        web_category_hours: Vec::new(),
    };

    if q.with_columns && start_d == end_d {
        if let Ok(cols) = DataService::get_category_hours_data(&pool, start_d, tz_id, &config_service).await {
            bundle.app_category_hours = cols;
        }
        if let Ok(cols) =
            WebDataService::get_browse_data_by_category_statistics(&pool, start_d, end_d, tz_id, &config_service).await
        {
            bundle.web_category_hours = cols;
        }
    }

    Json(ApiResponse::ok(bundle))
}

async fn data_chart_bundle(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Query(q): Query<ChartBundleQuery>,
) -> Json<ApiResponse<ChartBundle>> {
    let tz_id = &q.timezone;
    let start_d = q.start.date();
    let end_d = q.end.date();

    let columns = match q.bucket.as_deref() {
        Some("day") => {
            DataService::get_category_range_data(&pool, start_d, end_d, tz_id, &config_service).await
        }
        Some("month") => {
            DataService::get_category_year_data(&pool, start_d, tz_id, &config_service).await
        }
        _ => DataService::get_category_hours_data(&pool, start_d, tz_id, &config_service).await,
    };
    let columns = match columns {
        Ok(cols) => cols,
        Err(e) => return err_json(e),
    };
    let columns = match CategoryService::enrich_columns(&pool, columns).await {
        Ok(cols) => cols,
        Err(e) => return err_json(e),
    };

    let top = match DataService::get_date_range_log_list(&pool, start_d, end_d, q.take.unwrap_or(5), 0, tz_id, &config_service).await {
        Ok(data) => data,
        Err(e) => return err_json(e),
    };
    let app_count = match DataService::get_date_range_app_count(&pool, start_d, end_d, tz_id, &config_service).await {
        Ok(v) => v,
        Err(e) => return err_json(e),
    };

    let mut app_count_prev = 0;
    let mut total_seconds_prev = 0.0;
    let mut web_prev = crate::models::bundle::WebTotals::default();
    if let (Some(ps), Some(pe)) = (q.prev_start, q.prev_end) {
        let ps_d = ps.date();
        let pe_d = pe.date();
        app_count_prev = match DataService::get_date_range_app_count(&pool, ps_d, pe_d, tz_id, &config_service).await {
            Ok(v) => v,
            Err(e) => return err_json(e),
        };
        total_seconds_prev = match DataService::get_range_total_data(&pool, ps_d, pe_d, tz_id, &config_service).await {
            Ok(rows) => rows.iter().sum(),
            Err(e) => return err_json(e),
        };
        web_prev.duration_total = match WebDataService::get_browse_duration_total(&pool, ps, pe, tz_id, &config_service).await {
            Ok(v) => v,
            Err(e) => return err_json(e),
        };
        web_prev.sites_total = match WebDataService::get_browse_sites_total(&pool, ps, pe, tz_id, &config_service).await {
            Ok(v) => v,
            Err(e) => return err_json(e),
        };
        web_prev.pages_total = match WebDataService::get_browse_pages_total(&pool, ps, pe, tz_id, &config_service).await {
            Ok(v) => v,
            Err(e) => return err_json(e),
        };
    }

    Json(ApiResponse::ok(ChartBundle {
        columns,
        top,
        app_count,
        app_count_prev,
        total_seconds_prev,
        web_prev,
    }))
}

async fn web_chart_bundle(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Query(q): Query<WebChartBundleQuery>,
) -> Json<ApiResponse<WebChartBundle>> {
    let tz_id = &q.timezone;
    let start_d = q.start.date();
    let end_d = q.end.date();

    let categories = match WebDataService::get_web_site_categories(&pool).await {
        Ok(c) => (*c).clone(),
        Err(e) => return err_json(e),
    };
    let category_statistics =
        match WebDataService::get_categories_statistics(&pool, start_d, end_d, tz_id, &config_service).await {
            Ok(data) => data,
            Err(e) => return err_json(e),
        };
    let category_columns =
        match WebDataService::get_browse_data_by_category_statistics(&pool, start_d, end_d, tz_id, &config_service).await
        {
            Ok(data) => data,
            Err(e) => return err_json(e),
        };
    let top_sites = match WebDataService::get_date_range_web_site_list(
        &pool,
        start_d.and_hms_opt(0, 0, 0).unwrap(),
        end_d.and_hms_opt(0, 0, 0).unwrap(),
        q.take.unwrap_or(10),
        0,
        false,
        tz_id,
        &config_service,
    )
    .await
    {
        Ok(data) => data,
        Err(e) => return err_json(e),
    };
    let duration_total = match WebDataService::get_browse_duration_total(&pool, q.start, q.end, tz_id, &config_service).await {
        Ok(v) => v,
        Err(e) => return err_json(e),
    };
    let sites_total = match WebDataService::get_browse_sites_total(&pool, q.start, q.end, tz_id, &config_service).await {
        Ok(v) => v,
        Err(e) => return err_json(e),
    };
    let pages_total = match WebDataService::get_browse_pages_total(&pool, q.start, q.end, tz_id, &config_service).await {
        Ok(v) => v,
        Err(e) => return err_json(e),
    };

    Json(ApiResponse::ok(WebChartBundle {
        categories,
        category_statistics,
        category_columns,
        top_sites,
        duration_total,
        sites_total,
        pages_total,
    }))
}

async fn data_day_detail(
    State(pool): State<SqlitePool>,
    Extension(config_service): Extension<Arc<ConfigService>>,
    Query(q): Query<DayDetailQuery>,
) -> Json<ApiResponse<DayDetailBundle>> {
    let tz_id = &q.timezone;
    let d = q.date.date();

    let apps = match DataService::get_date_range_log_list(&pool, d, d, 0, 0, tz_id, &config_service).await {
        Ok(data) => data,
        Err(e) => return err_json(e),
    };
    let sessions = match DataService::get_app_sessions(
        &pool,
        d.and_hms_opt(0, 0, 0).unwrap(),
        d.and_hms_opt(23, 59, 59).unwrap(),
        tz_id,
        &config_service,
    )
    .await
    {
        Ok(data) => data,
        Err(e) => return err_json(e),
    };

    Json(ApiResponse::ok(DayDetailBundle { apps, sessions }))
}

async fn categories_with_counts(
    State(pool): State<SqlitePool>,
) -> Json<ApiResponse<Vec<CategoryWithCount>>> {
    match CategoryService::get_categories_with_counts(&pool).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => err_json(e),
    }
}

async fn web_categories_with_counts(
    State(pool): State<SqlitePool>,
) -> Json<ApiResponse<Vec<WebCategoryWithCount>>> {
    match WebDataService::get_web_site_categories_with_counts(&pool).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => err_json(e),
    }
}

async fn batch_category(
    State(pool): State<SqlitePool>,
    Json(req): Json<BatchCategoryRequest>,
) -> Json<ApiResponse<Vec<AppModel>>> {
    match AppDataService::batch_update_category(&pool, req).await {
        Ok(data) => Json(ApiResponse::ok(data)),
        Err(e) => err_json(e),
    }
}

fn err_json<T>(e: AppError) -> Json<ApiResponse<T>> {
    Json(ApiResponse { code: 500, message: e.to_string(), data: None })
}

#[cfg(test)]
mod tests {
    use super::*;
    use axum::body::Body;
    use axum::http::{header, Request, StatusCode};
    use chrono::Utc;
    use http_body_util::BodyExt;
    use tower::ServiceExt;

    async fn bundle_app() -> (axum::Router, SqlitePool) {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        crate::migrations::run(&pool, "UTC").await.unwrap();
        // 分类列表缓存是进程级全局的，而各测试用独立内存库，先清掉防止跨测试污染
        crate::services::category::invalidate_category_cache().await;
        crate::services::web_data::invalidate_web_category_cache().await;

        let now = Utc::now();
        let today = now.date_naive();

        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('工作', NULL, '#4a90d9', 0, '[]', 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO AppModels (Name, Alias, Description, File, CategoryID, IconFile, TotalTime) VALUES ('Code.exe', 'Code', NULL, '/usr/bin/code', (SELECT ID FROM CategoryModels WHERE Name = '工作'), NULL, 1200)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO HoursLogModels (DataTime, AppModelID, Time) VALUES (?1, (SELECT ID FROM AppModels LIMIT 1), 1200)")
            .bind(now)
            .execute(&pool).await.unwrap();

        sqlx::query("INSERT INTO WebSiteCategoryModels (Name, IconFile, Color, IsUrlMatch, UrlPatterns, IsSystem) VALUES ('资讯', NULL, '#2a9d8f', 0, NULL, 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO WebSiteModels (Title, Domain, Alias, CategoryID, IconFile, Duration) VALUES ('Example', 'example.com', NULL, (SELECT ID FROM WebSiteCategoryModels WHERE Name = '资讯'), NULL, 300)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO WebUrlModels (Title, Url, IconFile) VALUES ('Home', 'https://example.com/', NULL)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO WebBrowseLogModels (UrlId, LogTime, Duration, SiteId) VALUES ((SELECT ID FROM WebUrlModels LIMIT 1), ?1, 300, (SELECT ID FROM WebSiteModels LIMIT 1))")
            .bind(now)
            .execute(&pool).await.unwrap();

        sqlx::query("INSERT INTO AppSessions (AppModelID, StartTime, EndTime, Duration) VALUES ((SELECT ID FROM AppModels LIMIT 1), ?1, ?2, 600)")
            .bind(now - chrono::Duration::seconds(600))
            .bind(now)
            .execute(&pool).await.unwrap();

        let root = std::env::temp_dir().join(format!("taix-bundles-{}", std::process::id()));
        std::fs::create_dir_all(&root).unwrap();
        let config_service = Arc::new(ConfigService::new(&root));

        let app = router()
            .merge(crate::routes::category_summary::router())
            .with_state(pool.clone())
            .layer(axum::extract::Extension(config_service));
        (app, pool)
    }

    async fn get(app: &axum::Router, uri: &str) -> serde_json::Value {
        let resp = app
            .clone()
            .oneshot(Request::builder().uri(uri).body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK, "GET {uri} failed");
        let body = resp.into_body().collect().await.unwrap().to_bytes().to_vec();
        let value: serde_json::Value = serde_json::from_slice(&body).unwrap();
        assert_eq!(value["code"], 0, "GET {uri} -> {value}");
        value
    }

    async fn put_json(app: &axum::Router, uri: &str, body: serde_json::Value) -> serde_json::Value {
        let resp = app
            .clone()
            .oneshot(
                Request::builder()
                    .method("PUT")
                    .uri(uri)
                    .header(header::CONTENT_TYPE, "application/json")
                    .body(Body::from(body.to_string()))
                    .unwrap(),
            )
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK, "PUT {uri} failed");
        let body = resp.into_body().collect().await.unwrap().to_bytes().to_vec();
        let value: serde_json::Value = serde_json::from_slice(&body).unwrap();
        assert_eq!(value["code"], 0, "PUT {uri} -> {value}");
        value
    }

    fn day_params() -> (String, String, String) {
        let today = Utc::now().date_naive();
        let prev = (today - chrono::Duration::days(1)).to_string();
        let today = today.to_string();
        (format!("{today}T00:00:00"), format!("{today}T00:00:00"), format!("{prev}T00:00:00"))
    }

    #[tokio::test]
    async fn overview_returns_apps_sites_and_hour_columns() {
        let (app, _pool) = bundle_app().await;
        let (start, end, _) = day_params();
        let v = get(&app, &format!(
            "/api/data/overview?start={start}&end={end}&take=5&withColumns=true&timezone=UTC"
        )).await;
        let data = &v["data"];
        assert_eq!(data["apps"].as_array().unwrap().len(), 1, "apps: {data}");
        assert_eq!(data["sites"].as_array().unwrap().len(), 1, "sites: {data}");
        assert_eq!(data["appCategoryHours"].as_array().unwrap().len(), 1, "columns: {data}");
        assert_eq!(data["webCategoryHours"].as_array().unwrap().len(), 1, "web columns: {data}");
        assert_eq!(data["apps"][0]["appModel"]["name"], "Code.exe");
    }

    #[tokio::test]
    async fn chart_bundle_returns_columns_top_counts_and_web_prev() {
        let (app, pool) = bundle_app().await;
        let cat_id: i64 =
            sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE Name = '工作'").fetch_one(&pool).await.unwrap();
        let (start, end, prev) = day_params();
        let v = get(&app, &format!(
            "/api/data/chart-bundle?start={start}&end={end}&prevStart={prev}&prevEnd={prev}&bucket=hour&take=5&timezone=UTC"
        )).await;
        let data = &v["data"];
        let columns = data["columns"].as_array().unwrap();
        assert_eq!(columns.len(), 1);
        assert_eq!(columns[0]["name"], "工作");
        assert_eq!(columns[0]["color"], "#4a90d9");
        assert_eq!(columns[0]["categoryId"], cat_id);
        assert_eq!(data["top"].as_array().unwrap().len(), 1);
        assert_eq!(data["appCount"], 1);
        assert_eq!(data["webPrev"]["durationTotal"], 0, "上期无数据应为 0");
    }

    #[tokio::test]
    async fn web_chart_bundle_returns_all_parts() {
        let (app, _pool) = bundle_app().await;
        let (start, end, _) = day_params();
        let v = get(&app, &format!(
            "/api/webdata/chart-bundle?start={start}&end={end}&take=10&timezone=UTC"
        )).await;
        let data = &v["data"];
        assert!(data["categories"].as_array().unwrap().iter().any(|c| c["name"] == "资讯"), "分类表应内嵌: {data}");
        assert_eq!(data["categoryStatistics"].as_array().unwrap().len(), 1);
        assert_eq!(data["categoryStatistics"][0]["name"], "资讯");
        assert_eq!(data["categoryColumns"].as_array().unwrap().len(), 1);
        assert_eq!(data["topSites"].as_array().unwrap().len(), 1);
        assert_eq!(data["topSites"][0]["domain"], "example.com");
        assert_eq!(data["durationTotal"], 300);
        assert_eq!(data["sitesTotal"], 1);
        assert_eq!(data["pagesTotal"], 1);
    }

    #[tokio::test]
    async fn day_detail_returns_apps_and_sessions() {
        let (app, _pool) = bundle_app().await;
        let (start, _, _) = day_params();
        let v = get(&app, &format!("/api/data/day-detail?date={start}&timezone=UTC")).await;
        let data = &v["data"];
        assert_eq!(data["apps"].as_array().unwrap().len(), 1);
        assert_eq!(data["sessions"].as_array().unwrap().len(), 1);
        assert_eq!(data["sessions"][0]["duration"], 600);
    }

    #[tokio::test]
    async fn with_counts_returns_categories_and_counts() {
        let (app, _pool) = bundle_app().await;
        let v = get(&app, "/api/category/with-counts").await;
        let items = v["data"].as_array().unwrap();
        assert!(items.len() >= 2, "系统分类 + 工作分类: {v}");
        let work = items.iter().find(|c| c["name"] == "工作").unwrap();
        assert_eq!(work["count"], 1);
        assert_eq!(work["isSystem"], false);

        let v = get(&app, "/api/webdata/categories-with-counts").await;
        let items = v["data"].as_array().unwrap();
        let news = items.iter().find(|c| c["name"] == "资讯").expect("资讯分类应在列表里");
        assert_eq!(news["count"], 1);
        assert_eq!(news["isSystem"], false);
    }

    #[tokio::test]
    async fn batch_category_updates_and_returns_apps() {
        let (app, _pool) = bundle_app().await;
        let v = put_json(&app, "/api/appdata/batch-category",
            serde_json::json!({ "appIds": [1], "categoryId": 0 })).await;
        let apps = v["data"].as_array().unwrap();
        assert_eq!(apps.len(), 1, "返回更新后的应用: {v}");
        assert_eq!(apps[0]["categoryId"], 1, "0 应回退到系统分类");
        assert_eq!(apps[0]["category"]["isSystem"], true, "应内嵌分类");
    }

    #[tokio::test]
    async fn summary_with_members_embeds_member_models() {
        let (app, pool) = bundle_app().await;
        let cat_id: i64 =
            sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE Name = '工作'").fetch_one(&pool).await.unwrap();
        let (start, end, prev) = day_params();
        let v = get(&app, &format!(
            "/api/category/app/{cat_id}/summary?start={start}&end={end}&prevStart={prev}&prevEnd={prev}&withMembers=true&withModels=true&timezone=UTC"
        )).await;
        let data = &v["data"];
        assert!(data["totalSeconds"].as_i64().unwrap() > 0, "{data}");
        let members = data["members"].as_array().expect("withMembers 应内嵌成员").to_vec();
        assert_eq!(members.len(), 1);
        assert_eq!(members[0]["name"], "Code");
        assert_eq!(members[0]["app"]["name"], "Code.exe", "withModels 应内嵌完整应用");
        assert!(data.get("hourlyDistribution").is_some());
    }

    #[tokio::test]
    async fn summary_without_members_keeps_legacy_shape() {
        let (app, pool) = bundle_app().await;
        let cat_id: i64 =
            sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE Name = '工作'").fetch_one(&pool).await.unwrap();
        let (start, end, _) = day_params();
        let v = get(&app, &format!(
            "/api/category/app/{cat_id}/summary?start={start}&end={end}&timezone=UTC"
        )).await;
        let data = &v["data"];
        assert!(data.get("members").is_none(), "未请求时 members 不得出现在响应里: {data}");
    }
}

#[cfg(test)]
mod differential_tests {
    use super::*;
    use crate::utils::{parse_timezone, tz_date_to_utc_range};
    use axum::body::Body;
    use axum::http::{header, Request, StatusCode};
    use chrono::Utc;
    use http_body_util::BodyExt;
    use tower::ServiceExt;

    pub(super) async fn diff_app() -> (axum::Router, SqlitePool) {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        crate::migrations::run(&pool, "Asia/Shanghai").await.unwrap();
        // 分类列表缓存是进程级全局的，而各测试用独立内存库，先清掉防止跨测试污染
        crate::services::category::invalidate_category_cache().await;
        crate::services::web_data::invalidate_web_category_cache().await;

        let tz = parse_timezone("Asia/Shanghai");
        let today_local = Utc::now().with_timezone(&tz).date_naive();
        let (day_start, _) = tz_date_to_utc_range(today_local, &tz);
        let yesterday_start = day_start - chrono::Duration::days(1);

        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('工作', 'work.png', '#4a90d9', 0, '[]', 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('透明', NULL, NULL, 0, '[]', 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('空分类', 'empty.png', '#000000', 0, '[]', 0)")
            .execute(&pool).await.unwrap();

        sqlx::query("INSERT INTO AppModels (Name, Alias, Description, File, CategoryID, IconFile, TotalTime) VALUES ('Code.exe', 'Code', NULL, '/bin/code', (SELECT ID FROM CategoryModels WHERE Name='工作'), 'code.png', 900)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO AppModels (Name, Alias, Description, File, CategoryID, IconFile, TotalTime) VALUES ('Ghost.exe', NULL, NULL, NULL, (SELECT ID FROM CategoryModels WHERE Name='透明'), NULL, 120)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO AppModels (Name, Alias, Description, File, CategoryID, IconFile, TotalTime) VALUES ('Old.exe', 'Old', NULL, NULL, (SELECT ID FROM CategoryModels WHERE Name='工作'), NULL, 45)")
            .execute(&pool).await.unwrap();

        for (app, dt, secs) in [
            ("Code.exe", day_start + chrono::Duration::hours(1), 600i64),
            ("Code.exe", day_start + chrono::Duration::hours(2), 300),
            ("Ghost.exe", day_start + chrono::Duration::hours(1), 120),
            ("Old.exe", yesterday_start + chrono::Duration::hours(1), 45),
        ] {
            sqlx::query("INSERT INTO HoursLogModels (DataTime, AppModelID, Time) VALUES (?1, (SELECT ID FROM AppModels WHERE Name = ?2), ?3)")
                .bind(dt).bind(app).bind(secs)
                .execute(&pool).await.unwrap();
        }

        sqlx::query("INSERT INTO AppSessions (AppModelID, StartTime, EndTime, Duration) VALUES ((SELECT ID FROM AppModels WHERE Name='Code.exe'), ?1, ?2, 1500)")
            .bind(day_start + chrono::Duration::minutes(30))
            .bind(day_start + chrono::Duration::minutes(30) + chrono::Duration::seconds(1500))
            .execute(&pool).await.unwrap();

        sqlx::query("INSERT INTO WebSiteCategoryModels (Name, IconFile, Color, IsUrlMatch, UrlPatterns, IsSystem) VALUES ('资讯', NULL, '#2a9d8f', 0, NULL, 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO WebSiteModels (Title, Domain, Alias, CategoryID, IconFile, Duration) VALUES ('Example', 'example.com', NULL, (SELECT ID FROM WebSiteCategoryModels WHERE Name='资讯'), NULL, 300)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO WebSiteModels (Title, Domain, Alias, CategoryID, IconFile, Duration) VALUES ('News', 'news.org', NULL, (SELECT ID FROM WebSiteCategoryModels WHERE Name='资讯'), NULL, 60)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO WebUrlModels (Title, Url, IconFile) VALUES ('Home', 'https://example.com/', NULL)")
            .execute(&pool).await.unwrap();
        for (dt, secs, site) in [
            (day_start + chrono::Duration::minutes(90), 300i64, "example.com"),
            (day_start + chrono::Duration::hours(3), 120, "news.org"),
            (yesterday_start + chrono::Duration::hours(2), 60, "example.com"),
        ] {
            sqlx::query("INSERT INTO WebBrowseLogModels (UrlId, LogTime, Duration, SiteId) VALUES ((SELECT ID FROM WebUrlModels LIMIT 1), ?1, ?2, (SELECT ID FROM WebSiteModels WHERE Domain = ?3))")
                .bind(dt).bind(secs).bind(site)
                .execute(&pool).await.unwrap();
        }

        let root = std::env::temp_dir().join(format!("taix-diff-{}", std::process::id()));
        std::fs::create_dir_all(&root).unwrap();
        let config_service = Arc::new(ConfigService::new(&root));

        let app = router()
            .merge(crate::routes::data::router())
            .merge(crate::routes::web_data::router())
            .merge(crate::routes::category::router())
            .merge(crate::routes::category_summary::router())
            .merge(crate::routes::app_data::router())
            .with_state(pool.clone())
            .layer(axum::extract::Extension(config_service));
        (app, pool)
    }

    pub(super) async fn get(app: &axum::Router, uri: &str) -> serde_json::Value {
        let resp = app
            .clone()
            .oneshot(Request::builder().uri(uri).body(Body::empty()).unwrap())
            .await
            .unwrap();
        assert_eq!(resp.status(), StatusCode::OK, "GET {uri}");
        let body = resp.into_body().collect().await.unwrap().to_bytes().to_vec();
        let value: serde_json::Value = serde_json::from_slice(&body).unwrap();
        assert_eq!(value["code"], 0, "GET {uri} -> {value}");
        value
    }

    pub(super) fn tz_params() -> (String, String, String, String) {
        let tz = parse_timezone("Asia/Shanghai");
        let today = Utc::now().with_timezone(&tz).date_naive();
        let yesterday = today - chrono::Duration::days(1);
        let tomorrow = today + chrono::Duration::days(1);
        (
            format!("{today}T00:00:00"),
            format!("{yesterday}T00:00:00"),
            format!("{tomorrow}T00:00:00"),
            "Asia/Shanghai".to_string(),
        )
    }

    #[tokio::test]
    async fn overview_apps_equal_old_range_endpoint() {
        let (app, _pool) = diff_app().await;
        let (start, _, _, tzq) = tz_params();
        let bundle = get(&app, &format!("/api/data/overview?start={start}&end={start}&take=0&timezone={tzq}")).await;
        let old = get(&app, &format!("/api/data/range?start={start}&end={start}&take=-1&skip=-1&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["apps"], old["data"], "overview.apps 必须与旧 /api/data/range 完全一致");
        assert_eq!(bundle["data"]["apps"].as_array().unwrap().len(), 2, "仅今日两只应用（昨天那只不得混入）");
    }

    #[tokio::test]
    async fn overview_sites_equal_old_web_range_endpoint() {
        let (app, _pool) = diff_app().await;
        let (start, _, _, tzq) = tz_params();
        let bundle = get(&app, &format!("/api/data/overview?start={start}&end={start}&take=0&timezone={tzq}")).await;
        let old = get(&app, &format!("/api/webdata/range?start={start}&end={start}&take=0&skip=-1&isTime=false&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["sites"], old["data"], "overview.sites 必须与旧 /api/webdata/range 完全一致");
        assert_eq!(bundle["data"]["sites"].as_array().unwrap().len(), 2);
    }

    #[tokio::test]
    async fn chart_columns_match_old_values_and_category_meta() {
        let (app, pool) = diff_app().await;
        let (start, _, _, tzq) = tz_params();
        let bundle = get(&app, &format!("/api/data/chart-bundle?start={start}&end={start}&bucket=hour&take=5&timezone={tzq}")).await;
        let old = get(&app, &format!("/api/data/category-hours?date={start}&timezone={tzq}")).await;

        let old_rows = old["data"].as_array().unwrap();
        let columns = bundle["data"]["columns"].as_array().unwrap();
        assert_eq!(columns.len(), old_rows.len(), "列数一致");
        for row in old_rows {
            let cid = row["categoryId"].as_i64().unwrap();
            let col = columns.iter().find(|c| c["categoryId"].as_i64() == Some(cid)).expect("每条旧列都有聚合列");
            assert_eq!(col["values"], row["values"], "categoryId={cid} 的按小时值必须一致");
        }

        for col in columns {
            let cid = col["categoryId"].as_i64().unwrap();
            let (name, color, icon): (String, Option<String>, Option<String>) = sqlx::query_as(
                "SELECT Name, Color, IconFile FROM CategoryModels WHERE ID = ?",
            )
            .bind(cid)
            .fetch_one(&pool)
            .await
            .unwrap();
            assert_eq!(col["name"], name);
            let color_json = color.map(serde_json::Value::String).unwrap_or(serde_json::Value::Null);
            let icon_json = icon.map(serde_json::Value::String).unwrap_or(serde_json::Value::Null);
            assert_eq!(col["color"], color_json, "categoryId={cid} color");
            assert_eq!(col["iconFile"], icon_json, "categoryId={cid} icon");
        }
        let transparent = columns.iter().find(|c| c["name"] == "透明").unwrap();
        assert!(transparent["color"].is_null(), "NULL 颜色必须保持 null: {transparent}");
    }

    #[tokio::test]
    async fn chart_top_count_prev_equal_old_endpoints() {
        let (app, _pool) = diff_app().await;
        let (start, prev, _, tzq) = tz_params();

        let bundle = get(&app, &format!(
            "/api/data/chart-bundle?start={start}&end={start}&prevStart={prev}&prevEnd={prev}&bucket=hour&take=5&timezone={tzq}"
        )).await;

        let old_top = get(&app, &format!("/api/data/range?start={start}&end={start}&take=5&skip=-1&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["top"], old_top["data"]);

        let old_count = get(&app, &format!("/api/data/range-app-count?start={start}&end={start}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["appCount"], old_count["data"], "今日应用数一致");
        assert_eq!(bundle["data"]["appCount"], 2, "跨零点行不得计入今日");

        let old_count_prev = get(&app, &format!("/api/data/range-app-count?start={prev}&end={prev}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["appCountPrev"], old_count_prev["data"]);
        assert_eq!(bundle["data"]["appCountPrev"], 1, "昨日仅 Old.exe 有数据");

        let old_total_prev = get(&app, &format!("/api/data/range-total?start={prev}&end={prev}&timezone={tzq}")).await;
        let sum: f64 = old_total_prev["data"].as_array().unwrap().iter().map(|v| v.as_f64().unwrap()).sum();
        assert_eq!(bundle["data"]["totalSecondsPrev"], sum, "上期总时长一致（含 45s）");
        assert_eq!(bundle["data"]["totalSecondsPrev"], 45.0);

        let old_web_prev = get(&app, &format!("/api/webdata/browse-duration-total?start={prev}&end={prev}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["webPrev"]["durationTotal"], old_web_prev["data"]);
        assert_eq!(bundle["data"]["webPrev"]["durationTotal"], 60);
    }

    #[tokio::test]
    async fn web_chart_bundle_equal_old_web_endpoints() {
        let (app, _pool) = diff_app().await;
        let (start, _, _, tzq) = tz_params();

        let bundle = get(&app, &format!("/api/webdata/chart-bundle?start={start}&end={start}&take=10&timezone={tzq}")).await;

        let old_stats = get(&app, &format!("/api/webdata/categories-statistics?start={start}&end={start}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["categoryStatistics"], old_stats["data"]);
        assert_eq!(bundle["data"]["categoryStatistics"][0]["value"], 420, "今日 300+120，昨日的 60s 不得计入");

        let old_cols = get(&app, &format!("/api/webdata/browse-category-statistics?start={start}&end={start}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["categoryColumns"], old_cols["data"]);

        let old_top = get(&app, &format!("/api/webdata/range?start={start}&end={start}&take=10&skip=-1&isTime=false&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["topSites"], old_top["data"]);

        let old_duration = get(&app, &format!("/api/webdata/browse-duration-total?start={start}&end={start}&timezone={tzq}")).await;
        let old_sites = get(&app, &format!("/api/webdata/browse-sites-total?start={start}&end={start}&timezone={tzq}")).await;
        let old_pages = get(&app, &format!("/api/webdata/browse-pages-total?start={start}&end={start}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["durationTotal"], old_duration["data"]);
        assert_eq!(bundle["data"]["sitesTotal"], old_sites["data"]);
        assert_eq!(bundle["data"]["pagesTotal"], old_pages["data"]);
        assert_eq!(bundle["data"]["sitesTotal"], 2);
    }

    #[tokio::test]
    async fn with_counts_equal_old_per_category_counts() {
        let (app, _pool) = diff_app().await;
        let cats = get(&app, "/api/category/with-counts").await;
        let old_cats = get(&app, "/api/category").await;
        let old_list = old_cats["data"].as_array().unwrap();
        assert_eq!(cats["data"].as_array().unwrap().len(), old_list.len());

        for c in cats["data"].as_array().unwrap() {
            let id = c["id"].as_i64().unwrap();
            let old = old_list.iter().find(|x| x["id"].as_i64() == Some(id)).expect("分类集合一致");
            for field in ["name", "iconFile", "color", "isDirectoryMatch", "isSystem", "directories"] {
                assert_eq!(c[field], old[field], "分类 {id} 字段 {field}");
            }
            let apps = get(&app, &format!("/api/appdata/by-category/{id}")).await;
            assert_eq!(c["count"], apps["data"].as_array().unwrap().len(), "分类 {id} 应用数");
        }
        let empty = cats["data"].as_array().unwrap().iter().find(|c| c["name"] == "空分类").unwrap();
        assert_eq!(empty["count"], 0, "零应用分类计数为 0");
    }

    #[tokio::test]
    async fn web_with_counts_equal_old_sites_count_endpoint() {
        let (app, _pool) = diff_app().await;
        let cats = get(&app, "/api/webdata/categories-with-counts").await;
        let old_cats = get(&app, "/api/webdata/categories").await;
        let old_list = old_cats["data"].as_array().unwrap();
        assert_eq!(cats["data"].as_array().unwrap().len(), old_list.len());

        for c in cats["data"].as_array().unwrap() {
            let id = c["id"].as_i64().unwrap();
            let old_count = get(&app, &format!("/api/webdata/sites-count?categoryId={id}")).await;
            assert_eq!(c["count"], old_count["data"], "站点分类 {id} 计数");
        }
    }

    #[tokio::test]
    async fn day_detail_equal_old_range_and_sessions() {
        let (app, _pool) = diff_app().await;
        let (start, _, tomorrow, tzq) = tz_params();
        let bundle = get(&app, &format!("/api/data/day-detail?date={start}&timezone={tzq}")).await;

        let old_range = get(&app, &format!("/api/data/range?start={start}&end={start}&take=-1&skip=-1&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["apps"], old_range["data"]);

        let end_of_day = format!("{}T23:59:59", &start[..10]);
        let old_sessions = get(&app, &format!("/api/data/sessions?start={start}&end={end_of_day}&timezone={tzq}")).await;
        assert_eq!(bundle["data"]["sessions"], old_sessions["data"]);
        assert_eq!(bundle["data"]["sessions"].as_array().unwrap().len(), 1);
    }

    #[tokio::test]
    async fn summary_members_equal_old_members_endpoint() {
        let (app, pool) = diff_app().await;
        let cat_id: i64 = sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE Name='工作'")
            .fetch_one(&pool).await.unwrap();
        let (start, _, tomorrow, tzq) = tz_params();

        let bundle = get(&app, &format!(
            "/api/category/app/{cat_id}/summary?start={start}&end={start}&withMembers=true&withModels=true&timezone={tzq}"
        )).await;
        let old_members = get(&app, &format!(
            "/api/category/app/{cat_id}/members?start={start}&end={tomorrow}&withModels=true&timezone={tzq}"
        )).await;
        assert_eq!(bundle["data"]["members"], old_members["data"], "摘要内嵌成员必须与成员端点完全一致");
        assert_eq!(bundle["data"]["members"].as_array().unwrap().len(), 1, "只有 Code.exe 有今日数据");
        assert_eq!(bundle["data"]["totalSeconds"], 900, "今日 600+300");

        let member_app = &bundle["data"]["members"][0]["app"];
        let cat_apps = get(&app, &format!("/api/appdata/by-category/{cat_id}")).await;
        let same = cat_apps["data"].as_array().unwrap().iter()
            .find(|a| a["id"] == member_app["id"])
            .expect("成员应用应出现在分类列表");
        assert_eq!(member_app, same, "内嵌应用模型与旧路径完全一致");
    }

    #[tokio::test]
    async fn batch_category_result_equals_per_app_update() {
        let (app, pool) = diff_app().await;
        let cat_id: i64 = sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE Name='空分类'")
            .fetch_one(&pool).await.unwrap();
        let code_id: i64 = sqlx::query_scalar("SELECT ID FROM AppModels WHERE Name='Code.exe'")
            .fetch_one(&pool).await.unwrap();
        let ghost_id: i64 = sqlx::query_scalar("SELECT ID FROM AppModels WHERE Name='Ghost.exe'")
            .fetch_one(&pool).await.unwrap();

        let ghost: (String, Option<String>, Option<String>, Option<String>, i64, Option<String>, i64) =
            sqlx::query_as("SELECT Name, Alias, Description, File, CategoryID, IconFile, TotalTime FROM AppModels WHERE ID = ?")
                .bind(ghost_id).fetch_one(&pool).await.unwrap();
        let (name, alias, desc, file, _, icon, total) = &ghost;
        let put_body = serde_json::json!({
            "id": ghost_id, "name": name, "alias": alias, "description": desc,
            "file": file, "iconFile": icon, "categoryId": cat_id, "totalTime": total,
        });
        let resp = app.clone().oneshot(
            Request::builder()
                .method("PUT")
                .uri(&format!("/api/appdata/{ghost_id}"))
                .header(header::CONTENT_TYPE, "application/json")
                .body(Body::from(put_body.to_string()))
                .unwrap(),
        ).await.unwrap();
        assert_eq!(resp.status(), StatusCode::OK);

        let batch = {
            let resp = app.clone().oneshot(
                Request::builder()
                    .method("PUT")
                    .uri("/api/appdata/batch-category")
                    .header(header::CONTENT_TYPE, "application/json")
                    .body(Body::from(format!(r#"{{"appIds":[{code_id}],"categoryId":{cat_id}}}"#)))
                    .unwrap(),
            ).await.unwrap();
            let body = resp.into_body().collect().await.unwrap().to_bytes().to_vec();
            let v: serde_json::Value = serde_json::from_slice(&body).unwrap();
            assert_eq!(v["code"], 0, "{v}");
            v
        };

        let batched = &batch["data"][0];
        let old_row = get(&app, &format!("/api/appdata/{ghost_id}")).await;
        assert_eq!(batched["categoryId"], old_row["data"]["categoryId"], "分类归属一致");
        assert_eq!(batched["category"], old_row["data"]["category"], "内嵌分类一致");
        assert_eq!(batched["category"]["name"], "空分类");
    }

    /// 回归：排除集【非空】时走 NOT IN 分支，SQL 不得因拼接产生语法错误，
    /// 且各 bundle 与旧端点结果一致（此分支曾因模板双重拼接 500，被真实数据对拍抓出）
    #[tokio::test]
    async fn nonempty_excluded_set_matches_old_endpoints() {
        let pool = SqlitePool::connect("sqlite::memory:").await.unwrap();
        crate::migrations::run(&pool, "UTC").await.unwrap();
        crate::services::category::invalidate_category_cache().await;
        crate::services::web_data::invalidate_web_category_cache().await;

        let now = Utc::now();
        let today = now.date_naive();
        // 与 diff_app 保持完全一致的三分类元数据：分类列表缓存是进程级全局的，
        // 各测试并行时互相读到对方的缓存也必须无歧义
        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('工作', 'work.png', '#4a90d9', 0, '[]', 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('透明', NULL, NULL, 0, '[]', 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO CategoryModels (Name, IconFile, Color, IsDirectoryMatch, Directories, IsSystem) VALUES ('空分类', 'empty.png', '#000000', 0, '[]', 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO AppModels (Name, Alias, Description, File, CategoryID, IconFile, TotalTime) VALUES ('Code.exe', NULL, NULL, NULL, (SELECT ID FROM CategoryModels WHERE Name='工作'), NULL, 0)")
            .execute(&pool).await.unwrap();
        sqlx::query("INSERT INTO AppModels (Name, Alias, Description, File, CategoryID, IconFile, TotalTime) VALUES ('Keep.exe', NULL, NULL, NULL, (SELECT ID FROM CategoryModels WHERE Name='工作'), NULL, 0)")
            .execute(&pool).await.unwrap();
        for (app, secs) in [("Code.exe", 600i64), ("Keep.exe", 300)] {
            sqlx::query("INSERT INTO HoursLogModels (DataTime, AppModelID, Time) VALUES (?1, (SELECT ID FROM AppModels WHERE Name = ?2), ?3)")
                .bind(now).bind(app).bind(secs).execute(&pool).await.unwrap();
        }

        let root = std::env::temp_dir().join(format!("taix-excl-{}", std::process::id()));
        let _ = std::fs::remove_dir_all(&root);
        std::fs::create_dir_all(&root).unwrap();
        std::fs::write(
            root.join("AppConfig.json"),
            r#"{"Version":4,"Behavior":{"IgnoreProcessList":["Code.exe"],"IsWhiteList":false}}"#,
        ).unwrap();
        let config_service = Arc::new(ConfigService::new(&root));

        let app = router()
            .merge(crate::routes::data::router())
            .with_state(pool.clone())
            .layer(axum::extract::Extension(config_service));
        let start = format!("{today}T00:00:00");
        let tzq = "UTC";

        // 排除已生效：被忽略的应用不得出现在结果里（证明走的是 NOT IN 分支）
        let range = get(&app, &format!("/api/data/range?start={start}&end={start}&take=-1&skip=-1&timezone={tzq}")).await;
        let names: Vec<&str> = range["data"].as_array().unwrap()
            .iter().filter_map(|r| r["appModel"]["name"].as_str()).collect();
        assert!(!names.contains(&"Code.exe"), "排除应用不得出现: {names:?}");
        assert!(names.contains(&"Keep.exe"), "保留应用必须在: {names:?}");

        // bundle 与旧端点在非空排除集下依旧一致
        let bundle = get(&app, &format!(
            "/api/data/chart-bundle?start={start}&end={start}&bucket=hour&take=5&timezone={tzq}")).await;
        let old_hours = get(&app, &format!("/api/data/category-hours?date={start}&timezone={tzq}")).await;
        let old_map: std::collections::HashMap<i64, serde_json::Value> = old_hours["data"].as_array().unwrap()
            .iter().map(|r| (r["categoryId"].as_i64().unwrap(), r["values"].clone())).collect();
        for col in bundle["data"]["columns"].as_array().unwrap() {
            let cid = col["categoryId"].as_i64().unwrap();
            assert_eq!(col["values"], old_map[&cid], "排除集非空时 categoryId={cid} 小时值必须一致");
        }

        let overview = get(&app, &format!("/api/data/overview?start={start}&end={start}&take=0&withColumns=false&timezone={tzq}")).await;
        assert_eq!(overview["data"]["apps"], range["data"], "overview.apps 与旧端点一致");

        std::fs::remove_dir_all(&root).ok();
    }
}

#[cfg(test)]
mod fixture_export {
    use super::differential_tests::{diff_app, get, tz_params};
    use sqlx::SqlitePool;

    #[tokio::test]
    async fn dump_response_fixtures() {
        let (app, pool) = diff_app().await;
        let (start, prev, _tomorrow, _tz) = tz_params();
        let cat_id: i64 = sqlx::query_scalar("SELECT ID FROM CategoryModels WHERE Name='工作'")
            .fetch_one(&pool).await.unwrap();

        let dir = "/tmp/taix-fixtures";
        std::fs::create_dir_all(dir).unwrap();

        let cases: Vec<(&str, String)> = vec![
            ("overview", format!("/api/data/overview?start={start}&end={start}&take=0&withColumns=true&timezone=Asia/Shanghai")),
            ("app_chart", format!("/api/data/chart-bundle?start={start}&end={start}&prevStart={prev}&prevEnd={prev}&bucket=hour&take=5&timezone=Asia/Shanghai")),
            ("web_chart", format!("/api/webdata/chart-bundle?start={start}&end={start}&take=10&timezone=Asia/Shanghai")),
            ("day_detail", format!("/api/data/day-detail?date={start}&timezone=Asia/Shanghai")),
            ("category_with_counts", "/api/category/with-counts".to_string()),
            ("web_category_with_counts", "/api/webdata/categories-with-counts".to_string()),
            ("summary_with_members", format!("/api/category/app/{cat_id}/summary?start={start}&end={start}&withMembers=true&withModels=true&timezone=Asia/Shanghai")),
        ];
        for (name, uri) in cases {
            let v = get(&app, &uri).await;
            std::fs::write(format!("{dir}/{name}.json"), v.to_string()).unwrap();
        }
    }
}
