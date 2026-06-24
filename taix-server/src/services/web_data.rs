use chrono::{DateTime, Datelike, Duration, NaiveDate, NaiveDateTime, Timelike, Utc};
use regex::RegexSet;
use sqlx::SqlitePool;
use std::collections::HashMap;
use std::path::Path;
use std::sync::Arc;
use tokio::sync::RwLock;
use tracing::{debug, info, warn};

use crate::constants;
use crate::error::AppError;
use crate::models::request::{AddUrlBrowseTimeRequest, UpdateSitesCategoryRequest};
use crate::models::log::{ColumnDataModel, InfrastructureDataModel};
use crate::models::web::{WebBrowseLogModel, WebSiteCategoryModel, WebSiteModel, WebUrlModel};
use crate::services::config::ConfigService;
use crate::utils::{parse_timezone, tz_naive_to_utc};

static WEB_CATEGORY_CACHE: std::sync::OnceLock<RwLock<Option<Vec<WebSiteCategoryModel>>>> = std::sync::OnceLock::new();
static URL_MATCH_CACHE: std::sync::OnceLock<RwLock<Option<Vec<CompiledUrlMatchRule>>>> = std::sync::OnceLock::new();
static SYSTEM_CATEGORY_ID: std::sync::OnceLock<RwLock<Option<i64>>> = std::sync::OnceLock::new();

fn web_category_cache() -> &'static RwLock<Option<Vec<WebSiteCategoryModel>>> {
    WEB_CATEGORY_CACHE.get_or_init(|| RwLock::new(None))
}

fn url_match_cache() -> &'static RwLock<Option<Vec<CompiledUrlMatchRule>>> {
    URL_MATCH_CACHE.get_or_init(|| RwLock::new(None))
}

fn system_category_id_cache() -> &'static RwLock<Option<i64>> {
    SYSTEM_CATEGORY_ID.get_or_init(|| RwLock::new(None))
}

/// 编译后的 URL 匹配规则
#[derive(Clone)]
struct CompiledUrlMatchRule {
    category_id: i64,
    regex_set: Arc<RegexSet>,
}

async fn invalidate_web_category_cache() {
    let mut cache = web_category_cache().write().await;
    *cache = None;
}

async fn invalidate_url_match_cache() {
    let mut cache = url_match_cache().write().await;
    *cache = None;
}

/// 查询 WebSiteModels 并计算应排除的 SiteId 列表
async fn get_excluded_site_ids(
    pool: &SqlitePool,
    config_service: &ConfigService,
) -> Vec<i64> {
    const MAX_EXCLUDED_IDS: usize = 900;

    let sites: Vec<(i64, String)> = sqlx::query_as("SELECT ID, Domain FROM WebSiteModels")
        .fetch_all(pool)
        .await
        .unwrap_or_default();
    if sites.is_empty() {
        return Vec::new();
    }
    let domains: Vec<&str> = sites.iter().map(|(_, d)| d.as_str()).collect();
    let excluded_domains = config_service.get_excluded_domains(&domains).await;
    let excluded_set: std::collections::HashSet<String> = excluded_domains.into_iter().collect();
    let mut ids: Vec<i64> = sites.into_iter()
        .filter(|(_, d)| excluded_set.contains(d))
        .map(|(id, _)| id)
        .collect();
    if ids.len() > MAX_EXCLUDED_IDS {
        tracing::warn!(
            "排除站点 ID 数量 {} 超过 SQLite 参数上限保护值 {}，已截断；超出的站点将不会被过滤",
            ids.len(),
            MAX_EXCLUDED_IDS
        );
        ids.truncate(MAX_EXCLUDED_IDS);
    }
    ids
}

pub struct WebDataService;

impl WebDataService {
    pub async fn add_url_browse_time(pool: &SqlitePool, req: AddUrlBrowseTimeRequest, favicons_dir: Option<&Path>) -> Result<(i64, i64), AppError> {
        if req.url.is_empty() {
            debug!("add_url_browse_time: empty url");
            return Ok((0, 0));
        }
        if req.duration <= 0 {
            debug!("add_url_browse_time: skip zero or negative duration");
            return Ok((0, 0));
        }
        if is_browser_internal_page(&req.url, req.title.as_deref()) {
            debug!("add_url_browse_time: skip browser internal page url={}", req.url);
            return Ok((0, 0));
        }
        if !req.url.starts_with("http://") && !req.url.starts_with("https://") {
            debug!("add_url_browse_time: skip non-http url={}", req.url);
            return Ok((0, 0));
        }
        debug!("add_url_browse_time: url={} duration={}", req.url, req.duration);

        // 时间戳合法性校验：拒绝明显过去或未来的时间
        if let Some(dt) = req.date_time {
            if dt.year() < constants::MIN_VALID_YEAR {
                debug!("add_url_browse_time: skip invalid timestamp year={}", dt.year());
                return Ok((0, 0));
            }
            let now_utc = Utc::now();
            if dt > now_utc + Duration::minutes(constants::FUTURE_TIMESTAMP_TOLERANCE_MINUTES) {
                warn!("add_url_browse_time: future timestamp {} (> now + 5min), rejecting", dt);
                return Ok((0, 0));
            }
        }

        let mut current_req = req;
        if current_req.duration > constants::MAX_WEB_DURATION_SECS {
            tracing::warn!(
                "add_url_browse_time: duration {}s exceeds max {}s, truncating",
                current_req.duration, constants::MAX_WEB_DURATION_SECS
            );
            current_req.duration = constants::MAX_WEB_DURATION_SECS;
        }

        let mut tx = pool.begin().await?;
        let mut loop_count = 0;
        let (result_site_id, result_url_id) = loop {
            loop_count += 1;
            if loop_count > constants::MAX_ADD_URL_ITERATIONS {
                warn!("add_url_browse_time: loop exceeded 48 iterations, breaking to prevent infinite loop");
                break (0, 0);
            }

            let date_time = current_req.date_time.unwrap_or_else(Utc::now);


            let log_time = date_time.with_minute(0).unwrap().with_second(0).unwrap().with_nanosecond(0).unwrap();
            let duration = current_req.duration;
            let now_time_max = (constants::MINS_PER_HOUR - date_time.minute() as i64) * constants::SECS_PER_MIN - date_time.second() as i64;

            let mut now_duration = duration;
            let mut next_duration = 0i64;

            if now_duration > constants::SECS_PER_HOUR {
                next_duration = now_duration - constants::SECS_PER_HOUR;
                now_duration = constants::SECS_PER_HOUR;
            }
            if now_duration > now_time_max {
                next_duration += now_duration - now_time_max;
                now_duration = now_time_max;
            }

            let domain = extract_domain(&current_req.url);

            let site_title = extract_site_name(&domain, current_req.title.as_deref());

            let site_id: i64 = {
                let existing: Option<(i64,)> = sqlx::query_as("SELECT ID FROM WebSiteModels WHERE Domain = ?")
                    .bind(&domain).fetch_optional(&mut *tx).await?;
                if let Some((id,)) = existing { id } else {
                    let matched_category_id = match_url_for_new_site(&domain).await;
                    let category_id = matched_category_id.unwrap_or(get_system_category_id_cached().await);
                    sqlx::query("INSERT INTO WebSiteModels (Title, Domain, CategoryID) VALUES (?, ?, ?)")
                        .bind(&site_title).bind(&domain).bind(category_id).execute(&mut *tx).await?.last_insert_rowid()
                }
            };

            let url_title = current_req.title.as_ref().and_then(|t| clean_title(t));

            let url_id: i64 = {
                let existing: Option<(i64,)> = sqlx::query_as("SELECT ID FROM WebUrlModels WHERE Url = ?")
                    .bind(&current_req.url).fetch_optional(&mut *tx).await?;
                if let Some((id,)) = existing { id } else {
                    sqlx::query("INSERT INTO WebUrlModels (Url, Title) VALUES (?, ?)")
                        .bind(&current_req.url).bind(&url_title).execute(&mut *tx).await?.last_insert_rowid()
                }
            };

            let existing: Option<(i64, i64)> = sqlx::query_as("SELECT ID, Duration FROM WebBrowseLogModels WHERE LogTime = ? AND UrlId = ?")
                .bind(log_time).bind(url_id).fetch_optional(&mut *tx).await?;

            let actual_added = if let Some((id, existing_duration)) = existing {
                let new_duration = (existing_duration + now_duration).min(constants::SECS_PER_HOUR);
                let overflow = (existing_duration + now_duration) - new_duration;
                if overflow > 0 {
                    next_duration += overflow;
                }
                sqlx::query("UPDATE WebBrowseLogModels SET Duration = ? WHERE ID = ?")
                    .bind(new_duration).bind(id).execute(&mut *tx).await?;
                now_duration - overflow
            } else {
                sqlx::query("INSERT INTO WebBrowseLogModels (UrlId, LogTime, Duration, SiteId) VALUES (?, ?, ?, ?)")
                    .bind(url_id).bind(log_time).bind(now_duration).bind(site_id).execute(&mut *tx).await?;
                now_duration
            };

            if actual_added > 0 {
                sqlx::query("UPDATE WebSiteModels SET Duration = Duration + ? WHERE ID = ?")
                    .bind(actual_added).bind(site_id).execute(&mut *tx).await?;
            }

            // 防止 Duration 无限累加
            sqlx::query("UPDATE WebSiteModels SET Duration = ? WHERE ID = ? AND Duration > ?")
                .bind(constants::MAX_SITE_DURATION_SECS).bind(site_id).bind(constants::MAX_SITE_DURATION_SECS).execute(&mut *tx).await?;

            if next_duration > 0 {
                current_req = AddUrlBrowseTimeRequest {
                    url: current_req.url.clone(),
                    title: current_req.title.clone(),
                    duration: next_duration,
                    date_time: Some(log_time + Duration::hours(1)),
                    icon_url: current_req.icon_url.clone(),
                };
                continue;
            }

            break (site_id, url_id);
        };

        tx.commit().await?;

        // 尝试保存图标
        if let Some(dir) = favicons_dir {
            let icon_url = current_req.icon_url.clone();
            let url_str = current_req.url.clone();
            let pool = pool.clone();
            let dir = dir.to_path_buf();
            tokio::spawn(async move {
                // 如果有图标URL，直接下载
                if let Some(ref icon_url) = icon_url {
                    if !icon_url.is_empty() && (icon_url.starts_with("http://") || icon_url.starts_with("https://")) {
                        let _ = save_icon(icon_url, &dir, result_site_id, result_url_id, &pool).await;
                        return;
                    }
                }
                // 否则尝试从域名获取默认favicon
                if let Some((scheme, domain)) = extract_domain_scheme(&url_str) {
                let default_icon_url = format!("{}://{}/favicon.ico", scheme, domain);
                let _ = save_icon(&default_icon_url, &dir, result_site_id, result_url_id, &pool).await;
            }
            });
        }

        Ok((result_site_id, result_url_id))
    }

