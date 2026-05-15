use chrono::{DateTime, Datelike, NaiveDate, NaiveDateTime, Timelike, Utc};
use sqlx::SqlitePool;
use std::collections::HashMap;
use tracing::{debug, info};

use crate::error::AppError;
use crate::models::app::{AppModel, AppModelRow};
use crate::models::category::CategoryModel;
use crate::services::category::CategoryService;
use crate::services::config::ConfigService;
use crate::models::log::{
    AppSessionModel, ColumnDataModel, DailyLogModel, ExportDataResult, HoursLogModel,
};
use crate::utils::{last_day_of_month, parse_timezone, tz_date_range_to_utc_date_range, tz_date_to_utc_range, tz_naive_to_utc};

pub struct DataService;

impl DataService {
    pub async fn get_date_range_log_list(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        take: i64,
        skip: i64,
        tz_id: &str,
        config_service: &ConfigService,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_date_range_log_list: start={} end={} take={} skip={}", start, end, take, skip);
        let limit = if take > 0 { take } else { -1 };
        let offset = if skip > 0 { skip } else { 0 };

        let tz = parse_timezone(tz_id);
        let days = (end - start).num_days() + 1;

        // 小范围（≤31天）：从 HoursLogModels 精确查询，避免 UTC 日期边界问题
        // 大范围（>31天）：从 DailyLogModels 查询，不后过滤，接受首尾边界轻微混叠
        let mut groups: HashMap<i64, (i64, NaiveDate)> = HashMap::new();
        if days <= 31 {
            let utc_start = tz_date_to_utc_range(start, &tz).0;
            let utc_end = tz_date_to_utc_range(end, &tz).1;

            let rows: Vec<(i64, i64, DateTime<Utc>)> = sqlx::query_as(
                r#"
                SELECT AppModelID, SUM(Time) as total, MAX(DataTime) as last_time
                FROM HoursLogModels
                WHERE DataTime >= ? AND DataTime < ? AND AppModelID != 0
                GROUP BY AppModelID
                "#,
            )
            .bind(utc_start)
            .bind(utc_end)
            .fetch_all(pool)
            .await?;

            for (app_id, time, last_time) in rows {
                let local_date = last_time.with_timezone(&tz).date_naive();
                groups.insert(app_id, (time, local_date));
            }

        } else {
            let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);

            let logs: Vec<DailyLogModel> = sqlx::query_as(
                "SELECT * FROM DailyLogModels WHERE Date >= ? AND Date <= ? AND AppModelID != 0"
            )
            .bind(utc_start)
            .bind(utc_end)
            .fetch_all(pool)
            .await?;

            for log in logs {
                let entry = groups.entry(log.app_model_id).or_insert((0, log.date));
                entry.0 += log.time;
                entry.1 = entry.1.max(log.date);
            }
        }

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let app_refs: Vec<(i64, &str, Option<&str>)> = apps.iter()
            .map(|a| (a.id, a.name.as_deref().unwrap_or(""), a.file.as_deref()))
            .collect();
        let excluded_ids = config_service.get_excluded_app_ids(&app_refs).await;
        let excluded_set: std::collections::HashSet<i64> = excluded_ids.into_iter().collect();
        let app_map: std::collections::HashMap<i64, AppModelRow> =
            apps.into_iter().map(|a| (a.id, a)).collect();

        // 过滤后排序分页
        let mut sorted_groups: Vec<_> = groups.into_iter()
            .filter(|(app_id, _)| !excluded_set.contains(app_id))
            .collect();
        sorted_groups.sort_by(|a, b| b.1.0.cmp(&a.1.0));
        let offset_usize = offset as usize;
        let limit_usize = if limit > 0 { limit as usize } else { usize::MAX };
        let sorted_groups: Vec<_> = sorted_groups.into_iter()
            .skip(offset_usize)
            .take(limit_usize)
            .collect();

        let mut result = Vec::new();
        for (app_id, (time, date)) in sorted_groups {
            let app_model = app_map.get(&app_id).cloned().map(|a| AppModel {
                id: a.id,
                name: a.name,
                alias: a.alias,
                description: a.description,
                file: a.file,
                category_id: a.category_id,
                icon_file: a.icon_file,
                total_time: a.total_time,
                category: None,
            });

            result.push(DailyLogModel {
                id: 0,
                date,
                app_model_id: app_id,
                time,
                app_model,
            });
        }

