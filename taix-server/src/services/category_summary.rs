use chrono::{DateTime, NaiveDate, NaiveDateTime, Timelike, Utc};
use chrono_tz::Tz;
use sqlx::SqlitePool;
use std::collections::HashMap;
use tracing::debug;

use crate::error::AppError;
use crate::models::app::{AppJoinCols, AppModel, APP_JOIN_COLS_SQL};
use crate::models::category_summary::{
    CategoryMemberModel, CategorySummaryModel, DailyPointModel,
};
use crate::models::web::{WebSiteJoinCols, WebSiteModel, WEB_SITE_JOIN_COLS_SQL};
use crate::services::config::ConfigService;
use crate::utils::{excluded_clause, parse_timezone, tz_date_to_utc_range, tz_naive_to_utc};
use crate::bind_excluded;

pub struct CategorySummaryService;

impl CategorySummaryService {
    pub async fn get_app_category_summary(
        pool: &SqlitePool,
        category_id: i64,
        start: NaiveDate,
        end: NaiveDate,
        prev_start: Option<NaiveDate>,
        prev_end: Option<NaiveDate>,
        tz_id: &str,
        config_service: &ConfigService,
    ) -> Result<CategorySummaryModel, AppError> {
        debug!(
            "get_app_category_summary: cat={} start={} end={}",
            category_id, start, end
        );

        let category_name: Option<String> =
            sqlx::query_scalar("SELECT Name FROM CategoryModels WHERE ID = ?")
                .bind(category_id)
                .fetch_optional(pool)
                .await?;
        let category_name = category_name.unwrap_or_else(|| String::from("?"));

        let tz = parse_timezone(tz_id);
        let utc_start = tz_date_to_utc_range(start, &tz).0;
        let utc_end = tz_date_to_utc_range(end, &tz).1;

        let excluded = config_service.get_excluded_app_id_set(pool).await;
        let excluded_vec: Vec<i64> = excluded.into_iter().collect();

        // 一次 SQL 拉取后在内存聚合
        let sql = format!(
            r#"
            SELECT h.DataTime, h.Time
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE a.CategoryID = ?
              AND h.DataTime >= ? AND h.DataTime < ?
              AND h.AppModelID != 0{}
            "#,
            excluded_clause("h.AppModelID", &excluded_vec)
        );
        let rows: Vec<(DateTime<Utc>, i64)> = bind_excluded!(
            sqlx::query_as::<_, (DateTime<Utc>, i64)>(&sql)
                .bind(category_id)
                .bind(utc_start)
                .bind(utc_end),
            &excluded_vec
        )
        .fetch_all(pool)
        .await?;

        // 环比总时长，纯 SUM 标量查询
        let previous_total_seconds = match (prev_start, prev_end) {
            (Some(ps), Some(pe)) if ps <= pe => {
                sum_app_range_total(pool, category_id, ps, pe, &tz, &excluded_vec).await
            }
            _ => 0,
        };

        // 按本地日期分桶
        let mut daily: HashMap<NaiveDate, i64> = HashMap::new();
        // 按本地时区小时分桶
        let mut hourly: [i64; 24] = [0; 24];
        let mut total_seconds: i64 = 0;

        for (utc_dt, secs) in &rows {
            let local = utc_dt.with_timezone(&tz);
            let local_date = local.date_naive();
            if local_date < start || local_date > end {
                continue;
            }
            *daily.entry(local_date).or_insert(0) += secs;
            let h = local.time().hour() as usize;
            hourly[h] += secs;
            total_seconds += secs;
        }

        let active_days = daily.values().filter(|&&v| v > 0).count() as i64;
        let average_daily_seconds = if active_days > 0 {
            total_seconds / active_days
        } else {
            0
        };

        let daily_trend = build_daily_trend(start, end, &daily);
        let hourly_distribution = hourly.to_vec();

        Ok(CategorySummaryModel {
            category_id,
            category_name,
            total_seconds,
            previous_total_seconds,
            active_days,
            average_daily_seconds,
            daily_trend,
            hourly_distribution,
            members: None,
        })
    }