    pub async fn get_web_sites(pool: &SqlitePool, category_id: Option<i64>, config_service: &ConfigService) -> Result<Vec<WebSiteModel>, AppError> {
        debug!("get_web_sites: category_id={:?}", category_id);
        let rows = if let Some(cat_id) = category_id {
            sqlx::query_as::<_, WebSiteModel>("SELECT * FROM WebSiteModels WHERE CategoryID = ?")
                .bind(cat_id).fetch_all(pool).await?
        } else {
            sqlx::query_as::<_, WebSiteModel>("SELECT * FROM WebSiteModels").fetch_all(pool).await?
        };
        let domains: Vec<&str> = rows.iter().filter_map(|r| r.domain.as_deref()).collect();
        let excluded = config_service.get_excluded_domains(&domains).await;
        let excluded_set: std::collections::HashSet<String> = excluded.into_iter().collect();
        Ok(rows.into_iter().filter(|r| r.domain.as_ref().map_or(true, |d| !excluded_set.contains(d))).collect())
    }

    pub async fn get_web_site_categories(pool: &SqlitePool) -> Result<Vec<WebSiteCategoryModel>, AppError> {
        debug!("get_web_site_categories");

        {
            let cache = web_category_cache().read().await;
            if let Some(cached) = cache.as_ref() {
                debug!("get_web_site_categories from cache");
                return Ok(cached.clone());
            }
        }

        let mut cats: Vec<WebSiteCategoryModel> =
            sqlx::query_as("SELECT * FROM WebSiteCategoryModels WHERE IsSystem = 0")
                .fetch_all(pool).await?;

        let system: Option<WebSiteCategoryModel> =
            sqlx::query_as("SELECT * FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1")
                .fetch_optional(pool).await?;

        let result = if let Some(sys) = system {
            let mut all = vec![sys];
            all.append(&mut cats);
            all
        } else {
            cats
        };

        let mut cache = web_category_cache().write().await;
        *cache = Some(result.clone());

        Ok(result)
    }

    pub async fn create_web_site_category(pool: &SqlitePool, data: WebSiteCategoryModel) -> Result<WebSiteCategoryModel, AppError> {
        info!("create_web_site_category: name={}", data.name);
        let id = sqlx::query("INSERT INTO WebSiteCategoryModels (Name, IconFile, Color, IsUrlMatch, UrlPatterns) VALUES (?, ?, ?, ?, ?)")
            .bind(&data.name).bind(&data.icon_file).bind(&data.color)
            .bind(data.is_url_match).bind(&data.url_patterns)
            .execute(pool).await?.last_insert_rowid();
        invalidate_web_category_cache().await;
        invalidate_url_match_cache().await;
        Ok(WebSiteCategoryModel {
            id,
            name: data.name,
            icon_file: data.icon_file,
            color: data.color,
            is_url_match: data.is_url_match,
            url_patterns: data.url_patterns,
            is_system: false,
        })
    }

    pub async fn update_web_site_category(pool: &SqlitePool, id: i64, data: WebSiteCategoryModel) -> Result<(), AppError> {
        info!("update_web_site_category: id={}", id);
        let existing: Option<WebSiteCategoryModel> = sqlx::query_as("SELECT * FROM WebSiteCategoryModels WHERE ID = ?")
            .bind(id).fetch_optional(pool).await?;
        if existing.is_none() {
            return Err(AppError::Business("分类不存在".to_string()));
        }
        sqlx::query("UPDATE WebSiteCategoryModels SET Name = ?, IconFile = ?, Color = ?, IsUrlMatch = ?, UrlPatterns = ? WHERE ID = ?")
            .bind(&data.name).bind(&data.icon_file).bind(&data.color)
            .bind(data.is_url_match).bind(&data.url_patterns).bind(id)
            .execute(pool).await?;
        invalidate_web_category_cache().await;
        invalidate_url_match_cache().await;
        Ok(())
    }

    pub async fn delete_web_site_category(pool: &SqlitePool, id: i64) -> Result<(), AppError> {
        info!("delete_web_site_category: id={}", id);

        let mut tx = pool.begin().await?;

        let existing: Option<WebSiteCategoryModel> = sqlx::query_as("SELECT * FROM WebSiteCategoryModels WHERE ID = ?")
            .bind(id).fetch_optional(&mut *tx).await?;

        let Some(existing) = existing else {
            return Err(AppError::Business("分类不存在".to_string()));
        };

        if existing.is_system {
            return Err(AppError::Business("系统分类不能删除".to_string()));
        }

        let site_count: i64 =
            sqlx::query_scalar("SELECT COUNT(*) FROM WebSiteModels WHERE CategoryID = ?")
                .bind(id)
                .fetch_one(&mut *tx)
                .await?;

        if site_count > 0 {
            let system_category_id: i64 =
                sqlx::query_scalar("SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1")
                    .fetch_one(&mut *tx)
                    .await?;

            sqlx::query("UPDATE WebSiteModels SET CategoryID = ? WHERE CategoryID = ?")
                .bind(system_category_id)
                .bind(id)
                .execute(&mut *tx)
                .await?;
        }

        sqlx::query("DELETE FROM WebSiteCategoryModels WHERE ID = ?")
            .bind(id)
            .execute(&mut *tx)
            .await?;

        tx.commit().await?;
        invalidate_web_category_cache().await;
        Ok(())
    }

