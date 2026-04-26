use chrono::{DateTime, Datelike, Duration, Local, NaiveDate, NaiveDateTime, TimeZone, Timelike, Utc};
use sqlx::SqlitePool;
use tracing::{debug, info};

use crate::error::AppError;
use crate::models::app::{AppModel, AppModelRow};
use crate::models::category::CategoryModel;
use crate::models::log::{
    ColumnDataModel, DailyLogModel, ExportDataResult, HoursLogModel,
};

pub struct DataService;

impl DataService {
    pub async fn get_today_log_list(pool: &SqlitePool) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_today_log_list");
        let today = Local::now().date_naive();
        let logs: Vec<DailyLogModel> = sqlx::query_as(
            "SELECT * FROM DailyLogModels WHERE Date = ? AND AppModelID != 0",
        )
        .bind(today.to_string())
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
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_date_range_log_list: start={} end={} take={} skip={}", start, end, take, skip);
        // SQLite 中 LIMIT -1 表示无限制，OFFSET 0 表示不跳过
        let limit = if take > 0 { take } else { -1 };
        let offset = if skip > 0 { skip } else { 0 };

        let rows: Vec<(i64, i64, NaiveDate)> = sqlx::query_as(
            r#"
            SELECT AppModelID as app_model_id, SUM(Time) as time, MAX(Date) as date
            FROM DailyLogModels
            WHERE Date >= ? AND Date <= ? AND AppModelID != 0
            GROUP BY AppModelID
            ORDER BY time DESC
            LIMIT ? OFFSET ?
            "#,
        )
        .bind(start.to_string())
        .bind(end.to_string())
        .bind(limit)
        .bind(offset)
        .fetch_all(pool)
        .await?;

        let apps: Vec<AppModelRow> = sqlx::query_as("SELECT * FROM AppModels")
            .fetch_all(pool)
            .await?;

        let mut result = Vec::new();
        for (app_id, time, date) in rows {
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
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_this_week_log_list");
        let now = Local::now().date_naive();
        let (week_start, week_end) = get_week_range(now, false);
        Self::get_date_range_log_list(pool, week_start, week_end, -1, -1).await
    }

    pub async fn get_last_week_log_list(
        pool: &SqlitePool,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_last_week_log_list");
        let now = Local::now().date_naive();
        let (week_start, week_end) = get_week_range(now, true);
        Self::get_date_range_log_list(pool, week_start, week_end, -1, -1).await
    }

    pub async fn get_process_month_log_list(
        pool: &SqlitePool,
        app_id: i64,
        month: NaiveDate,
    ) -> Result<Vec<DailyLogModel>, AppError> {
        debug!("get_process_month_log_list: app_id={} month={}", app_id, month);
        let logs: Vec<DailyLogModel> = sqlx::query_as(
            r#"
            SELECT * FROM DailyLogModels
            WHERE strftime('%Y', Date) = ? AND strftime('%m', Date) = ? AND AppModelID = ?
            "#,
        )
        .bind(month.year().to_string())
        .bind(format!("{:02}", month.month()))
        .bind(app_id)
        .fetch_all(pool)
        .await?;
        Ok(logs)
    }

    pub async fn get_process_day(
        pool: &SqlitePool,
        app_id: i64,
        day: NaiveDate,
    ) -> Result<Option<DailyLogModel>, AppError> {
        debug!("get_process_day: app_id={} day={}", app_id, day);
        let log: Option<DailyLogModel> = sqlx::query_as(
            "SELECT * FROM DailyLogModels WHERE AppModelID = ? AND Date = ?",
        )
        .bind(app_id)
        .bind(day.to_string())
        .fetch_optional(pool)
        .await?;
        Ok(log)
    }