    // 网站分类排除用 site 维度，不复用 App 的 excluded 列表
    pub async fn get_web_category_summary(
        pool: &SqlitePool,
        category_id: i64,
        start: NaiveDate,
        end: NaiveDate,
        prev_start: Option<NaiveDate>,
        prev_end: Option<NaiveDate>,
        tz_id: &str,
        _config_service: &ConfigService,
    ) -> Result<CategorySummaryModel, AppError> {
        debug!(
            "get_web_category_summary: cat={} start={} end={}",
            category_id, start, end
        );

        let category_name: Option<String> =
            sqlx::query_scalar("SELECT Name FROM WebSiteCategoryModels WHERE ID = ?")
                .bind(category_id)
                .fetch_optional(pool)
                .await?;
        let category_name = category_name.unwrap_or_else(|| String::from("?"));

        let tz = parse_timezone(tz_id);
        let start_dt = chrono::NaiveDateTime::new(start, chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap());
        let end_dt = chrono::NaiveDateTime::new(end + chrono::Duration::days(1), chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap());
        let utc_start = tz_naive_to_utc(start_dt, &tz);
        let utc_end = tz_naive_to_utc(end_dt, &tz);

        let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT l.LogTime, l.Duration
            FROM WebBrowseLogModels l
            JOIN WebSiteModels s ON l.SiteId = s.ID
            WHERE s.CategoryID = ?
              AND l.LogTime >= ? AND l.LogTime < ?
            "#,
        )
        .bind(category_id)
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        // 环比总时长，纯 SUM 标量查询
        let previous_total_seconds = match (prev_start, prev_end) {
            (Some(ps), Some(pe)) if ps <= pe => {
                sum_web_range_total(pool, category_id, ps, pe, &tz).await
            }
            _ => 0,
        };

        let mut daily: HashMap<NaiveDate, i64> = HashMap::new();
        let mut hourly: [i64; 24] = [0; 24];
        let mut total_seconds: i64 = 0;

        for (utc_dt, secs) in &rows {
            let local = utc_dt.with_timezone(&tz);
            let local_date = local.date_naive();
            if local_date < start || local_date > end {
                continue;
            }
            *daily.entry(local_date).or_insert(0) += secs;
            let h = local.time().hour() as usize;
            hourly[h] += secs;
            total_seconds += secs;
        }

        let active_days = daily.values().filter(|&&v| v > 0).count() as i64;
        let average_daily_seconds = if active_days > 0 {
            total_seconds / active_days
        } else {
            0
        };

        let daily_trend = build_daily_trend(start, end, &daily);
        let hourly_distribution = hourly.to_vec();

        Ok(CategorySummaryModel {
            category_id,
            category_name,
            total_seconds,
            previous_total_seconds,
            active_days,
            average_daily_seconds,
            daily_trend,
            hourly_distribution,
            members: None,
        })
    }

    /// 区间成员明细，用于柱状图点击列展开；with_models 时内嵌完整应用模型
    pub async fn get_app_category_members(
        pool: &SqlitePool,
        category_id: i64,
        start: NaiveDateTime,
        end: NaiveDateTime,
        tz_id: &str,
        config_service: &ConfigService,
        with_models: bool,
    ) -> Result<Vec<CategoryMemberModel>, AppError> {
        let tz = parse_timezone(tz_id);
        let utc_start = tz_naive_to_utc(start, &tz);
        let utc_end = tz_naive_to_utc(end, &tz);

        let excluded = config_service.get_excluded_app_id_set(pool).await;
        let excluded_vec: Vec<i64> = excluded.into_iter().collect();

        let sql = format!(
            r#"
            SELECT a.ID, h.Time
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE a.CategoryID = ?
              AND h.DataTime >= ? AND h.DataTime < ?
              AND h.AppModelID != 0{}
            "#,
            excluded_clause("h.AppModelID", &excluded_vec)
        );
        let rows: Vec<(i64, i64)> = bind_excluded!(
            sqlx::query_as::<_, (i64, i64)>(&sql)
                .bind(category_id)
                .bind(utc_start)
                .bind(utc_end),
            &excluded_vec
        )
        .fetch_all(pool)
        .await?;

        let mut by_app: HashMap<i64, i64> = HashMap::new();
        for (app_id, secs) in &rows {
            *by_app.entry(*app_id).or_insert(0) += secs;
        }

        let mut members = build_top_items_for_apps(pool, &by_app).await?;
        if with_models {
            attach_app_models(pool, &mut members).await?;
        }
        Ok(members)
    }

    /// 网站分类区间成员明细；with_models 时内嵌完整站点模型
    pub async fn get_web_category_members(
        pool: &SqlitePool,
        category_id: i64,
        start: NaiveDateTime,
        end: NaiveDateTime,
        tz_id: &str,
        with_models: bool,
    ) -> Result<Vec<CategoryMemberModel>, AppError> {
        let tz = parse_timezone(tz_id);
        let utc_start = tz_naive_to_utc(start, &tz);
        let utc_end = tz_naive_to_utc(end, &tz);

        let rows: Vec<(i64, i64)> = sqlx::query_as(
            r#"
            SELECT s.ID, l.Duration
            FROM WebBrowseLogModels l
            JOIN WebSiteModels s ON l.SiteId = s.ID
            WHERE s.CategoryID = ?
              AND l.LogTime >= ? AND l.LogTime < ?
            "#,
        )
        .bind(category_id)
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let mut by_site: HashMap<i64, i64> = HashMap::new();
        for (site_id, secs) in &rows {
            *by_site.entry(*site_id).or_insert(0) += secs;
        }

        let mut members = build_top_items_for_sites(pool, &by_site).await?;
        if with_models {
            attach_site_models(pool, &mut members).await?;
        }
        Ok(members)
    }
}