    pub async fn get_web_site(pool: &SqlitePool, id: i64) -> Result<Option<WebSiteModel>, AppError> {
        debug!("get_web_site: id={}", id);
        let site: Option<WebSiteModel> = sqlx::query_as("SELECT * FROM WebSiteModels WHERE ID = ?")
            .bind(id).fetch_optional(pool).await?;
        Ok(site)
    }

    pub async fn get_web_site_by_domain(pool: &SqlitePool, domain: &str) -> Result<Option<WebSiteModel>, AppError> {
        debug!("get_web_site_by_domain: domain={}", domain);
        let site: Option<WebSiteModel> = sqlx::query_as("SELECT * FROM WebSiteModels WHERE LOWER(Domain) = LOWER(?)")
            .bind(domain).fetch_optional(pool).await?;
        Ok(site)
    }

    pub async fn update_web_site(pool: &SqlitePool, id: i64, website: WebSiteModel) -> Result<Option<WebSiteModel>, AppError> {
        info!("update_web_site: id={}", id);
        sqlx::query("UPDATE WebSiteModels SET Alias = ?, Domain = ?, Title = ? WHERE ID = ?")
            .bind(&website.alias).bind(&website.domain).bind(&website.title).bind(id)
            .execute(pool).await?;
        Self::get_web_site(pool, id).await
    }

    pub async fn update_web_sites_category(pool: &SqlitePool, req: UpdateSitesCategoryRequest) -> Result<(), AppError> {
        info!("update_web_sites_category: site_ids={:?} category_id={}", req.site_ids, req.category_id);
        let category_id = if req.category_id > 0 {
            let cat_exists: Option<(i64,)> = sqlx::query_as("SELECT ID FROM WebSiteCategoryModels WHERE ID = ?")
                .bind(req.category_id)
                .fetch_optional(pool)
                .await?;
            if cat_exists.is_none() {
                warn!("update_web_sites_category: category_id={} not found, fallback to system category", req.category_id);
                0
            } else {
                req.category_id
            }
        } else {
            0
        };
        let category_id = if category_id > 0 {
            category_id
        } else {
            let system_id: Option<(i64,)> = sqlx::query_as("SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1")
                .fetch_optional(pool)
                .await?;
            match system_id {
                Some((id,)) => id,
                None => {
                    warn!("update_web_sites_category: system category not found, skip");
                    return Ok(());
                }
            }
        };
        if !req.site_ids.is_empty() {
            let mut builder = sqlx::QueryBuilder::new("UPDATE WebSiteModels SET CategoryID = ");
            builder.push_bind(category_id);
            builder.push(" WHERE ID IN (");
            let mut first = true;
            for site_id in &req.site_ids {
                if !first { builder.push(","); }
                builder.push_bind(site_id);
                first = false;
            }
            builder.push(")");
            builder.build().execute(pool).await?;
        }
        Ok(())
    }

    pub async fn get_unset_category_web_sites(pool: &SqlitePool, config_service: &ConfigService) -> Result<Vec<WebSiteModel>, AppError> {
        debug!("get_unset_category_web_sites");
        let system_category_id: Option<(i64,)> = sqlx::query_as("SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1")
            .fetch_optional(pool).await?;
        let category_id = match system_category_id {
            Some((id,)) => id,
            None => return Ok(Vec::new()),
        };
        let sites: Vec<WebSiteModel> = sqlx::query_as("SELECT * FROM WebSiteModels WHERE CategoryID = ?")
            .bind(category_id).fetch_all(pool).await?;
        let domains: Vec<&str> = sites.iter().filter_map(|r| r.domain.as_deref()).collect();
        let excluded = config_service.get_excluded_domains(&domains).await;
        let excluded_set: std::collections::HashSet<String> = excluded.into_iter().collect();
        Ok(sites.into_iter().filter(|r| r.domain.as_ref().map_or(true, |d| !excluded_set.contains(d))).collect())
    }

    pub async fn clear_web_data(pool: &SqlitePool, start: Option<NaiveDate>, end: Option<NaiveDate>, site_id: Option<i64>, tz_id: &str) -> Result<(), AppError> {
        info!("clear_web_data: start={:?} end={:?} site_id={:?}", start, end, site_id);
        if let Some(sid) = site_id {
            sqlx::query("DELETE FROM WebBrowseLogModels WHERE SiteId = ?").bind(sid).execute(pool).await?;
            sqlx::query(
                "UPDATE WebSiteModels SET Duration = COALESCE((SELECT SUM(Duration) FROM WebBrowseLogModels WHERE SiteId = ?), 0) WHERE ID = ?"
            )
            .bind(sid)
            .bind(sid)
            .execute(pool)
            .await?;
        } else if let (Some(start), Some(end)) = (start, end) {
            let end_dt = NaiveDateTime::new(
                end,
                chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap(),
            );
            let utc_start = tz_naive_to_utc(NaiveDateTime::new(start, chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap()), &parse_timezone(tz_id));
            let utc_end = tz_naive_to_utc(end_dt, &parse_timezone(tz_id));
            sqlx::query("DELETE FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ?")
                .bind(utc_start).bind(utc_end).execute(pool).await?;

            // 重算所有 WebSite 的 Duration，避免按日期范围清除后总计虚高
            sqlx::query(
                "UPDATE WebSiteModels SET Duration = COALESCE((SELECT SUM(Duration) FROM WebBrowseLogModels WHERE SiteId = WebSiteModels.ID), 0)"
            )
            .execute(pool)
            .await?;
        }
        Ok(())
    }