    pub async fn clear_app_data(
        pool: &SqlitePool,
        app_id: i64,
        month: Option<NaiveDate>,
    ) -> Result<(), AppError> {
        info!("clear_app_data: app_id={} month={:?}", app_id, month);
        if let Some(month) = month {
            sqlx::query(
                r#"
                DELETE FROM DailyLogModels
                WHERE AppModelID = ? AND strftime('%Y', Date) = ? AND strftime('%m', Date) = ?
                "#,
            )
            .bind(app_id)
            .bind(month.year().to_string())
            .bind(format!("{:02}", month.month()))
            .execute(pool)
            .await?;

            // 按月删除 HoursLogModels：先把本地月份范围转 UTC，再删除
            let month_start = NaiveDate::from_ymd_opt(month.year(), month.month(), 1).unwrap();
            let month_end_day = last_day_of_month(month.year(), month.month());
            let month_end = NaiveDate::from_ymd_opt(month.year(), month.month(), month_end_day).unwrap();
            let utc_start = local_naive_to_utc(NaiveDateTime::new(month_start, chrono::NaiveTime::from_hms_opt(0,0,0).unwrap()));
            let utc_end = local_naive_to_utc(NaiveDateTime::new(month_end, chrono::NaiveTime::from_hms_opt(23,59,59).unwrap()));

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

            sqlx::query("UPDATE AppModels SET TotalTime = 0 WHERE ID = ?")
                .bind(app_id)
                .execute(pool)
                .await?;
        }

        Ok(())
    }

    pub async fn clear_range(
        pool: &SqlitePool,
        start: NaiveDate,
        end: NaiveDate,
    ) -> Result<(), AppError> {
        info!("clear_range: start={} end={}", start, end);
        let days_in_month = last_day_of_month(end.year(), end.month());
        let start_dt = NaiveDateTime::new(
            NaiveDate::from_ymd_opt(start.year(), start.month(), 1).unwrap(),
            chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
        );
        let end_dt = NaiveDateTime::new(
            NaiveDate::from_ymd_opt(end.year(), end.month(), days_in_month).unwrap(),
            chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap(),
        );

        sqlx::query("DELETE FROM DailyLogModels WHERE Date >= ? AND Date <= ?")
            .bind(start_dt.date().to_string())
            .bind(end_dt.date().to_string())
            .execute(pool)
            .await?;

        let utc_start = local_naive_to_utc(start_dt);
        let utc_end = local_naive_to_utc(end_dt);
        sqlx::query("DELETE FROM HoursLogModels WHERE DataTime >= ? AND DataTime <= ?")
            .bind(utc_start)
            .bind(utc_end)
            .execute(pool)
            .await?;

        Ok(())
    }