async fn attach_app_models(
    pool: &SqlitePool,
    members: &mut [CategoryMemberModel],
) -> Result<(), AppError> {
    let ids: Vec<i64> = members.iter().filter(|m| m.id > 0).map(|m| m.id).collect();
    if ids.is_empty() {
        return Ok(());
    }
    let sql = format!(
        "SELECT {cols} FROM AppModels a \
         LEFT JOIN CategoryModels c ON a.CategoryID = c.ID \
         WHERE a.ID IN ({})",
        crate::utils::in_clause(&ids),
        cols = APP_JOIN_COLS_SQL
    );
    let mut q = sqlx::query_as::<_, AppJoinCols>(&sql);
    for id in &ids {
        q = q.bind(*id);
    }
    let rows = q.fetch_all(pool).await?;
    let map: HashMap<i64, AppModel> = rows
        .into_iter()
        .filter_map(|r| r.to_app_model().map(|m| (m.id, m)))
        .collect();
    for m in members.iter_mut() {
        m.app = map.get(&m.id).cloned();
    }
    Ok(())
}

async fn attach_site_models(
    pool: &SqlitePool,
    members: &mut [CategoryMemberModel],
) -> Result<(), AppError> {
    let ids: Vec<i64> = members.iter().filter(|m| m.id > 0).map(|m| m.id).collect();
    if ids.is_empty() {
        return Ok(());
    }
    let sql = format!(
        "SELECT {cols} FROM WebSiteModels s \
         LEFT JOIN WebSiteCategoryModels c ON s.CategoryID = c.ID \
         WHERE s.ID IN ({})",
        crate::utils::in_clause(&ids),
        cols = WEB_SITE_JOIN_COLS_SQL
    );
    let mut q = sqlx::query_as::<_, WebSiteJoinCols>(&sql);
    for id in &ids {
        q = q.bind(*id);
    }
    let rows = q.fetch_all(pool).await?;
    let map: HashMap<i64, WebSiteModel> = rows
        .into_iter()
        .filter_map(|r| r.to_site_model().map(|m| (m.id, m)))
        .collect();
    for m in members.iter_mut() {
        m.site = map.get(&m.id).cloned();
    }
    Ok(())
}

fn build_daily_trend(
    start: NaiveDate,
    end: NaiveDate,
    daily: &HashMap<NaiveDate, i64>,
) -> Vec<DailyPointModel> {
    let days = (end - start).num_days();
    if days < 0 {
        return Vec::new();
    }
    let mut out = Vec::with_capacity((days + 1) as usize);
    let mut cur = start;
    while cur <= end {
        out.push(DailyPointModel {
            date: cur,
            seconds: daily.get(&cur).copied().unwrap_or(0),
        });
        match cur.succ_opt() {
            Some(d) => cur = d,
            None => break,
        }
    }
    out
}

async fn build_top_items_for_apps(
    pool: &SqlitePool,
    by_app: &HashMap<i64, i64>,
) -> Result<Vec<CategoryMemberModel>, AppError> {
    if by_app.is_empty() {
        return Ok(Vec::new());
    }

    let app_ids: Vec<i64> = by_app.keys().copied().collect();
    let placeholders = app_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
    let sql = format!(
        "SELECT ID, Name, Alias, IconFile FROM AppModels WHERE ID IN ({})",
        placeholders
    );
    let mut q = sqlx::query_as::<_, (i64, Option<String>, Option<String>, Option<String>)>(&sql);
    for id in &app_ids {
        q = q.bind(*id);
    }
    let rows = q.fetch_all(pool).await?;
    let meta_map: HashMap<i64, (String, Option<String>)> = rows
        .into_iter()
        .map(|(id, name, alias, icon)| {
            let display = alias
                .filter(|s| !s.is_empty())
                .or(name)
                .unwrap_or_else(|| String::from("?"));
            (id, (display, icon))
        })
        .collect();

    let mut sorted: Vec<(i64, i64)> = by_app.iter().map(|(k, v)| (*k, *v)).collect();
    sorted.sort_by(|a, b| b.1.cmp(&a.1));

    Ok(reduce_to_top_n(sorted, &meta_map))
}