    pub async fn get_date_range_web_site_list(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, take: i64, skip: i64, is_time: bool, tz_id: &str, config_service: &ConfigService) -> Result<Vec<WebSiteModel>, AppError> {
        debug!("get_date_range_web_site_list: start={} end={} take={} skip={} is_time={}", start, end, take, skip, is_time);
        let (start, end) = if is_time {
            (NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(start.hour(), 0, 0).unwrap()),
             NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(end.hour(), 59, 59).unwrap()))
        } else {
            (NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap()),
             NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap()))
        };
        let utc_start = tz_naive_to_utc(start, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end, &parse_timezone(tz_id));

        // 内存过滤后分页
        let rows: Vec<(i64, i64)> = sqlx::query_as(
            r#"SELECT SiteId, SUM(Duration) as duration FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ? AND SiteId != 0 GROUP BY SiteId ORDER BY duration DESC"#
        )
        .bind(utc_start).bind(utc_end).fetch_all(pool).await?;

        if rows.is_empty() {
            return Ok(Vec::new());
        }

        let mut builder = sqlx::QueryBuilder::new("SELECT * FROM WebSiteModels WHERE ID IN (");
        let mut separated = builder.separated(", ");
        for (sid, _) in &rows {
            separated.push_bind(*sid);
        }
        separated.push_unseparated(")");
        let sites: Vec<WebSiteModel> = builder.build_query_as().fetch_all(pool).await?;

        let mut site_map: std::collections::HashMap<i64, WebSiteModel> =
            sites.into_iter().map(|s| (s.id, s)).collect();

        let mut result = Vec::new();
        for (sid, dur) in rows {
            if let Some(mut s) = site_map.remove(&sid) {
                s.duration = dur;
                result.push(s);
            }
        }
        let domains: Vec<&str> = result.iter().filter_map(|r| r.domain.as_deref()).collect();
        let excluded = config_service.get_excluded_domains(&domains).await;
        let excluded_set: std::collections::HashSet<String> = excluded.into_iter().collect();
        let mut result: Vec<_> = result.into_iter()
            .filter(|r| r.domain.as_ref().map_or(true, |d| !excluded_set.contains(d)))
            .collect();

        let offset = skip.max(0) as usize;
        let limit = if take > 0 { take as usize } else { usize::MAX };
        if offset > 0 {
            result = result.into_iter().skip(offset).collect();
        }
        if limit < usize::MAX {
            result.truncate(limit);
        }
        Ok(result)
    }

    pub async fn get_web_sites_count(pool: &SqlitePool, category_id: i64) -> Result<i64, AppError> {
        debug!("get_web_sites_count: category_id={}", category_id);
        let count: (i64,) = sqlx::query_as("SELECT COUNT(*) FROM WebSiteModels WHERE CategoryID = ?")
            .bind(category_id).fetch_one(pool).await?;
        Ok(count.0)
    }

    pub async fn get_categories_statistics(pool: &SqlitePool, start: NaiveDate, end: NaiveDate, tz_id: &str, config_service: &ConfigService) -> Result<Vec<InfrastructureDataModel>, AppError> {
        debug!("get_categories_statistics: start={} end={}", start, end);
        let excluded_site_ids = get_excluded_site_ids(pool, config_service).await;
        let has_excluded = !excluded_site_ids.is_empty();

        let start_dt = NaiveDateTime::new(start, chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_dt = NaiveDateTime::new(end, chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_dt, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end_dt, &parse_timezone(tz_id));

        let data: Vec<(i64, String, i64)> = if !has_excluded {
            sqlx::query_as(
                r#"SELECT wsc.ID, wsc.Name, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                JOIN WebSiteCategoryModels wsc ON ws.CategoryID = wsc.ID
                WHERE wbl.LogTime >= ? AND wbl.LogTime <= ?
                GROUP BY wsc.ID, wsc.Name"#
            ).bind(utc_start).bind(utc_end).fetch_all(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                r#"SELECT wsc.ID, wsc.Name, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                JOIN WebSiteCategoryModels wsc ON ws.CategoryID = wsc.ID
                WHERE wbl.LogTime >= ? AND wbl.LogTime <= ? AND wbl.SiteId NOT IN ({})
                GROUP BY wsc.ID, wsc.Name"#,
                placeholders
            );
            let mut query = sqlx::query_as::<_, (i64, String, i64)>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_all(pool).await?
        };

        let no_category: Vec<(i64, i64)> = if !has_excluded {
            sqlx::query_as(
                r#"SELECT ws.CategoryID, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                WHERE ws.CategoryID = 0 AND wbl.LogTime >= ? AND wbl.LogTime <= ?
                GROUP BY ws.CategoryID"#
            ).bind(utc_start).bind(utc_end).fetch_all(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                r#"SELECT ws.CategoryID, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                WHERE ws.CategoryID = 0 AND wbl.LogTime >= ? AND wbl.LogTime <= ? AND wbl.SiteId NOT IN ({})
                GROUP BY ws.CategoryID"#,
                placeholders
            );
            let mut query = sqlx::query_as::<_, (i64, i64)>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_all(pool).await?
        };

        let mut result: Vec<InfrastructureDataModel> = data.into_iter()
            .map(|(id, name, value)| InfrastructureDataModel { id, name, value })
            .collect();
        for (id, value) in no_category {
            result.push(InfrastructureDataModel { id, name: "未分类".to_string(), value });
        }
        Ok(result)
    }

    pub async fn get_browse_data_statistics(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, site_id: i64, tz_id: &str, config_service: &ConfigService) -> Result<Vec<InfrastructureDataModel>, AppError> {
        debug!("get_browse_data_statistics: start={} end={} site_id={}", start, end, site_id);
        let excluded_site_ids = get_excluded_site_ids(pool, config_service).await;
        let has_excluded = !excluded_site_ids.is_empty();

        let tz = parse_timezone(tz_id);
        let start_date = NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_date = NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_date, &tz);
        let utc_end = tz_naive_to_utc(end_date, &tz);

        let mut q = String::from("SELECT LogTime, SUM(Duration) as total FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ?");
        if site_id > 0 { q.push_str(" AND SiteId = ?"); }
        if has_excluded {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            q.push_str(&format!(" AND SiteId NOT IN ({})", placeholders));
        }
        q.push_str(" GROUP BY LogTime");

        let mut query = sqlx::query_as::<_, (DateTime<Utc>, i64)>(&q)
            .bind(utc_start).bind(utc_end);
        if site_id > 0 { query = query.bind(site_id); }
        if has_excluded {
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
        }
        let data = query.fetch_all(pool).await?;

        let mut result = Vec::new();
        if start_date.date() == end_date.date() {
            let mut buckets = [0i64; 24];
            for (t, v) in &data {
                let h = t.with_timezone(&tz).hour() as usize;
                if h < 24 {
                    buckets[h] += *v;
                }
            }
            for (i, value) in buckets.iter().enumerate() {
                result.push(InfrastructureDataModel { id: i as i64, name: i.to_string(), value: *value });
            }
        } else {
            let days = (end_date.date() - start_date.date()).num_days() + 1;
            if days <= constants::DAY_VIEW_THRESHOLD {
                let days_usize = days as usize;
                let mut buckets = vec![0i64; days_usize];
                let start_local_date = start_date.date();
                for (t, v) in &data {
                    let local_date = t.with_timezone(&tz).date_naive();
                    let offset = (local_date - start_local_date).num_days();
                    if offset >= 0 && (offset as usize) < days_usize {
                        buckets[offset as usize] += *v;
                    }
                }
                for (i, value) in buckets.iter().enumerate() {
                    result.push(InfrastructureDataModel { id: i as i64, name: i.to_string(), value: *value });
                }
            } else {
                let mut buckets = [0i64; 12];
                let target_year = start_date.year();
                for (t, v) in &data {
                    let local = t.with_timezone(&tz);
                    if local.year() == target_year {
                        let m = (local.month() - 1) as usize;
                        if m < 12 {
                            buckets[m] += *v;
                        }
                    }
                }
                for (i, value) in buckets.iter().enumerate() {
                    result.push(InfrastructureDataModel { id: i as i64, name: i.to_string(), value: *value });
                }
            }
        }
        Ok(result)
    }

    pub async fn get_browse_data_by_category_statistics(pool: &SqlitePool, start: NaiveDate, end: NaiveDate, tz_id: &str, config_service: &ConfigService) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_browse_data_by_category_statistics: start={} end={}", start, end);
        let excluded_site_ids = get_excluded_site_ids(pool, config_service).await;
        let has_excluded = !excluded_site_ids.is_empty();

        let tz = parse_timezone(tz_id);
        let start_dt = NaiveDateTime::new(start, chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_dt = NaiveDateTime::new(end, chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_dt, &tz);
        let utc_end = tz_naive_to_utc(end_dt, &tz);

        let categories: Vec<(i64, i64)> = if !has_excluded {
            sqlx::query_as(
                r#"SELECT COALESCE(ws.CategoryID, 0) as cat_id, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                LEFT JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                WHERE wbl.LogTime >= ? AND wbl.LogTime <= ?
                GROUP BY cat_id"#
            ).bind(utc_start).bind(utc_end).fetch_all(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                r#"SELECT COALESCE(ws.CategoryID, 0) as cat_id, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                LEFT JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                WHERE wbl.LogTime >= ? AND wbl.LogTime <= ? AND wbl.SiteId NOT IN ({})
                GROUP BY cat_id"#,
                placeholders
            );
            let mut query = sqlx::query_as::<_, (i64, i64)>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_all(pool).await?
        };

        let data: Vec<(i64, DateTime<Utc>, i64)> = if !has_excluded {
            sqlx::query_as(
                r#"SELECT COALESCE(ws.CategoryID, 0) as cat_id, wbl.LogTime, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                LEFT JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                WHERE wbl.LogTime >= ? AND wbl.LogTime <= ?
                GROUP BY cat_id, wbl.LogTime"#
            ).bind(utc_start).bind(utc_end).fetch_all(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                r#"SELECT COALESCE(ws.CategoryID, 0) as cat_id, wbl.LogTime, SUM(wbl.Duration) as total FROM WebBrowseLogModels wbl
                LEFT JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
                WHERE wbl.LogTime >= ? AND wbl.LogTime <= ? AND wbl.SiteId NOT IN ({})
                GROUP BY cat_id, wbl.LogTime"#,
                placeholders
            );
            let mut query = sqlx::query_as::<_, (i64, DateTime<Utc>, i64)>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_all(pool).await?
        };

        let mut result = Vec::new();
        let mut result_map: HashMap<i64, &mut ColumnDataModel> = HashMap::new();

        if start == end {
            for (cat_id, _) in &categories {
                result.push(ColumnDataModel { app_id: None, category_id: Some(*cat_id), values: vec![0.0; 24] });
            }
            for item in &mut result {
                if let Some(cid) = item.category_id {
                    result_map.insert(cid, item);
                }
            }
            for (cid, t, d) in &data {
                if let Some(item) = result_map.get_mut(cid) {
                    let h = t.with_timezone(&tz).hour() as usize;
                    if h < 24 {
                        item.values[h] += *d as f64;
                    }
                }
            }
        } else {
            let days = (end - start).num_days() + 1;
            if days <= constants::DAY_VIEW_THRESHOLD {
                for (cat_id, _) in &categories {
                    result.push(ColumnDataModel { app_id: None, category_id: Some(*cat_id), values: vec![0.0; days as usize] });
                }
                for item in &mut result {
                    if let Some(cid) = item.category_id {
                        result_map.insert(cid, item);
                    }
                }
                let days_usize = days as usize;
                for (cid, t, d) in &data {
                    if let Some(item) = result_map.get_mut(cid) {
                        let local_date = t.with_timezone(&tz).date_naive();
                        let offset = (local_date - start).num_days();
                        if offset >= 0 && (offset as usize) < days_usize {
                            item.values[offset as usize] += *d as f64;
                        }
                    }
                }
            } else {
                for (cat_id, _) in &categories {
                    result.push(ColumnDataModel { app_id: None, category_id: Some(*cat_id), values: vec![0.0; 12] });
                }
                for item in &mut result {
                    if let Some(cid) = item.category_id {
                        result_map.insert(cid, item);
                    }
                }
                let target_year = start.year();
                for (cid, t, d) in &data {
                    if let Some(item) = result_map.get_mut(cid) {
                        let local = t.with_timezone(&tz);
                        if local.year() == target_year {
                            let m = (local.month() - 1) as usize;
                            if m < 12 {
                                item.values[m] += *d as f64;
                            }
                        }
                    }
                }
            }
        }
        Ok(result)
    }

    pub async fn get_browse_duration_total(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, tz_id: &str, config_service: &ConfigService) -> Result<i64, AppError> {
        debug!("get_browse_duration_total: start={} end={}", start, end);
        let excluded_site_ids = get_excluded_site_ids(pool, config_service).await;
        let has_excluded = !excluded_site_ids.is_empty();

        let start_date = NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_date = NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_date, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end_date, &parse_timezone(tz_id));

        let total: Option<i64> = if !has_excluded {
            sqlx::query_scalar("SELECT SUM(Duration) FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ?")
                .bind(utc_start).bind(utc_end).fetch_optional(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                "SELECT SUM(Duration) FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ? AND SiteId NOT IN ({})",
                placeholders
            );
            let mut query = sqlx::query_scalar::<_, i64>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_optional(pool).await?
        };
        Ok(total.unwrap_or(0))
    }

    pub async fn get_browse_sites_total(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, tz_id: &str, config_service: &ConfigService) -> Result<i64, AppError> {
        debug!("get_browse_sites_total: start={} end={}", start, end);
        let excluded_site_ids = get_excluded_site_ids(pool, config_service).await;
        let has_excluded = !excluded_site_ids.is_empty();

        let start_date = NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_date = NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_date, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end_date, &parse_timezone(tz_id));

        let count: (i64,) = if !has_excluded {
            sqlx::query_as("SELECT COUNT(DISTINCT SiteId) FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ?")
                .bind(utc_start).bind(utc_end).fetch_one(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                "SELECT COUNT(DISTINCT SiteId) FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ? AND SiteId NOT IN ({})",
                placeholders
            );
            let mut query = sqlx::query_as::<_, (i64,)>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_one(pool).await?
        };
        Ok(count.0)
    }

    pub async fn get_browse_pages_total(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, tz_id: &str, config_service: &ConfigService) -> Result<i64, AppError> {
        debug!("get_browse_pages_total: start={} end={}", start, end);
        let excluded_site_ids = get_excluded_site_ids(pool, config_service).await;
        let has_excluded = !excluded_site_ids.is_empty();

        let start_date = NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_date = NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_date, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end_date, &parse_timezone(tz_id));

        let count: (i64,) = if !has_excluded {
            sqlx::query_as("SELECT COUNT(DISTINCT UrlId) FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ?")
                .bind(utc_start).bind(utc_end).fetch_one(pool).await?
        } else {
            let placeholders = excluded_site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
            let sql = format!(
                "SELECT COUNT(DISTINCT UrlId) FROM WebBrowseLogModels WHERE LogTime >= ? AND LogTime <= ? AND SiteId NOT IN ({})",
                placeholders
            );
            let mut query = sqlx::query_as::<_, (i64,)>(&sql)
                .bind(utc_start).bind(utc_end);
            for id in &excluded_site_ids {
                query = query.bind(*id);
            }
            query.fetch_one(pool).await?
        };
        Ok(count.0)
    }

    #[allow(non_snake_case)]
    pub async fn get_browse_log_list(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, site_id: i64, tz_id: &str, config_service: &ConfigService) -> Result<Vec<WebBrowseLogModel>, AppError> {
        debug!("get_browse_log_list: start={} end={} site_id={}", start, end, site_id);
        let start_date = NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_date = NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_date, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end_date, &parse_timezone(tz_id));

        let mut query = String::from(
            r#"SELECT wbl.ID, wbl.UrlId, wbl.LogTime, wbl.Duration, wbl.SiteId,
            ws.Domain, ws.Title AS WsTitle, ws.IconFile AS WsIconFile, ws.CategoryID AS WsCategoryID,
            wu.Url, wu.Title AS WuTitle, wu.IconFile AS WuIconFile
            FROM WebBrowseLogModels wbl
            JOIN WebUrlModels wu ON wbl.UrlId = wu.ID
            JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
            WHERE wbl.LogTime >= ? AND wbl.LogTime <= ?"#
        );
        if site_id > 0 {
            query.push_str(" AND wbl.SiteId = ?");
        }
        query.push_str(" ORDER BY wbl.LogTime DESC");

        #[derive(sqlx::FromRow)]
        #[allow(non_snake_case)]
        struct BrowseLogRow {
            ID: i64,
            UrlId: i64,
            LogTime: DateTime<Utc>,
            Duration: i64,
            SiteId: i64,
            Domain: String,
            WsTitle: Option<String>,
            WsIconFile: Option<String>,
            WsCategoryID: i64,
            Url: String,
            WuTitle: Option<String>,
            WuIconFile: Option<String>,
        }

        let mut sql_query = sqlx::query_as::<_, BrowseLogRow>(&query)
            .bind(utc_start)
            .bind(utc_end);
        if site_id > 0 {
            sql_query = sql_query.bind(site_id);
        }
        let rows = sql_query.fetch_all(pool).await?;

        let web_categories = Self::get_web_site_categories(pool).await?;
        let category_map: HashMap<i64, WebSiteCategoryModel> = web_categories
            .into_iter()
            .map(|c| (c.id, c))
            .collect();

        let domains: Vec<&str> = rows.iter().map(|r| r.Domain.as_str()).collect();
        let excluded = config_service.get_excluded_domains(&domains).await;
        let excluded_set: std::collections::HashSet<String> = excluded.into_iter().collect();

        let mut result = Vec::new();
        for row in rows {
            if excluded_set.contains(&row.Domain) {
                continue;
            }
            let category = category_map.get(&row.WsCategoryID).cloned();
            result.push(WebBrowseLogModel {
                id: row.ID,
                url_id: row.UrlId,
                log_time: row.LogTime,
                duration: row.Duration,
                site_id: row.SiteId,
                site: Some(WebSiteModel {
                    id: row.SiteId,
                    title: row.WsTitle,
                    domain: Some(row.Domain),
                    alias: None,
                    category_id: row.WsCategoryID,
                    icon_file: row.WsIconFile,
                    duration: 0,
                    category: category.clone(),
                }),
                url: Some(WebUrlModel {
                    id: row.UrlId,
                    title: row.WuTitle,
                    url: Some(row.Url),
                    icon_file: row.WuIconFile,
                }),
            });
        }
        Ok(result)
    }

    pub async fn get_web_site_log_list(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, tz_id: &str, config_service: &ConfigService) -> Result<Vec<WebSiteModel>, AppError> {
        debug!("get_web_site_log_list: start={} end={}", start, end);
        let start_date = NaiveDateTime::new(start.date(), chrono::NaiveTime::from_hms_opt(0,0,0).unwrap());
        let end_date = NaiveDateTime::new(end.date(), chrono::NaiveTime::from_hms_opt(23,59,59).unwrap());
        let utc_start = tz_naive_to_utc(start_date, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end_date, &parse_timezone(tz_id));
        let rows: Vec<(i64, Option<String>, Option<String>, Option<String>, Option<String>, i64, i64)> = sqlx::query_as(
            r#"SELECT wbl.SiteId, MAX(ws.Title), MAX(ws.Domain), MAX(ws.IconFile), MAX(ws.Alias), COALESCE(MAX(ws.CategoryID), 0), SUM(wbl.Duration) as duration
            FROM WebBrowseLogModels wbl
            LEFT JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
            WHERE wbl.LogTime >= ? AND wbl.LogTime <= ?
            GROUP BY wbl.SiteId"#
        ).bind(utc_start).bind(utc_end).fetch_all(pool).await?;

        let web_categories = Self::get_web_site_categories(pool).await?;
        let category_map: HashMap<i64, WebSiteCategoryModel> = web_categories
            .into_iter()
            .map(|c| (c.id, c))
            .collect();

        let domains: Vec<&str> = rows.iter().filter_map(|r| r.2.as_deref()).collect();
        let excluded = config_service.get_excluded_domains(&domains).await;
        let excluded_set: std::collections::HashSet<String> = excluded.into_iter().collect();

        let mut result = Vec::new();
        for (site_id, title, domain, icon_file, alias, category_id, duration) in rows {
            if domain.as_ref().map_or(false, |d| excluded_set.contains(d)) {
                continue;
            }
            result.push(WebSiteModel {
                id: site_id,
                title,
                domain: Some(domain.unwrap_or_default()),
                alias,
                category_id,
                icon_file,
                duration,
                category: category_map.get(&category_id).cloned(),
            });
        }
        Ok(result)
    }

    pub async fn get_web_export_data(pool: &SqlitePool, start: NaiveDateTime, end: NaiveDateTime, tz_id: &str, config_service: &ConfigService) -> Result<crate::models::web::WebExportDataResult, AppError> {
        info!("get_web_export_data: start={} end={}", start, end);
        const MAX_EXPORT_DAYS: i64 = 366;
        let days = (end.date() - start.date()).num_days() + 1;
        if days > MAX_EXPORT_DAYS {
            return Err(AppError::Business(format!("导出范围不能超过 {} 天", MAX_EXPORT_DAYS)));
        }
        let utc_start = tz_naive_to_utc(start, &parse_timezone(tz_id));
        let utc_end = tz_naive_to_utc(end, &parse_timezone(tz_id));

        #[derive(sqlx::FromRow)]
        struct ExportLogRow {
            id: i64,
            url_id: i64,
            log_time: DateTime<Utc>,
            duration: i64,
            site_id: i64,
            domain: String,
            ws_title: Option<String>,
            ws_icon_file: Option<String>,
            ws_category_id: i64,
            wu_url: String,
            wu_title: Option<String>,
            wu_icon_file: Option<String>,
            wsc_name: Option<String>,
            wsc_color: Option<String>,
        }

        let rows: Vec<ExportLogRow> = sqlx::query_as(
            r#"
            SELECT
                wbl.ID as id, wbl.UrlId as url_id, wbl.LogTime as log_time, wbl.Duration as duration, wbl.SiteId as site_id,
                ws.Domain as domain, ws.Title as ws_title, ws.IconFile as ws_icon_file, ws.CategoryID as ws_category_id,
                wu.Url as wu_url, wu.Title as wu_title, wu.IconFile as wu_icon_file,
                wsc.Name as wsc_name, wsc.Color as wsc_color
            FROM WebBrowseLogModels wbl
            JOIN WebUrlModels wu ON wbl.UrlId = wu.ID
            JOIN WebSiteModels ws ON wbl.SiteId = ws.ID
            LEFT JOIN WebSiteCategoryModels wsc ON ws.CategoryID = wsc.ID
            WHERE wbl.LogTime >= ? AND wbl.LogTime <= ?
            ORDER BY wbl.LogTime DESC
            "#
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let domains: Vec<&str> = rows.iter().map(|r| r.domain.as_str()).collect();
        let excluded = config_service.get_excluded_domains(&domains).await;
        let excluded_set: std::collections::HashSet<String> = excluded.into_iter().collect();

        let mut logs = Vec::new();
        for row in rows {
            if excluded_set.contains(&row.domain) {
                continue;
            }
            logs.push(WebBrowseLogModel {
            id: row.id,
            url_id: row.url_id,
            log_time: row.log_time,
            duration: row.duration,
            site_id: row.site_id,
            site: Some(WebSiteModel {
                id: row.site_id,
                title: row.ws_title,
                domain: Some(row.domain),
                alias: None,
                category_id: row.ws_category_id,
                icon_file: row.ws_icon_file,
                duration: 0,
                category: row.wsc_name.map(|name| WebSiteCategoryModel {
                    id: row.ws_category_id,
                    name,
                    icon_file: None,
                    color: row.wsc_color,
                    is_url_match: false,
                    url_patterns: None,
                    is_system: false,
                }),
            }),
            url: Some(WebUrlModel {
                id: row.url_id,
                title: row.wu_title,
                url: Some(row.wu_url),
                icon_file: row.wu_icon_file,
            }),
        });
        }

        Ok(crate::models::web::WebExportDataResult { logs })
    }

    pub async fn apply_url_match(pool: &SqlitePool, patterns: Option<Vec<String>>) -> Result<usize, AppError> {
        let rules = if let Some(ref p) = patterns {
            if p.is_empty() {
                return Ok(0);
            }
            let categories = Self::get_web_site_categories(pool).await?;
            build_url_match_rules_from_categories_with_patterns(&categories, p)
        } else {
            Self::load_url_match_rules(pool).await?
        };

        if rules.is_empty() {
            return Ok(0);
        }

        let sites: Vec<(i64, String, i64)> = sqlx::query_as(
            "SELECT ID, Domain, CategoryID FROM WebSiteModels WHERE Domain IS NOT NULL"
        )
        .fetch_all(pool)
        .await?;

        let mut to_update: Vec<(i64, i64)> = Vec::new();
        let system_category_id: i64 = sqlx::query_scalar(
            "SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1"
        )
        .fetch_optional(pool)
        .await?
        .unwrap_or(0);

        for (site_id, domain, current_cat_id) in &sites {
            if *current_cat_id == system_category_id {
                if let Some(cat_id) = match_url_against_rules(&rules, domain) {
                    to_update.push((*site_id, cat_id));
                }
            }
        }

        if to_update.is_empty() {
            return Ok(0);
        }

        let mut tx = pool.begin().await?;
        let mut updated = 0usize;
        for (site_id, category_id) in &to_update {
            let result = sqlx::query(
                "UPDATE WebSiteModels SET CategoryID = ? WHERE ID = ? AND CategoryID != ?"
            )
            .bind(category_id)
            .bind(site_id)
            .bind(category_id)
            .execute(&mut *tx)
            .await?;
            if result.rows_affected() > 0 {
                updated += 1;
            }
        }
        tx.commit().await?;

        if updated > 0 {
            info!("URL match applied: {} sites re-categorized", updated);
        }

        Ok(updated)
    }

    async fn load_url_match_rules(pool: &SqlitePool) -> Result<Vec<CompiledUrlMatchRule>, AppError> {
        {
            let cache = url_match_cache().read().await;
            if let Some(cached) = cache.as_ref() {
                if !cached.is_empty() {
                    return Ok(cached.clone());
                }
            }
        }

        let categories = Self::get_web_site_categories(pool).await?;
        let rules = build_url_match_rules_from_categories(&categories);

        let mut cache = url_match_cache().write().await;
        *cache = Some(rules.clone());

        Ok(rules)
    }

    pub async fn warmup_url_match_cache(pool: &SqlitePool) -> Result<usize, AppError> {
        let rules = Self::load_url_match_rules(pool).await?;
        let _ = load_system_category_id(pool).await;
        Ok(rules.len())
    }

}

