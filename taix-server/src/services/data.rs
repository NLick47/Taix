use chrono::{DateTime, Datelike, Duration, NaiveDate, NaiveDateTime, TimeZone, Timelike, Utc};
use sqlx::SqlitePool;
use std::collections::HashMap;
use tracing::{debug, info};

use crate::error::AppError;
use crate::models::app::{AppModel, AppModelRow};
use crate::models::category::CategoryModel;
use crate::models::log::{
    ColumnDataModel, DailyLogModel, ExportDataResult, HoursLogModel,
};
use crate::utils::{last_day_of_month, parse_timezone, tz_date_range_to_utc_date_range, tz_date_to_utc_range, tz_naive_to_utc};

/// 将 UTC 日期转换为目标时区的本地日期（取 UTC 00:00 在该时区对应的日期）
fn utc_date_to_local_date(utc_date: NaiveDate, tz: &chrono_tz::Tz) -> NaiveDate {
    let utc_dt = Utc.from_utc_datetime(&utc_date.and_hms_opt(0, 0, 0).unwrap());
    utc_dt.with_timezone(tz).date_naive()
}

pub struct DataService;

impl DataService {
    pub async fn get_today_log_list(pool: &SqlitePool, today: NaiveDate, tz_id: &str) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_today_log_list");
        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_to_utc_range(today, &tz);
        let logs: Vec<DailyLogModel> = sqlx::query_as(
            "SELECT * FROM DailyLogModels WHERE Date >= ? AND Date <= ? AND AppModelID != 0",
        )
        .bind(utc_start.date_naive().to_string())
        .bind(utc_end.date_naive().to_string())
        .fetch_all(pool)
        .await?;
        Ok(logs)
    }

    pub async fn get_date_range_log_list(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
        take: i64,
        skip: i64,
        tz_id: &str,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_date_range_log_list: start={} end={} take={} skip={}", start, end, take, skip);
        let limit = if take > 0 { take } else { -1 };
        let offset = if skip > 0 { skip } else { 0 };

        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);

        // 先查出原始记录，再在后端做过滤和聚合，避免 UTC 日期边界导致的数据混叠
        let logs: Vec<DailyLogModel> = sqlx::query_as(
            "SELECT * FROM DailyLogModels WHERE Date >= ? AND Date <= ? AND AppModelID != 0"
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        // 后过滤：排除本地日期不在 [start, end] 范围内的记录
        let mut groups: HashMap<i64, (i64, NaiveDate)> = HashMap::new();
        for log in logs {
            let local_date = utc_date_to_local_date(log.date, &tz);
            if local_date >= start && local_date <= end {
                let entry = groups.entry(log.app_model_id).or_insert((0, log.date));
                entry.0 += log.time;
                entry.1 = entry.1.max(log.date);
            }
        }

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        // 按使用时长降序排列，再应用 LIMIT/OFFSET
        let mut sorted_groups: Vec<_> = groups.into_iter().collect();
        sorted_groups.sort_by(|a, b| b.1.0.cmp(&a.1.0));
        let offset_usize = offset as usize;
        let limit_usize = if limit > 0 { limit as usize } else { usize::MAX };
        let sorted_groups: Vec<_> = sorted_groups.into_iter()
            .skip(offset_usize)
            .take(limit_usize)
            .collect();

        let mut result = Vec::new();
        for (app_id, (time, date)) in sorted_groups {
            let app = apps.iter().find(|a| a.id == app_id).cloned();
            let app_model = app.map(|a| AppModel {
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

    pub async fn get_this_week_log_list(
        pool: &SqlitePool,
        date: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_this_week_log_list");
        let (week_start, week_end) = get_week_range(date, false);
        Self::get_date_range_log_list(pool, week_start, week_end, -1, -1, tz_id).await
    }

    pub async fn get_last_week_log_list(
        pool: &SqlitePool,
        date: NaiveDate,
        tz_id: &str,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_last_week_log_list");
        let (week_start, week_end) = get_week_range(date, true);
        Self::get_date_range_log_list(pool, week_start, week_end, -1, -1, tz_id).await
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
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(month_start, month_end, &tz);

        let logs: Vec<DailyLogModel> = sqlx::query_as(
            "SELECT * FROM DailyLogModels WHERE Date >= ? AND Date <= ? AND AppModelID = ?"
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .bind(app_id)
        .fetch_all(pool)
        .await?;

        // 后过滤：排除本地日期不在目标月份范围内的记录
        let logs: Vec<DailyLogModel> = logs.into_iter()
            .filter(|log| {
                let local_date = utc_date_to_local_date(log.date, &tz);
                local_date >= month_start && local_date <= month_end
            })
            .collect();
        Ok(logs)
    }

    pub async fn get_process_day(
        pool: &SqlitePool,
        app_id: i64,
        day: NaiveDate,
        tz_id: &str,
    ) -> Result<Option<DailyLogModel>, AppError> {
        debug!("get_process_day: app_id={} day={}", app_id, day);
        let tz = parse_timezone(tz_id);
        let (utc_start, utc_end) = tz_date_to_utc_range(day, &tz);
        let logs: Vec<DailyLogModel> = sqlx::query_as(
            "SELECT * FROM DailyLogModels WHERE AppModelID = ? AND Date >= ? AND Date <= ?",
        )
        .bind(app_id)
        .bind(utc_start.date_naive().to_string())
        .bind(utc_end.date_naive().to_string())
        .fetch_all(pool)
        .await?;

        // 后过滤：只保留本地日期等于目标日期的记录
        let logs: Vec<DailyLogModel> = logs.into_iter()
            .filter(|log| {
                let local_date = utc_date_to_local_date(log.date, &tz);
                local_date == day
            })
            .collect();

        if logs.is_empty() {
            Ok(None)
        } else {
            let total_time = logs.iter().map(|l| l.time).sum();
            let first = logs.into_iter().next().unwrap();
            Ok(Some(DailyLogModel {
                id: first.id,
                date: first.date,
                app_model_id: first.app_model_id,
                time: total_time,
                app_model: first.app_model,
            }))
        }
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
            .bind(utc_start.to_string())
            .bind(utc_end.to_string())
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
            .bind(utc_start.to_string())
            .bind(utc_end.to_string())
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

        for (app_id, new_total) in totals {
            sqlx::query("UPDATE AppModels SET TotalTime = ? WHERE ID = ?")
                .bind(new_total)
                .bind(app_id)
                .execute(pool)
                .await?;
        }

        Ok(())
    }

    pub async fn get_time_range_log_list(
        pool: &SqlitePool,
        time: NaiveDateTime,
        tz_id: &str,
    ) -> Result<Vec<HoursLogModel>, AppError> {
        debug!("get_time_range_log_list: time={}", time);
        let local_hour = NaiveDateTime::new(
            time.date(),
            chrono::NaiveTime::from_hms_opt(time.hour() as u32, 0, 0).unwrap(),
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

        let mut result = Vec::new();
        for log in logs {
            let app = apps.iter().find(|a| a.id == log.app_model_id).cloned();
            let app_model = app.map(|a| AppModel {
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
            let (utc_start, utc_end) = tz_date_to_utc_range(start, &parse_timezone(tz_id));

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
                let local_hour = utc_dt.with_timezone(&parse_timezone(tz_id)).hour() as usize;
                if local_hour < 24 {
                    result[local_hour] += total as f64;
                }
            }
            Ok(result)
        } else {
            let tz = parse_timezone(tz_id);
            let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);
            let rows: Vec<(NaiveDate, i64)> = sqlx::query_as(
                r#"
                SELECT Date as date, SUM(Time) as total
                FROM DailyLogModels
                WHERE Date >= ? AND Date <= ?
                GROUP BY Date
                "#,
            )
            .bind(utc_start.to_string())
            .bind(utc_end.to_string())
            .fetch_all(pool)
            .await?;

            let days = (end - start).num_days() + 1;
            let mut result = vec![0.0; days as usize];
            for (utc_date, total) in rows {
                let local_date = utc_date_to_local_date(utc_date, &tz);
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
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(year_start, year_end, &tz);

        let rows: Vec<(NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT Date as date, SUM(Time) as total
            FROM DailyLogModels
            WHERE Date >= ? AND Date <= ?
            GROUP BY Date
            "#,
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let mut result = vec![0.0; 12];
        for i in 1..=12 {
            let month_start = NaiveDate::from_ymd_opt(year.year(), i, 1).unwrap();
            let month_end = last_day_of_month(year.year(), i);
            let month_end = NaiveDate::from_ymd_opt(year.year(), i, month_end).unwrap();

            let total: i64 = rows
                .iter()
                .filter(|(utc_d, _)| {
                    let local_d = utc_date_to_local_date(*utc_d, &tz);
                    local_d >= month_start && local_d <= month_end
                })
                .map(|(_, t)| t)
                .sum();
            result[(i - 1) as usize] = total as f64;
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
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
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
        let (utc_start, utc_end) = tz_date_to_utc_range(date, &parse_timezone(tz_id));

        let categories: Vec<(i64, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, SUM(h.Time) as total
            FROM HoursLogModels h
            JOIN AppModels a ON h.AppModelID = a.ID
            WHERE h.DataTime >= ? AND h.DataTime <= ?
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

        for i in 0..24 {
            let local_hour = NaiveDateTime::new(
                date,
                chrono::NaiveTime::from_hms_opt(i as u32, 0, 0).unwrap(),
            );
            let utc_hour = tz_naive_to_utc(local_hour, &parse_timezone(tz_id));
            for (cat_id, _) in &categories {
                let total = data
                    .iter()
                    .filter(|(cid, dt, _)| {
                        *cid == *cat_id && *dt == utc_hour
                    })
                    .map(|(_, _, t)| *t)
                    .next()
                    .unwrap_or(0);

                if let Some(item) = list.iter_mut().find(|x| x.category_id == Some(*cat_id)) {
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
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);

        let categories: Vec<(i64, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, SUM(d.Time) as total
            FROM DailyLogModels d
            JOIN AppModels a ON d.AppModelID = a.ID
            WHERE d.Date >= ? AND d.Date <= ?
            GROUP BY a.CategoryID
            ORDER BY category_id DESC
            "#,
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let data: Vec<(i64, NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, d.Date as date, SUM(d.Time) as total
            FROM DailyLogModels d
            JOIN AppModels a ON d.AppModelID = a.ID
            WHERE d.Date >= ? AND d.Date <= ?
            GROUP BY a.CategoryID, d.Date
            "#,
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let days = (end - start).num_days() + 1;
        let mut list = Vec::new();
        for (cat_id, _) in &categories {
            list.push(ColumnDataModel {
                app_id: None,
                category_id: Some(*cat_id),
                values: vec![0.0; days as usize],
            });
        }

        // 将 UTC 日期数据映射回对应的本地日期
        for (cat_id, utc_date, total) in &data {
            let local_date = utc_date_to_local_date(*utc_date, &tz);
            if local_date >= start && local_date <= end {
                if let Some(item) = list.iter_mut().find(|x| x.category_id == Some(*cat_id)) {
                    let index = (local_date - start).num_days() as usize;
                    item.values[index] += *total as f64;
                }
            }
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
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(year_start, year_end, &tz);

        let categories: Vec<(i64, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, SUM(d.Time) as total
            FROM DailyLogModels d
            JOIN AppModels a ON d.AppModelID = a.ID
            WHERE d.Date >= ? AND d.Date <= ?
            GROUP BY a.CategoryID
            ORDER BY category_id DESC
            "#,
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let data: Vec<(i64, NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT a.CategoryID as category_id, d.Date as date, SUM(d.Time) as total
            FROM DailyLogModels d
            JOIN AppModels a ON d.AppModelID = a.ID
            WHERE d.Date >= ? AND d.Date <= ?
            GROUP BY a.CategoryID, d.Date
            "#,
        )
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let mut list = Vec::new();
        for (cat_id, _) in &categories {
            list.push(ColumnDataModel {
                app_id: None,
                category_id: Some(*cat_id),
                values: vec![0.0; 12],
            });
        }

        for i in 1..=12 {
            let month_start = NaiveDate::from_ymd_opt(date.year(), i, 1).unwrap();
            let month_end_day = last_day_of_month(date.year(), i);
            let month_end = NaiveDate::from_ymd_opt(date.year(), i, month_end_day).unwrap();

            for (cat_id, _) in &categories {
                let total: i64 = data
                    .iter()
                    .filter(|(cid, utc_d, _)| {
                        let local_d = utc_date_to_local_date(*utc_d, &tz);
                        *cid == *cat_id && local_d >= month_start && local_d <= month_end
                    })
                    .map(|(_, _, t)| t)
                    .sum();

                if let Some(item) = list.iter_mut().find(|x| x.category_id == Some(*cat_id)) {
                    item.values[(i - 1) as usize] = total as f64;
                }
            }
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
        let (utc_start, utc_end) = tz_date_to_utc_range(date, &parse_timezone(tz_id));

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

        let mut values = vec![0.0; 24];
        for i in 0..24 {
            let local_hour = NaiveDateTime::new(
                date,
                chrono::NaiveTime::from_hms_opt(i as u32, 0, 0).unwrap(),
            );
            let utc_hour = tz_naive_to_utc(local_hour, &parse_timezone(tz_id));
            let total = data
                .iter()
                .find(|(dt, _)| {
                    *dt == utc_hour
                })
                .map(|(_, t)| *t)
                .unwrap_or(0);
            values[i] = total as f64;
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
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);

        let data: Vec<(NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT Date as date, SUM(Time) as total
            FROM DailyLogModels
            WHERE AppModelID = ? AND Date >= ? AND Date <= ?
            GROUP BY Date
            "#,
        )
        .bind(app_id)
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let days = (end - start).num_days() + 1;
        let mut values = vec![0.0; days as usize];

        for (utc_date, total) in &data {
            let local_date = utc_date_to_local_date(*utc_date, &tz);
            if local_date >= start && local_date <= end {
                let index = (local_date - start).num_days() as usize;
                values[index] += *total as f64;
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
        let (utc_start, utc_end) = tz_date_range_to_utc_date_range(year_start, year_end, &tz);

        let data: Vec<(NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT Date as date, SUM(Time) as total
            FROM DailyLogModels
            WHERE AppModelID = ? AND Date >= ? AND Date <= ?
            GROUP BY Date
            "#,
        )
        .bind(app_id)
        .bind(utc_start.to_string())
        .bind(utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let mut values = vec![0.0; 12];
        for i in 1..=12 {
            let month_start = NaiveDate::from_ymd_opt(date.year(), i, 1).unwrap();
            let month_end_day = last_day_of_month(date.year(), i);
            let month_end = NaiveDate::from_ymd_opt(date.year(), i, month_end_day).unwrap();

            let total: i64 = data
                .iter()
                .filter(|(utc_d, _)| {
                    let local_d = utc_date_to_local_date(*utc_d, &tz);
                    local_d >= month_start && local_d <= month_end
                })
                .map(|(_, t)| t)
                .sum();
            values[(i - 1) as usize] = total as f64;
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
    ) -> Result<ExportDataResult, AppError> {
        info!("get_export_data: start={} end={}", start, end);
        let tz = parse_timezone(tz_id);
        let (daily_utc_start, daily_utc_end) = tz_date_range_to_utc_date_range(start, end, &tz);
        let s = tz_date_to_utc_range(start, &tz);
        let e = tz_date_to_utc_range(end, &tz);
        let (utc_start, utc_end) = (s.0, e.1);

        let daily_logs: Vec<DailyLogModel> = sqlx::query_as(
            r#"
            SELECT d.* FROM DailyLogModels d
            WHERE d.Date >= ? AND d.Date <= ?
            "#,
        )
        .bind(daily_utc_start.to_string())
        .bind(daily_utc_end.to_string())
        .fetch_all(pool)
        .await?;

        let hours_logs: Vec<HoursLogModel> = sqlx::query_as(
            "SELECT * FROM HoursLogModels WHERE DataTime >= ? AND DataTime <= ?",
        )
        .bind(utc_start)
        .bind(utc_end)
        .fetch_all(pool)
        .await?;

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let categories: Vec<CategoryModel> = sqlx::query_as("SELECT * FROM CategoryModels")
            .fetch_all(pool)
            .await?;

        let daily_logs = daily_logs
            .into_iter()
            .map(|mut log| {
                if let Some(app_row) = apps.iter().find(|a| a.id == log.app_model_id).cloned() {
                    let category = categories.iter().find(|c| c.id == app_row.category_id).cloned();
                    log.app_model = Some(AppModel {
                        id: app_row.id,
                        name: app_row.name,
                        alias: app_row.alias,
                        description: app_row.description,
                        file: app_row.file,
                        category_id: app_row.category_id,
                        icon_file: app_row.icon_file,
                        total_time: app_row.total_time,
                        category,
                    });
                }
                log
            })
            .collect();

        let hours_logs = hours_logs
            .into_iter()
            .map(|mut log| {
                if let Some(app_row) = apps.iter().find(|a| a.id == log.app_model_id).cloned() {
                    let category = categories.iter().find(|c| c.id == app_row.category_id).cloned();
                    log.app_model = Some(AppModel {
                        id: app_row.id,
                        name: app_row.name,
                        alias: app_row.alias,
                        description: app_row.description,
                        file: app_row.file,
                        category_id: app_row.category_id,
                        icon_file: app_row.icon_file,
                        total_time: app_row.total_time,
                        category,
                    });
                }
                log
            })
            .collect();

        Ok(ExportDataResult {
            daily_logs,
            hours_logs,
        })
    }

}

fn get_week_range(today: NaiveDate, last_week: bool) -> (NaiveDate, NaiveDate) {
    let weekday = today.weekday().num_days_from_monday() as i64;
    if last_week {
        let start = today - Duration::days(weekday + 7);
        let end = start + Duration::days(6);
        (start, end)
    } else {
        let start = today - Duration::days(weekday);
        let end = start + Duration::days(6);
        (start, end)
    }
}