    pub async fn get_time_range_log_list(
        pool: &SqlitePool,
        time: NaiveDateTime,
    ) -> Result<Vec<HoursLogModel>, AppError> {
        debug!("get_time_range_log_list: time={}", time);
        let local_hour = NaiveDateTime::new(
            time.date(),
            chrono::NaiveTime::from_hms_opt(time.hour() as u32, 0, 0).unwrap(),
        );
        let utc_hour = local_naive_to_utc(local_hour);

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
    ) -> Result<Vec<f64>, AppError> {
        debug!("get_range_total_data: start={} end={}", start, end);
        if start == end {
            let day_start = NaiveDateTime::new(
                start,
                chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
            );
            let day_end = NaiveDateTime::new(
                start,
                chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap(),
            );
            let utc_start = local_naive_to_utc(day_start);
            let utc_end = local_naive_to_utc(day_end);

            let rows: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
                r#"
                SELECT DataTime as data_time, SUM(Time) as total
                FROM HoursLogModels
                WHERE DataTime >= ? AND DataTime <= ?
                GROUP BY DataTime
                "#,
            )
            .bind(utc_start)
            .bind(utc_end)
            .fetch_all(pool)
            .await?;

            let mut result = vec![0.0; 24];
            for (utc_dt, total) in rows {
                let local_hour = utc_dt.with_timezone(&Local).hour() as usize;
                if local_hour < 24 {
                    result[local_hour] += total as f64;
                }
            }
            Ok(result)
        } else {
            let days_count = (end - start).num_days() + 1;
            let rows: Vec<(NaiveDate, i64)> = sqlx::query_as(
                r#"
                SELECT Date as date, SUM(Time) as total
                FROM DailyLogModels
                WHERE Date >= ? AND Date <= ?
                GROUP BY Date
                "#,
            )
            .bind(start.to_string())
            .bind(end.to_string())
            .fetch_all(pool)
            .await?;

            let map: std::collections::HashMap<_, _> = rows.into_iter().collect();
            let mut result = Vec::with_capacity(days_count as usize);
            for i in 0..days_count {
                let date = start + Duration::days(i);
                result.push(map.get(&date).copied().unwrap_or(0) as f64);
            }
            Ok(result)
        }
    }

    pub async fn get_month_total_data(
        pool: &SqlitePool,
        year: NaiveDate,
    ) -> Result<Vec<f64>, AppError> {
        debug!("get_month_total_data: year={}", year);
        let year_start = NaiveDate::from_ymd_opt(year.year(), 1, 1).unwrap();
        let year_end = NaiveDate::from_ymd_opt(year.year(), 12, 31).unwrap();

        let rows: Vec<(NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT Date as date, SUM(Time) as total
            FROM DailyLogModels
            WHERE Date >= ? AND Date <= ?
            GROUP BY Date
            "#,
        )
        .bind(year_start.to_string())
        .bind(year_end.to_string())
        .fetch_all(pool)
        .await?;

        let mut result = vec![0.0; 12];
        for i in 1..=12 {
            let month_start = NaiveDate::from_ymd_opt(year.year(), i, 1).unwrap();
            let month_end = last_day_of_month(year.year(), i);
            let month_end = NaiveDate::from_ymd_opt(year.year(), i, month_end).unwrap();

            let total: i64 = rows
                .iter()
                .filter(|(d, _)| *d >= month_start && *d <= month_end)
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
    ) -> Result<i64, AppError> {
        debug!("get_date_range_app_count: start={} end={}", start, end);
        let count: (i64,) = sqlx::query_as(
            r#"
            SELECT COUNT(DISTINCT AppModelID) FROM DailyLogModels
            WHERE Date >= ? AND Date <= ? AND AppModelID != 0
            "#,
        )
        .bind(start.to_string())
        .bind(end.to_string())
        .fetch_one(pool)
        .await?;
        Ok(count.0)
    }

    pub async fn get_category_hours_data(
        pool: &SqlitePool,
        date: NaiveDate,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_category_hours_data: date={}", date);
        let start = NaiveDateTime::new(
            date,
            chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
        );
        let end = NaiveDateTime::new(
            date,
            chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap(),
        );
        let utc_start = local_naive_to_utc(start);
        let utc_end = local_naive_to_utc(end);

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
            WHERE h.DataTime >= ? AND h.DataTime <= ?
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
            let utc_hour = local_naive_to_utc(local_hour);
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
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_category_range_data: start={} end={}", start, end);
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
        .bind(start.to_string())
        .bind(end.to_string())
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
        .bind(start.to_string())
        .bind(end.to_string())
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

        for i in 0..days {
            let date = start + Duration::days(i);
            for (cat_id, _) in &categories {
                let total = data
                    .iter()
                    .filter(|(cid, d, _)| *cid == *cat_id && *d == date)
                    .map(|(_, _, t)| *t)
                    .next()
                    .unwrap_or(0);

                if let Some(item) = list.iter_mut().find(|x| x.category_id == Some(*cat_id)) {
                    item.values[i as usize] = total as f64;
                }
            }
        }

        Ok(list)
    }

    pub async fn get_category_year_data(
        pool: &SqlitePool,
        date: NaiveDate,
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_category_year_data: date={}", date);
        let year_start = NaiveDate::from_ymd_opt(date.year(), 1, 1).unwrap();
        let year_end = NaiveDate::from_ymd_opt(date.year(), 12, 31).unwrap();

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
        .bind(year_start.to_string())
        .bind(year_end.to_string())
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
        .bind(year_start.to_string())
        .bind(year_end.to_string())
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
                    .filter(|(cid, d, _)| {
                        *cid == *cat_id && *d >= month_start && *d <= month_end
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
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_app_day_data: app_id={} date={}", app_id, date);
        let start = NaiveDateTime::new(
            date,
            chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
        );
        let end = NaiveDateTime::new(
            date,
            chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap(),
        );
        let utc_start = local_naive_to_utc(start);
        let utc_end = local_naive_to_utc(end);

        let data: Vec<(DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT DataTime as data_time, SUM(Time) as total
            FROM HoursLogModels
            WHERE AppModelID = ? AND DataTime >= ? AND DataTime <= ?
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
            let utc_hour = local_naive_to_utc(local_hour);
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
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_app_range_data: app_id={} start={} end={}", app_id, start, end);
        let data: Vec<(NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT Date as date, SUM(Time) as total
            FROM DailyLogModels
            WHERE AppModelID = ? AND Date >= ? AND Date <= ?
            GROUP BY Date
            "#,
        )
        .bind(app_id)
        .bind(start.to_string())
        .bind(end.to_string())
        .fetch_all(pool)
        .await?;

        let days = (end - start).num_days() + 1;
        let mut values = vec![0.0; days as usize];
        for i in 0..days {
            let date = start + Duration::days(i);
            let total = data.iter().find(|(d, _)| *d == date).map(|(_, t)| *t).unwrap_or(0);
            values[i as usize] = total as f64;
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
    ) -> Result<Vec<ColumnDataModel>, AppError> {
        debug!("get_app_year_data: app_id={} date={}", app_id, date);
        let year_start = NaiveDate::from_ymd_opt(date.year(), 1, 1).unwrap();
        let year_end = NaiveDate::from_ymd_opt(date.year(), 12, 31).unwrap();

        let data: Vec<(NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT Date as date, SUM(Time) as total
            FROM DailyLogModels
            WHERE AppModelID = ? AND Date >= ? AND Date <= ?
            GROUP BY Date
            "#,
        )
        .bind(app_id)
        .bind(year_start.to_string())
        .bind(year_end.to_string())
        .fetch_all(pool)
        .await?;

        let mut values = vec![0.0; 12];
        for i in 1..=12 {
            let month_start = NaiveDate::from_ymd_opt(date.year(), i, 1).unwrap();
            let month_end_day = last_day_of_month(date.year(), i);
            let month_end = NaiveDate::from_ymd_opt(date.year(), i, month_end_day).unwrap();

            let total: i64 = data
                .iter()
                .filter(|(d, _)| *d >= month_start && *d <= month_end)
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
    ) -> Result<ExportDataResult, AppError> {
        info!("get_export_data: start={} end={}", start, end);
        let start_dt = NaiveDateTime::new(
            start,
            chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
        );
        let end_dt = NaiveDateTime::new(
            end,
            chrono::NaiveTime::from_hms_opt(23, 59, 59).unwrap(),
        );
        let utc_start = local_naive_to_utc(start_dt);
        let utc_end = local_naive_to_utc(end_dt);

        let daily_logs: Vec<DailyLogModel> = sqlx::query_as(
            r#"
            SELECT d.* FROM DailyLogModels d
            WHERE d.Date >= ? AND d.Date <= ?
            "#,
        )
        .bind(start.to_string())
        .bind(end.to_string())
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

fn last_day_of_month(year: i32, month: u32) -> u32 {
    match month {
        1 | 3 | 5 | 7 | 8 | 10 | 12 => 31,
        4 | 6 | 9 | 11 => 30,
        2 => {
            if (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0) {
                29
            } else {
                28
            }
        }
        _ => 30,
    }
}

fn local_naive_to_utc(naive: NaiveDateTime) -> DateTime<Utc> {
    Local.from_local_datetime(&naive).unwrap().with_timezone(&Utc)
}