fn extract_domain(url: &str) -> String {
    url.trim_start_matches("http://")
        .trim_start_matches("https://")
        .split('/')
        .next()
        .unwrap_or(url)
        .split(':')
        .next()
        .unwrap_or(url)
        .to_string()
}

async fn save_icon(icon_url: &str, favicons_dir: &Path, site_id: i64, url_id: i64, pool: &SqlitePool) -> Result<(), AppError> {
    if icon_url.starts_with("data:") {
        return Ok(());
    }
    if !icon_url.starts_with("http://") && !icon_url.starts_with("https://") {
        return Ok(());
    }

    let png_filename = format!("site_{}.png", site_id);
    let png_filepath = favicons_dir.join(&png_filename);
    if png_filepath.exists() {
        tracing::debug!("Icon already exists for site {}, skipping download", site_id);
        return Ok(());
    }

    let is_svg_by_url = icon_url.to_lowercase().ends_with(".svg");

    let ext = std::path::Path::new(icon_url)
        .extension()
        .and_then(|e| e.to_str())
        .unwrap_or("ico");

    let mut filename = format!("site_{}.{}", site_id, ext);
    let mut relative_path = format!("WebFavicons/{}", filename);
    let mut filepath = favicons_dir.join(&filename);

    tokio::fs::create_dir_all(favicons_dir).await?;

    let icon_url = icon_url.to_string();
    let bytes = match tokio::time::timeout(
        std::time::Duration::from_secs(constants::ICON_DOWNLOAD_TIMEOUT_SECS),
        tokio::task::spawn_blocking(move || {
            attohttpc::get(&icon_url).send().and_then(|r| r.bytes())
        })
    ).await {
        Ok(Ok(Ok(bytes))) => bytes,
        Ok(Ok(Err(e))) => {
            tracing::warn!("Failed to download icon: {}", e);
            return Ok(());
        }
        Ok(Err(e)) => {
            tracing::warn!("Icon download task failed: {}", e);
            return Ok(());
        }
        Err(_) => {
            tracing::warn!("Icon download timed out");
            return Ok(());
        }
    };

    // 检测并转换 SVG 为 PNG，确保客户端兼容性
    let bytes = if is_svg_by_url || is_svg_data(&bytes) {
        match tokio::task::spawn_blocking(move || convert_svg_to_png(&bytes)).await {
            Ok(Ok(png_bytes)) => {
                filename = format!("site_{}.png", site_id);
                relative_path = format!("WebFavicons/{}", filename);
                filepath = favicons_dir.join(&filename);
                png_bytes
            }
            Ok(Err(e)) => {
                tracing::warn!("Failed to convert SVG to PNG: {}", e);
                return Ok(());
            }
            Err(e) => {
                tracing::warn!("SVG conversion task failed: {}", e);
                return Ok(());
            }
        }
    } else {
        bytes
    };

    tokio::fs::write(&filepath, bytes).await?;

    sqlx::query("UPDATE WebSiteModels SET IconFile = ? WHERE ID = ?")
        .bind(&relative_path).bind(site_id).execute(pool).await?;
    sqlx::query("UPDATE WebUrlModels SET IconFile = ? WHERE ID = ?")
        .bind(&relative_path).bind(url_id).execute(pool).await?;

    Ok(())
}