async fn build_top_items_for_sites(
    pool: &SqlitePool,
    by_site: &HashMap<i64, i64>,
) -> Result<Vec<CategoryMemberModel>, AppError> {
    if by_site.is_empty() {
        return Ok(Vec::new());
    }

    let site_ids: Vec<i64> = by_site.keys().copied().collect();
    let placeholders = site_ids.iter().map(|_| "?").collect::<Vec<_>>().join(",");
    let sql = format!(
        "SELECT ID, Title, Domain, Alias, IconFile FROM WebSiteModels WHERE ID IN ({})",
        placeholders
    );
    let mut q = sqlx::query_as::<_, (
        i64,
        Option<String>,
        Option<String>,
        Option<String>,
        Option<String>,
    )>(&sql);
    for id in &site_ids {
        q = q.bind(*id);
    }
    let rows = q.fetch_all(pool).await?;
    let meta_map: HashMap<i64, (String, Option<String>)> = rows
        .into_iter()
        .map(|(id, title, domain, alias, icon)| {
            let display = alias
                .filter(|s| !s.is_empty())
                .or_else(|| title.filter(|s| !s.is_empty()))
                .or(domain)
                .unwrap_or_else(|| String::from("?"));
            (id, (display, icon))
        })
        .collect();

    let mut sorted: Vec<(i64, i64)> = by_site.iter().map(|(k, v)| (*k, *v)).collect();
    sorted.sort_by(|a, b| b.1.cmp(&a.1));

    Ok(reduce_to_top_n(sorted, &meta_map))
}

/// 全量返回成员，按秒降序
fn reduce_to_top_n(
    sorted: Vec<(i64, i64)>,
    meta: &HashMap<i64, (String, Option<String>)>,
) -> Vec<CategoryMemberModel> {
    sorted
        .into_iter()
        .map(|(id, secs)| {
            let (name, icon) = meta
                .get(&id)
                .cloned()
                .unwrap_or_else(|| (String::from("?"), None));
            CategoryMemberModel {
                id,
                name,
                icon_file: icon,
                seconds: secs,
                app: None,
                site: None,
            }
        })
        .collect()
}

/// App 分类区间总时长，纯 SUM 标量查询
async fn sum_app_range_total(
    pool: &SqlitePool,
    category_id: i64,
    start: NaiveDate,
    end: NaiveDate,
    tz: &Tz,
    excluded_vec: &[i64],
) -> i64 {
    let utc_start = tz_date_to_utc_range(start, tz).0;
    let utc_end = tz_date_to_utc_range(end, tz).1;

    let sql = format!(
        r#"
        SELECT COALESCE(SUM(h.Time), 0)
        FROM HoursLogModels h
        JOIN AppModels a ON h.AppModelID = a.ID
        WHERE a.CategoryID = ?
          AND h.DataTime >= ? AND h.DataTime < ?
          AND h.AppModelID != 0{}
        "#,
        excluded_clause("h.AppModelID", excluded_vec)
    );
    bind_excluded!(
        sqlx::query_scalar::<_, i64>(&sql)
            .bind(category_id)
            .bind(utc_start)
            .bind(utc_end),
        excluded_vec
    )
    .fetch_one(pool)
    .await
    .unwrap_or(0)
}

/// Web 分类区间总时长，纯 SUM 标量查询
async fn sum_web_range_total(
    pool: &SqlitePool,
    category_id: i64,
    start: NaiveDate,
    end: NaiveDate,
    tz: &Tz,
) -> i64 {
    let start_dt =
        chrono::NaiveDateTime::new(start, chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap());
    let end_dt =
        chrono::NaiveDateTime::new(end + chrono::Duration::days(1), chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap());
    let utc_start = tz_naive_to_utc(start_dt, tz);
    let utc_end = tz_naive_to_utc(end_dt, tz);

    let total: i64 = sqlx::query_scalar(
        r#"
        SELECT COALESCE(SUM(l.Duration), 0)
        FROM WebBrowseLogModels l
        JOIN WebSiteModels s ON l.SiteId = s.ID
        WHERE s.CategoryID = ?
          AND l.LogTime >= ? AND l.LogTime < ?
        "#,
    )
    .bind(category_id)
    .bind(utc_start)
    .bind(utc_end)
    .fetch_one(pool)
    .await
    .unwrap_or(0);
    total
}