        Ok(result)
    }

    pub async fn get_process_month_log_list(
        pool: &SqlitePool,
        app_id: i64,
        month: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_process_month_log_list: app_id={} month={}", app_id, month);
        let tz = parse_timezone(tz_id);
        let month_start = NaiveDate::from_ymd_opt(month.year(), month.month(), 1).unwrap();
        let month_end_day = last_day_of_month(month.year(), month.month());
        let month_end = NaiveDate::from_ymd_opt(month.year(), month.month(), month_end_day).unwrap();
        let utc_start = tz_date_to_utc_range(month_start, &tz).0;
        let utc_end = tz_date_to_utc_range(month_end, &tz).1;

        // 从 HoursLogModels 查询并按本地日期精确聚合，避免 UTC 日期边界问题
        let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT DataTime, SUM(Time) as total
            FROM HoursLogModels
            WHERE AppModelID = ? AND DataTime >= ? AND DataTime < ?
            GROUP BY DataTime
            "#
        )
        .bind(app_id)
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let mut groups: HashMap<NaiveDate, i64> = HashMap::new();
        for (utc_dt, time) in rows {
            let local_date = utc_dt.with_timezone(&tz).date_naive();
            if local_date >= month_start && local_date <= month_end {
                *groups.entry(local_date).or_insert(0) += time;
            }
        }

        let mut result: Vec<DailyLogModel> = groups.into_iter()
            .map(|(date, time)| DailyLogModel {
                id: 0,
                date,
                app_model_id: app_id,
                time,
                app_model: None,
            })
            .collect();
        result.sort_by(|a, b| a.date.cmp(&b.date));
        Ok(result)
    }

    pub async fn clear_app_data(
        pool: &SqlitePool,
        app_id: i64,
        month: Option<NaiveDate>,
        tz_id: &str,
    ) -> Result<(), AppError> {
        info!("clear_app_data: app_id={} month={:?}", app_id, month);
        if let Some(month) = month {
            let tz = parse_timezone(tz_id);
            let month_start = NaiveDate::from_ymd_opt(month.year(), month.month(), 1).unwrap();
            let month_end_day = last_day_of_month(month.year(), month.month());
            let month_end = NaiveDate::from_ymd_opt(month.year(), month.month(), month_end_day).unwrap();
            let (utc_start, utc_end) = tz_date_range_to_utc_date_range(month_start, month_end, &tz);

            sqlx::query(
                "DELETE FROM DailyLogModels WHERE AppModelID = ? AND Date >= ? AND Date <= ?"
            )
            .bind(app_id)
            .bind(utc_start)
            .bind(utc_end)
            .execute(pool)
            .await?;

            // 按月删除 HoursLogModels：先把本地月份范围转 UTC，再删除
            let s = tz_date_to_utc_range(month_start, &tz);
            let e = tz_date_to_utc_range(month_end, &tz);
            let (utc_start, utc_end) = (s.0, e.1);

            sqlx::query(
                "DELETE FROM HoursLogModels WHERE AppModelID = ? AND DataTime >= ? AND DataTime <= ?"
            )
            .bind(app_id)
            .bind(utc_start)
            .bind(utc_end)
            .execute(pool)
            .await?;
        } else {
            sqlx::query("DELETE FROM DailyLogModels WHERE AppModelID = ?")
                .bind(app_id)
                .execute(pool)
                .await?;

            sqlx::query("DELETE FROM HoursLogModels WHERE AppModelID = ?")
                .bind(app_id)
                .execute(pool)
                .await?;
        }

        // 统一重算 TotalTime，避免按月清除后 TotalTime 与日志表不一致
        sqlx::query(
            "UPDATE AppModels SET TotalTime = (SELECT COALESCE(SUM(Time), 0) FROM DailyLogModels WHERE AppModelID = ?) WHERE ID = ?"
        )
        .bind(app_id)
        .bind(app_id)
        .execute(pool)
        .await?;

        Ok(())
    }

    pub async fn clear_range(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        tz_id: &str,
    ) -> Result<(), AppError> {
        info!("clear_range: start={} end={}", start, end);

        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);
        sqlx::query("DELETE FROM DailyLogModels WHERE Date >= ? AND Date <= ?")
            .bind(utc_start)
            .bind(utc_end)
            .execute(pool)
            .await?;

        let s = tz_date_to_utc_range(start, &tz);
        let e = tz_date_to_utc_range(end, &tz);
        let (utc_start, utc_end) = (s.0, e.1);
        sqlx::query("DELETE FROM HoursLogModels WHERE DataTime >= ? AND DataTime <= ?")
            .bind(utc_start)
            .bind(utc_end)
            .execute(pool)
            .await?;

        // 重算所有 App 的 TotalTime，避免按日期范围清除后总计虚高
        // 使用 JOIN + GROUP BY 替代 correlated subquery，提升大范围清除性能
        let totals: Vec<(i64, i64)> = sqlx::query_as(
            r#"
            SELECT a.ID, COALESCE(SUM(d.Time), 0)
            FROM AppModels a
            LEFT JOIN DailyLogModels d ON a.ID = d.AppModelID
            WHERE a.TotalTime > 0
            GROUP BY a.ID
            "#,
        )
        .fetch_all(pool)
        .await?;

        let mut tx = pool.begin().await?;
        for (app_id, new_total) in totals {
            sqlx::query("UPDATE AppModels SET TotalTime = ? WHERE ID = ?")
                .bind(new_total)
                .bind(app_id)
                .execute(&mut *tx)
                .await?;
        }
        tx.commit().await?;

        Ok(())
    }

    pub async fn get_time_range_log_list(
        pool: &SqlitePool,
        time: NaiveDateTime,
        tz_id: &str,
        config_service: &ConfigService,
    ) -> Result<Vec<HoursLogModel>, AppError> {
        debug!("get_time_range_log_list: time={}", time);
        let local_hour = NaiveDateTime::new(
            time.date(),
            chrono::NaiveTime::from_hms_opt(time.hour(), 0, 0).unwrap(),
        );
        let utc_hour = tz_naive_to_utc(local_hour, &parse_timezone(tz_id));

        let logs: Vec<HoursLogModel> =
            sqlx::query_as("SELECT * FROM HoursLogModels WHERE DataTime = ?")
                .bind(utc_hour)
                .fetch_all(pool)
                .await?;

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let app_refs: Vec<(i64, &str, Option<&str>)> = apps.iter()
            .map(|a| (a.id, a.name.as_deref().unwrap_or(""), a.file.as_deref()))
            .collect();
        let excluded_ids = config_service.get_excluded_app_ids(&app_refs).await;
        let excluded_set: std::collections::HashSet<i64> = excluded_ids.into_iter().collect();
        let app_map: std::collections::HashMap<i64, AppModelRow> =
            apps.into_iter().map(|a| (a.id, a)).collect();

        let categories = CategoryService::get_categories(pool).await?;
        let category_map: HashMap<i64, CategoryModel> = categories
            .into_iter()
            .map(|c| (c.id, c))
            .collect();

        let mut result = Vec::new();
        for log in logs {
            if excluded_set.contains(&log.app_model_id) {
                continue;
            }
            let app_model = app_map.get(&log.app_model_id).cloned().map(|a| AppModel {
                id: a.id,
                name: a.name,
                alias: a.alias,
                description: a.description,
                file: a.file,
                category_id: a.category_id,
                icon_file: a.icon_file,
                total_time: a.total_time,
                category: category_map.get(&a.category_id).cloned(),
            });

            result.push(HoursLogModel {
                id: log.id,
                data_time: log.data_time,
                app_model_id: log.app_model_id,
                time: log.time,
                app_model,
            });
        }

        Ok(result)
    }

    pub async fn get_hours_range_log_list(
        pool: &SqlitePool,
        start: NaiveDateTime,
        end: NaiveDateTime,
        tz_id: &str,
        config_service: &ConfigService,
    ) -> Result<Vec<HoursLogModel>, AppError> {
        debug!("get_hours_range_log_list: start={} end={}", start, end);
        let tz = parse_timezone(tz_id);
        let utc_start = tz_naive_to_utc(start, &tz);
        let utc_end = tz_naive_to_utc(end, &tz);

        let logs: Vec<HoursLogModel> =
            sqlx::query_as("SELECT * FROM HoursLogModels WHERE DataTime >= ? AND DataTime <= ?")
                .bind(utc_start)
                .bind(utc_end)
                .fetch_all(pool)
                .await?;

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let app_refs: Vec<(i64, &str, Option<&str>)> = apps.iter()
            .map(|a| (a.id, a.name.as_deref().unwrap_or(""), a.file.as_deref()))
            .collect();
        let excluded_ids = config_service.get_excluded_app_ids(&app_refs).await;
        let excluded_set: std::collections::HashSet<i64> = excluded_ids.into_iter().collect();
        let app_map: std::collections::HashMap<i64, AppModelRow> =
            apps.into_iter().map(|a| (a.id, a)).collect();

        let categories = CategoryService::get_categories(pool).await?;
        let category_map: HashMap<i64, CategoryModel> = categories
            .into_iter()
            .map(|c| (c.id, c))
            .collect();

        let mut result = Vec::new();
        for log in logs {
            if excluded_set.contains(&log.app_model_id) {
                continue;
            }
            let app_model = app_map.get(&log.app_model_id).cloned().map(|a| AppModel {
                id: a.id,
                name: a.name,
                alias: a.alias,
                description: a.description,
                file: a.file,
                category_id: a.category_id,
                icon_file: a.icon_file,
                total_time: a.total_time,
                category: category_map.get(&a.category_id).cloned(),
            });

            result.push(HoursLogModel {
                id: log.id,
                data_time: log.data_time,
                app_model_id: log.app_model_id,
                time: log.time,
                app_model,
            });
        }

        Ok(result)
    }

    pub async fn get_range_total_data(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<f64>, AppError> {
        debug!("get_range_total_data: start={} end={}", start, end);
        if start == end {
            let tz = parse_timezone(tz_id);
            let (utc_start, utc_end) = tz_date_to_utc_range(start, &tz);

            let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
                r#"
                SELECT DataTime as data_time, SUM(Time) as total
                FROM HoursLogModels
                WHERE DataTime >= ? AND DataTime < ?
                GROUP BY DataTime
                "#,
            )
            .bind(utc_start)
            .bind(utc_end)
            .fetch_all(pool)
            .await?;

            let mut result = vec![0.0; 24];
            for (utc_dt, total) in rows {
                let local_hour = utc_dt.with_timezone(&tz).hour() as usize;
                if local_hour < 24 {
                    result[local_hour] += total as f64;
                }
            }
            Ok(result)
        } else {
            let tz = parse_timezone(tz_id);
            let utc_start = tz_date_to_utc_range(start, &tz).0;
            let utc_end = tz_date_to_utc_range(end, &tz).1;

            let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
                r#"
                SELECT DataTime, SUM(Time) as total
                FROM HoursLogModels
                WHERE DataTime >= ? AND DataTime < ?
                GROUP BY DataTime
                "#,
            )
            .bind(utc_start)
            .bind(utc_end)
            .fetch_all(pool)
            .await?;

            let days = (end - start).num_days() + 1;
            let mut result = vec![0.0; days as usize];
            for (utc_dt, total) in rows {
                let local_date = utc_dt.with_timezone(&tz).date_naive();
                if local_date >= start && local_date <= end {
                    let index = (local_date - start).num_days() as usize;
                    result[index] += total as f64;
                }
            }
            Ok(result)
        }
    }

    pub async fn get_month_total_data(
        pool: &SqlitePool,
        year: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<f64>, AppError> {
        debug!("get_month_total_data: year={}", year);
        let tz = parse_timezone(tz_id);
        let year_start = NaiveDate::from_ymd_opt(year.year(), 1, 1).unwrap();
        let year_end = NaiveDate::from_ymd_opt(year.year(), 12, 31).unwrap();
        let utc_start = tz_date_to_utc_range(year_start, &tz).0;
        let utc_end = tz_date_to_utc_range(year_end, &tz).1;

        let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT DataTime, SUM(Time) as total
            FROM HoursLogModels
            WHERE DataTime >= ? AND DataTime < ?
            GROUP BY DataTime
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let mut result = vec![0.0; 12];
        for (utc_dt, total) in rows {
            let local_date = utc_dt.with_timezone(&tz).date_naive();
            if local_date.year() == year.year() {
                let month = local_date.month() as usize;
                result[month - 1] += total as f64;
            }
        }

        Ok(result)
    }

    pub async fn get_date_range_app_count(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        tz_id: &str,
    ) -> Result<i64, AppError> {
        debug!("get_date_range_app_count: start={} end={}", start, end);
        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);
        let count: (i64,) = sqlx::query_as(
            r#"
            SELECT COUNT(DISTINCT AppModelID) FROM DailyLogModels
            WHERE Date >= ? AND Date <= ? AND AppModelID != 0
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_one(pool)
        .await?;
        Ok(count.0)
    }

    pub async fn get_category_hours_data(
        pool: &SqlitePool,
        date: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_category_hours_data: date={}", date);
        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_to_utc_range(date, &tz);

        let categories: Vec<(i64, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, SUM(h.Time) as total
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE h.DataTime >= ? AND h.DataTime < ?
            GROUP BY a.CategoryID
            ORDER BY category_id DESC
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let data: Vec<(i64, DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, h.DataTime as data_time, SUM(h.Time) as total
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE h.DataTime >= ? AND h.DataTime < ?
            GROUP BY a.CategoryID, h.DataTime
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let mut list = Vec::new();
        for (cat_id, _) in &categories {
            list.push(ColumnDataModel {
                app_id: None,
                category_id: Some(*cat_id),
                values: vec![0.0; 24],
            });
        }

        let data_map: HashMap<(i64, DateTime<Utc>), i64> =
            data.into_iter().map(|(cid, dt, t)| ((cid, dt), t)).collect();
        let mut list_map: HashMap<i64, &mut ColumnDataModel> =
            HashMap::with_capacity(list.len());
        for item in &mut list {
            if let Some(cid) = item.category_id {
                list_map.insert(cid, item);
            }
        }

        for i in 0..24 {
            let local_hour = NaiveDateTime::new(
                date,
                chrono::NaiveTime::from_hms_opt(i as u32, 0, 0).unwrap(),
            );
            let utc_hour = tz_naive_to_utc(local_hour, &tz);
            for (cat_id, _) in &categories {
                let total = data_map.get(&(*cat_id, utc_hour)).copied().unwrap_or(0);
                if let Some(item) = list_map.get_mut(cat_id) {
                    item.values[i] = total as f64;
                }
            }
        }

        Ok(list)
    }

    pub async fn get_category_range_data(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_category_range_data: start={} end={}", start, end);
        let tz = parse_timezone(tz_id);
        let utc_start = tz_date_to_utc_range(start, &tz).0;
        let utc_end = tz_date_to_utc_range(end, &tz).1;

        let rows: Vec<(i64, DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, h.DataTime as data_time, h.Time as time
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE h.DataTime >= ? AND h.DataTime < ?
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let days = (end - start).num_days() + 1;
        let mut map: HashMap<i64, Vec<f64>> = HashMap::new();

        for (cat_id, utc_dt, time) in rows {
            let local_date = utc_dt.with_timezone(&tz).date_naive();
            if local_date >= start && local_date <= end {
                let entry = map.entry(cat_id).or_insert_with(|| vec![0.0; days as usize]);
                let index = (local_date - start).num_days() as usize;
                entry[index] += time as f64;
            }
        }

        let mut list = Vec::new();
        for (cat_id, values) in map {
            list.push(ColumnDataModel {
                app_id: None,
                category_id: Some(cat_id),
                values,
            });
        }

        Ok(list)
    }

    pub async fn get_category_year_data(
        pool: &SqlitePool,
        date: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_category_year_data: date={}", date);
        let tz = parse_timezone(tz_id);
        let year_start = NaiveDate::from_ymd_opt(date.year(), 1, 1).unwrap();
        let year_end = NaiveDate::from_ymd_opt(date.year(), 12, 31).unwrap();
        let utc_start = tz_date_to_utc_range(year_start, &tz).0;
        let utc_end = tz_date_to_utc_range(year_end, &tz).1;

        let rows: Vec<(i64, DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, h.DataTime as data_time, SUM(h.Time) as total
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE h.DataTime >= ? AND h.DataTime < ?
            GROUP BY a.CategoryID, h.DataTime
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let mut map: HashMap<i64, Vec<f64>> = HashMap::new();
        for (cat_id, utc_dt, time) in rows {
            let local_date = utc_dt.with_timezone(&tz).date_naive();
            if local_date.year() == date.year() {
                let month = local_date.month() as usize;
                let entry = map.entry(cat_id).or_insert_with(|| vec![0.0; 12]);
                entry[month - 1] += time as f64;
            }
        }

        let mut list = Vec::new();
        for (cat_id, values) in map {
            list.push(ColumnDataModel {
                app_id: None,
                category_id: Some(cat_id),
                values,
            });
        }

        Ok(list)
    }

    pub async fn get_app_day_data(
        pool: &SqlitePool,
        app_id: i64,
        date: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_app_day_data: app_id={} date={}", app_id, date);
        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_to_utc_range(date, &tz);

        let data: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT DataTime as data_time, SUM(Time) as total
            FROM HoursLogModels
            WHERE AppModelID = ? AND DataTime >= ? AND DataTime < ?
            GROUP BY DataTime
            "#,
        )
        .bind(app_id)
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let data_map: HashMap<DateTime<Utc>, i64> = data.into_iter().collect();
        let mut values = vec![0.0; 24];
        for i in 0..24 {
            let local_hour = NaiveDateTime::new(
                date,
                chrono::NaiveTime::from_hms_opt(i as u32, 0, 0).unwrap(),
            );
            let utc_hour = tz_naive_to_utc(local_hour, &tz);
            values[i] = data_map.get(&utc_hour).copied().unwrap_or(0) as f64;
        }

        Ok(vec![ColumnDataModel {
            app_id: Some(app_id),
            category_id: None,
            values,
        }])
    }

    pub async fn get_app_range_data(
        pool: &SqlitePool,
        app_id: i64,
        start: NaiveDate,
        end: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_app_range_data: app_id={} start={} end={}", app_id, start, end);
        let tz = parse_timezone(tz_id);
        let utc_start = tz_date_to_utc_range(start, &tz).0;
        let utc_end = tz_date_to_utc_range(end, &tz).1;

        let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT DataTime, Time
            FROM HoursLogModels
            WHERE AppModelID = ? AND DataTime >= ? AND DataTime < ?
            "#,
        )
        .bind(app_id)
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let days = (end - start).num_days() + 1;
        let mut values = vec![0.0; days as usize];

        for (utc_dt, time) in rows {
            let local_date = utc_dt.with_timezone(&tz).date_naive();
            if local_date >= start && local_date <= end {
                let index = (local_date - start).num_days() as usize;
                values[index] += time as f64;
            }
        }

        Ok(vec![ColumnDataModel {
            app_id: Some(app_id),
            category_id: None,
            values,
        }])
    }

    pub async fn get_app_year_data(
        pool: &SqlitePool,
        app_id: i64,
        date: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_app_year_data: app_id={} date={}", app_id, date);
        let tz = parse_timezone(tz_id);
        let year_start = NaiveDate::from_ymd_opt(date.year(), 1, 1).unwrap();
        let year_end = NaiveDate::from_ymd_opt(date.year(), 12, 31).unwrap();
        let utc_start = tz_date_to_utc_range(year_start, &tz).0;
        let utc_end = tz_date_to_utc_range(year_end, &tz).1;

        let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT DataTime, SUM(Time) as total
            FROM HoursLogModels
            WHERE AppModelID = ? AND DataTime >= ? AND DataTime < ?
            GROUP BY DataTime
            "#,
        )
        .bind(app_id)
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let mut values = vec![0.0; 12];
        for (utc_dt, total) in rows {
            let local_date = utc_dt.with_timezone(&tz).date_naive();
            if local_date.year() == date.year() {
                let month = local_date.month() as usize;
                values[month - 1] += total as f64;
            }
        }

        Ok(vec![ColumnDataModel {
            app_id: Some(app_id),
            category_id: None,
            values,
        }])
    }

    pub async fn get_export_data(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        tz_id: &str,
        config_service: &ConfigService,
    ) -> Result<ExportDataResult, AppError> {
        info!("get_export_data: start={} end={}", start, end);
        const MAX_EXPORT_DAYS: i64 = 366;
        let days = (end - start).num_days() + 1;
        if days > MAX_EXPORT_DAYS {
            return Err(AppError::Business(format!("导出范围不能超过 {} 天", MAX_EXPORT_DAYS)));
        }
        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = (tz_date_to_utc_range(start, &tz).0, tz_date_to_utc_range(end, &tz).1);

        let hours_logs: Vec<HoursLogModel> = sqlx::query_as(
            "SELECT * FROM HoursLogModels WHERE DataTime >= ? AND DataTime <= ? AND AppModelID != 0",
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let app_refs: Vec<(i64, &str, Option<&str>)> = apps.iter()
            .map(|a| (a.id, a.name.as_deref().unwrap_or(""), a.file.as_deref()))
            .collect();
        let excluded_ids = config_service.get_excluded_app_ids(&app_refs).await;
        let excluded_set: std::collections::HashSet<i64> = excluded_ids.into_iter().collect();
        let app_map: std::collections::HashMap<i64, AppModelRow> =
            apps.into_iter().map(|a| (a.id, a)).collect();
        let categories: Vec<CategoryModel> = sqlx::query_as("SELECT * FROM CategoryModels")
            .fetch_all(pool)
            .await?;
        let category_map: std::collections::HashMap<i64, CategoryModel> =
            categories.into_iter().map(|c| (c.id, c)).collect();

        // 内存聚合 daily_groups
        let mut daily_groups: HashMap<(NaiveDate, i64), i64> = HashMap::new();
        for log in &hours_logs {
            let local_date = log.data_time.with_timezone(&tz).date_naive();
            if local_date >= start && local_date <= end {
                *daily_groups.entry((local_date, log.app_model_id)).or_insert(0) += log.time;
            }
        }

        let mut daily_logs: Vec<DailyLogModel> = daily_groups
            .into_iter()
            .filter(|((_, app_id), _)| !excluded_set.contains(app_id))
            .map(|((date, app_id), time)| {
                let app_model = app_map.get(&app_id).cloned().map(|app_row| {
                    AppModel {
                        id: app_row.id,
                        name: app_row.name,
                        alias: app_row.alias,
                        description: app_row.description,
                        file: app_row.file,
                        category_id: app_row.category_id,
                        icon_file: app_row.icon_file,
                        total_time: app_row.total_time,
                        category: category_map.get(&app_row.category_id).cloned(),
                    }
                });
                DailyLogModel {
                    id: 0,
                    date,
                    app_model_id: app_id,
                    time,
                    app_model,
                }
            })
            .collect();

        daily_logs.sort_by(|a, b| {
            a.date.cmp(&b.date).then_with(|| {
                let a_name = a.app_model.as_ref().and_then(|m| m.alias.as_deref().or(m.name.as_deref())).unwrap_or("");
                let b_name = b.app_model.as_ref().and_then(|m| m.alias.as_deref().or(m.name.as_deref())).unwrap_or("");
                a_name.cmp(b_name)
            })
        });

        // Filter hours_logs by local date and fill app/category
        let mut hours_logs: Vec<HoursLogModel> = hours_logs
            .into_iter()
            .filter(|log| !excluded_set.contains(&log.app_model_id))
            .filter(|log| {
                let local_date = log.data_time.with_timezone(&tz).date_naive();
                local_date >= start && local_date <= end
            })
            .map(|mut log| {
                if let Some(app_row) = app_map.get(&log.app_model_id).cloned() {
                    log.app_model = Some(AppModel {
                        id: app_row.id,
                        name: app_row.name,
                        alias: app_row.alias,
                        description: app_row.description,
                        file: app_row.file,
                        category_id: app_row.category_id,
                        icon_file: app_row.icon_file,
                        total_time: app_row.total_time,
                        category: category_map.get(&app_row.category_id).cloned(),
                    });
                }
                log
            })
            .collect();

        hours_logs.sort_by(|a, b| {
            a.data_time.cmp(&b.data_time).then_with(|| {
                let a_name = a.app_model.as_ref().and_then(|m| m.alias.as_deref().or(m.name.as_deref())).unwrap_or("");
                let b_name = b.app_model.as_ref().and_then(|m| m.alias.as_deref().or(m.name.as_deref())).unwrap_or("");
                a_name.cmp(b_name)
            })
        });

        Ok(ExportDataResult {
            daily_logs,
            hours_logs,
        })
    }

    pub async fn get_app_sessions(
        pool: &SqlitePool,
        start: NaiveDateTime,
        end: NaiveDateTime,
        tz_id: &str,
        config_service: &ConfigService,
    ) -> Result<Vec<AppSessionModel>, AppError> {
        debug!("get_app_sessions: start={} end={}", start, end);
        let tz = parse_timezone(tz_id);
        let utc_start = tz_naive_to_utc(start, &tz);
        let utc_end = tz_naive_to_utc(end, &tz);

        let sessions: Vec<AppSessionModel> = sqlx::query_as(
            r#"
            SELECT s.* FROM AppSessions s
            WHERE s.StartTime >= ? AND s.StartTime < ?
            ORDER BY s.StartTime ASC
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let app_refs: Vec<(i64, &str, Option<&str>)> = apps.iter()
            .map(|a| (a.id, a.name.as_deref().unwrap_or(""), a.file.as_deref()))
            .collect();
        let excluded_ids = config_service.get_excluded_app_ids(&app_refs).await;
        let excluded_set: std::collections::HashSet<i64> = excluded_ids.into_iter().collect();

        let app_map: HashMap<i64, AppModelRow> = apps.into_iter().map(|a| (a.id, a)).collect();

        let categories = CategoryService::get_categories(pool).await?;
        let category_map: HashMap<i64, CategoryModel> = categories
            .into_iter()
            .map(|c| (c.id, c))
            .collect();

        let mut result = Vec::new();
        for mut session in sessions {
            if excluded_set.contains(&session.app_model_id) {
                continue;
            }
            if let Some(app_row) = app_map.get(&session.app_model_id) {
                session.app_model = Some(AppModel {
                    id: app_row.id,
                    name: app_row.name.clone(),
                    alias: app_row.alias.clone(),
                    description: app_row.description.clone(),
                    file: app_row.file.clone(),
                    category_id: app_row.category_id,
                    icon_file: app_row.icon_file.clone(),
                    total_time: app_row.total_time,
                    category: category_map.get(&app_row.category_id).cloned(),
                });
            }
            result.push(session);
        }

        // 合并相邻 session（间隔 <=30s），过滤 <3s
        result = merge_and_filter_sessions(result);

        Ok(result)
    }

}


fn merge_and_filter_sessions(sessions: Vec<AppSessionModel>) -> Vec<AppSessionModel> {
    const MERGE_GAP_SECS: i64 = 30;
    const MIN_DURATION_SECS: i64 = 3;

    // 过滤超短片段
    let sessions: Vec<_> = sessions
        .into_iter()
        .filter(|s| s.duration >= MIN_DURATION_SECS)
        .collect();

    if sessions.is_empty() {
        return sessions;
    }

    // 按应用分组合并相邻段
    let mut by_app: HashMap<i64, Vec<AppSessionModel>> = HashMap::new();
    for session in sessions {
        by_app.entry(session.app_model_id).or_default().push(session);
    }

    let mut merged = Vec::new();
    for (_, mut app_sessions) in by_app {
        app_sessions.sort_by(|a, b| a.start_time.cmp(&b.start_time));

        let mut iter = app_sessions.into_iter();
        let mut current = iter.next().unwrap();
        for next in iter {
            let gap = next.start_time.signed_duration_since(current.end_time).num_seconds();
            if gap <= MERGE_GAP_SECS {
                if next.end_time > current.end_time {
                    current.end_time = next.end_time;
                }
                current.duration += next.duration;
            } else {
                merged.push(current);
                current = next;
            }
        }
        merged.push(current);
    }

    merged.sort_by(|a, b| a.start_time.cmp(&b.start_time));
    merged
}