fn is_svg_data(bytes: &[u8]) -> bool {
    let prefix_len = bytes.len().min(constants::SVG_PREFIX_CHECK_LEN);
    let prefix = &bytes[..prefix_len];
    if let Ok(text) = std::str::from_utf8(prefix) {
        let trimmed = text.trim_start();
        trimmed.starts_with("<svg")
            || (trimmed.starts_with("<?xml") && text.to_lowercase().contains("<svg"))
    } else {
        false
    }
}

fn convert_svg_to_png(svg_bytes: &[u8]) -> Result<Vec<u8>, AppError> {
    let tree = resvg::usvg::Tree::from_data(svg_bytes, &resvg::usvg::Options::default())
        .map_err(|e| AppError::Internal(format!("SVG parse error: {}", e)))?;

    let size = tree.size();
    let width = size.width().ceil() as u32;
    let height = size.height().ceil() as u32;

    if width == 0 || height == 0 {
        tracing::warn!("SVG has zero dimensions ({}x{}), skipping", width, height);
        return Err(AppError::Internal("SVG has zero dimensions".to_string()));
    }

    // 限制最大尺寸，防止恶意/超大 SVG 消耗过多内存
    let max_size = constants::MAX_ICON_SIZE_PX;
    let (width, height) = if width > max_size || height > max_size {
        let scale = max_size as f32 / width.max(height) as f32;
        ((width as f32 * scale).ceil() as u32, (height as f32 * scale).ceil() as u32)
    } else {
        (width.max(1), height.max(1))
    };

    let mut pixmap = resvg::tiny_skia::Pixmap::new(width, height)
        .ok_or_else(|| AppError::Internal("Failed to create pixmap".to_string()))?;

    let transform = resvg::tiny_skia::Transform::from_scale(
        width as f32 / size.width(),
        height as f32 / size.height(),
    );

    resvg::render(&tree, transform, &mut pixmap.as_mut());

    pixmap.encode_png()
        .map_err(|e| AppError::Internal(format!("PNG encode error: {}", e)))
}

/// 从 URL 字符串提取 scheme 和 domain
fn extract_domain_scheme(url_str: &str) -> Option<(String, String)> {
    let stripped = url_str.strip_prefix("https://")
        .or_else(|| url_str.strip_prefix("http://"))?;
    let domain = stripped.split('/').next()?;
    let scheme = if url_str.starts_with("https") {
        "https".to_string()
    } else {
        "http".to_string()
    };
    Some((scheme, domain.to_string()))
}

/// 清洗 HTML title，提取核心品牌名（去掉常见分隔符后的标语后缀）
fn clean_title(title: &str) -> Option<String> {
    let title = title.trim();
    if title.is_empty() {
        return None;
    }

    let separators = [" - ", " | ", " — ", " – ", " _ ", " · ", " • "];
    let mut result = title;
    for sep in &separators {
        if let Some(idx) = result.find(sep) {
            result = &result[..idx];
        }
    }

    let result = result.trim();
    if result.is_empty() {
        None
    } else {
        Some(result.to_string())
    }
}

/// 提取站点显示名称：直接从域名推断
fn extract_site_name(domain: &str, _raw_title: Option<&str>) -> String {
    infer_name_from_domain(domain)
}

/// 从域名推断站点名称：取主品牌名，如有有意义的子域名则附加在后
fn infer_name_from_domain(domain: &str) -> String {
    let lower = domain.to_lowercase();

    let without_prefix = lower.trim_start_matches("www.");

    let parts: Vec<&str> = without_prefix.split('.').collect();
    if parts.len() >= 2 {
        let brand = parts[parts.len() - 2];
        if brand.len() > 1 {
            let brand_name = capitalize_first(brand);

            // 如有额外子域名（非品牌部分），将其作为后缀，排除常见无意义前缀
            if parts.len() > 2 {
                let subdomain = parts[0];
                let meaningless = ["m", "app", "chat", "web", "my", "account", "login", "mail"];
                if !meaningless.contains(&subdomain) && subdomain != brand {
                    return format!("{} {}", brand_name, capitalize_first(subdomain));
                }
            }

            return brand_name;
        }
    }

    domain.to_string()
}

fn capitalize_first(s: &str) -> String {
    let mut chars = s.chars();
    match chars.next() {
        Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
        None => s.to_string(),
    }
}

/// 过滤浏览器内置页面（新标签页、空白页等）
fn is_browser_internal_page(url: &str, title: Option<&str>) -> bool {
    let url_lower = url.to_lowercase();

    // 内置协议前缀
    let internal_prefixes = [
        "chrome://",
        "edge://",
        "browser://",
        "firefox://",
        "about:",
        "brave://",
        "opera://",
        "vivaldi://",
        "file://",
        "data:",
    ];
    for prefix in &internal_prefixes {
        if url_lower.starts_with(prefix) {
            return true;
        }
    }

    // 已知的新标签页/空白页标题
    if let Some(t) = title {
        let t_lower = t.trim().to_lowercase();
        let ignore_titles = [
            "新标签页",
            "新标签",
            "new tab",
            "newtab",
            "about:blank",
            "空白页",
            "blank page",
        ];
        for ignore in &ignore_titles {
            if t_lower == *ignore {
                return true;
            }
        }
    }

    false
}

fn build_url_match_rules_from_categories(categories: &[WebSiteCategoryModel]) -> Vec<CompiledUrlMatchRule> {
    const MAX_PATTERN_LEN: usize = 256;
    const MAX_PATTERNS_PER_CATEGORY: usize = 20;

    categories
        .iter()
        .filter(|c| c.is_url_match && !c.is_system)
        .filter_map(|c| {
            let patterns: Vec<String> = c.url_patterns.as_ref()
                .and_then(|p| serde_json::from_str::<Vec<String>>(p).ok())
                .unwrap_or_default();

            if patterns.is_empty() {
                return None;
            }

            let regex_patterns: Vec<String> = patterns
                .iter()
                .map(|p| p.trim())
                .filter(|p| !p.is_empty() && p.len() <= MAX_PATTERN_LEN)
                .take(MAX_PATTERNS_PER_CATEGORY)
                .map(|p| pattern_to_regex(p))
                .collect();

            if regex_patterns.is_empty() {
                return None;
            }

            match RegexSet::new(&regex_patterns) {
                Ok(set) => Some(CompiledUrlMatchRule {
                    category_id: c.id,
                    regex_set: Arc::new(set),
                }),
                Err(e) => {
                    warn!("Failed to compile URL regex set for category {}: {}", c.id, e);
                    None
                }
            }
        })
        .collect()
}

fn build_url_match_rules_from_categories_with_patterns(
    categories: &[WebSiteCategoryModel],
    filter_patterns: &[String],
) -> Vec<CompiledUrlMatchRule> {
    const MAX_PATTERN_LEN: usize = 256;

    let filter_set: std::collections::HashSet<String> = filter_patterns
        .iter()
        .map(|p| p.trim().to_lowercase())
        .collect();

    categories
        .iter()
        .filter(|c| c.is_url_match && !c.is_system)
        .filter_map(|c| {
            let all_patterns: Vec<String> = c.url_patterns.as_ref()
                .and_then(|p| serde_json::from_str::<Vec<String>>(p).ok())
                .unwrap_or_default();

            let matched_patterns: Vec<String> = all_patterns
                .into_iter()
                .map(|p| p.trim().to_string())
                .filter(|p| !p.is_empty() && p.len() <= MAX_PATTERN_LEN)
                .filter(|p| filter_set.contains(&p.to_lowercase()))
                .collect();

            if matched_patterns.is_empty() {
                return None;
            }

            let regex_patterns: Vec<String> = matched_patterns
                .iter()
                .map(|p| pattern_to_regex(p))
                .collect();

            match RegexSet::new(&regex_patterns) {
                Ok(set) => Some(CompiledUrlMatchRule {
                    category_id: c.id,
                    regex_set: Arc::new(set),
                }),
                Err(e) => {
                    warn!("Failed to compile URL regex set for category {}: {}", c.id, e);
                    None
                }
            }
        })
        .collect()
}

/// 判断是否为正则表达式（包含正则特殊元字符）
fn is_regex_pattern(pattern: &str) -> bool {
    // 正则元字符：^ $ [] () {} | + \ .
    // 注意：* 和 ? 在通配符中使用，不作为正则判断依据
    let regex_chars = ['^', '$', '[', ']', '(', ')', '{', '}', '|', '+', '\\'];
    pattern.chars().any(|c| regex_chars.contains(&c))
}

/// 将模式转换为正则表达式
/// - 包含正则元字符 → 当作正则表达式处理
/// - 包含 * 或 ? → 当作通配符转换
/// - 普通字符串 → 精确匹配
fn pattern_to_regex(pattern: &str) -> String {
    if is_regex_pattern(pattern) {
        // 已经是正则表达式，添加大小写不敏感和锚点
        format!("(?i)^({})$", pattern)
    } else if pattern.contains('*') || pattern.contains('?') {
        // 通配符模式，转换
        wildcard_to_regex(pattern)
    } else {
        // 普通字符串，精确匹配
        format!("(?i)^{}$", regex::escape(pattern))
    }
}

fn wildcard_to_regex(pattern: &str) -> String {
    let mut result = String::with_capacity(pattern.len() * 2);
    result.push_str("(?i)^");

    for c in pattern.chars() {
        match c {
            '*' => result.push_str(".*"),
            '?' => result.push('.'),
            '.' | '^' | '$' | '+' | '[' | ']' | '(' | ')' | '{' | '}' | '\\' | '|' => {
                result.push('\\');
                result.push(c);
            }
            _ => result.push(c),
        }
    }

    result.push('$');
    result
}

fn match_url_against_rules(rules: &[CompiledUrlMatchRule], domain: &str) -> Option<i64> {
    const MAX_DOMAIN_LEN: usize = 255;

    if domain.is_empty() || domain.len() > MAX_DOMAIN_LEN {
        return None;
    }

    let domain_lower = domain.to_lowercase();
    for rule in rules {
        if rule.regex_set.is_match(&domain_lower) {
            return Some(rule.category_id);
        }
    }
    None
}

async fn match_url_for_new_site(domain: &str) -> Option<i64> {
    if domain.is_empty() {
        return None;
    }

    let cache = url_match_cache().read().await;
    if let Some(rules) = cache.as_ref() {
        if !rules.is_empty() {
            return match_url_against_rules(rules, domain);
        }
    }

    None
}

async fn get_system_category_id_cached() -> i64 {
    let cache = system_category_id_cache().read().await;
    cache.as_ref().copied().unwrap_or(0)
}

async fn load_system_category_id(pool: &SqlitePool) -> Result<i64, AppError> {
    {
        let cache = system_category_id_cache().read().await;
        if let Some(id) = cache.as_ref() {
            return Ok(*id);
        }
    }

    let id: i64 = sqlx::query_scalar("SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1")
        .fetch_optional(pool)
        .await?
        .unwrap_or(0);

    let mut cache = system_category_id_cache().write().await;
    *cache = Some(id);

    Ok(id)
}
