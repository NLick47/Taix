use chrono::{DateTime, Datelike, Duration, Local, NaiveDate, NaiveDateTime, TimeZone, Timelike, Utc};
use sqlx::SqlitePool;
use std::collections::HashMap;
use tracing::{debug, info};

use crate::error::AppError;
use crate::models::request::UpdateAppDurationRequest;
use crate::services::category::CategoryService;

pub struct AppTimerService;

impl AppTimerService {
    pub async fn update_app_duration(
        pool: &SqlitePool,
        req: UpdateAppDurationRequest,
    ) -> Result<(), AppError> {
        let process_ = req.process_name;
        let duration_ = req.duration;
        let start_time_ = req.start_date_time;

        if process_.is_empty() || duration_ <= 0 {
            debug!("update_app_duration: skip empty process or zero duration");
            return Ok(());
        }

        // 过滤掉明显无效的时间（如 C# DateTime.MinValue 或 Unix 纪元）
        if start_time_.year() < 2000 {
            debug!("update_app_duration: skip invalid timestamp");
            return Ok(());
        }

        debug!(
            "update_app_duration: process={}, duration={}, start={}",
            process_, duration_, start_time_
        );

        let start_time_max_hours_duration =
            (59 - start_time_.minute() as i64) * 60 + (60 - start_time_.second() as i64);
        let start_time_hours_duration =
            if duration_ > start_time_max_hours_duration {
                start_time_max_hours_duration
            } else {
                duration_
            };
        let out_hours_duration = duration_ - start_time_hours_duration;

        // 本地小时整点，后续转换为 UTC 存入数据库
        let local_hour_start = NaiveDateTime::new(
            start_time_.date(),
            chrono::NaiveTime::from_hms_opt(start_time_.hour() as u32, 0, 0).unwrap(),
        );
        let mut end_local_hour = local_hour_start;
        let mut duration_hours_data: HashMap<DateTime<Utc>, i64> = HashMap::new();
        duration_hours_data.insert(local_naive_to_utc(local_hour_start), start_time_hours_duration);

        if out_hours_duration > 0 {
            let out_hours = out_hours_duration / 3600;
            let mut out_start_time = local_hour_start;
            for _ in 0..out_hours {
                out_start_time += Duration::hours(1);
                duration_hours_data.insert(local_naive_to_utc(out_start_time), 3600);
            }
            if out_hours_duration % 3600 > 0 {
                out_start_time += Duration::hours(1);
                let duration = out_hours_duration - out_hours * 3600;
                duration_hours_data.insert(local_naive_to_utc(out_start_time), duration);
            }
            end_local_hour = out_start_time;
        }

        let next_day_time = NaiveDateTime::new(
            NaiveDate::from_ymd_opt(start_time_.year(), start_time_.month(), start_time_.day()).unwrap(),
            chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
        ) + Duration::days(1);
        let start_time_max_day_duration = next_day_time.signed_duration_since(start_time_).num_seconds();
        let start_time_day_duration = if duration_ > start_time_max_day_duration {
            start_time_max_day_duration
        } else {
            duration_
        };
        let out_day_duration = duration_ - start_time_day_duration;
        let mut end_day_time = start_time_.date();
        let mut duration_day_data = HashMap::new();
        duration_day_data.insert(start_time_.date(), start_time_day_duration);

        if out_day_duration > 0 {
            let out_days = out_day_duration / 86400;
            let mut out_start_time = NaiveDateTime::new(
                NaiveDate::from_ymd_opt(start_time_.year(), start_time_.month(), start_time_.day()).unwrap(),
                chrono::NaiveTime::from_hms_opt(0, 0, 0).unwrap(),
            );
            for _ in 0..out_days {
                out_start_time += Duration::days(1);
                duration_day_data.insert(out_start_time.date(), 86400);
            }
            if out_day_duration % 86400 > 0 {
                out_start_time += Duration::days(1);
                let duration = out_day_duration - out_days * 86400;
                duration_day_data.insert(out_start_time.date(), duration);
            }
            end_day_time = out_start_time.date();
        }

        let app_id = match sqlx::query_as::<_, (i64,)>("SELECT ID FROM AppModels WHERE Name = ?")
            .bind(&process_)
            .fetch_optional(pool)
            .await?
        {
            Some((id,)) => {
                // 如果 File、IconFile 或 Description 为空，则用新上报的值填充
                if req.file.is_some() || req.icon_file.is_some() || req.description.is_some() {
                    sqlx::query(
                        "UPDATE AppModels SET File = COALESCE(File, ?), IconFile = COALESCE(IconFile, ?), Description = COALESCE(Description, ?) WHERE ID = ? AND (File IS NULL OR IconFile IS NULL OR Description IS NULL)",
                    )
                    .bind(&req.file)
                    .bind(&req.icon_file)
                    .bind(&req.description)
                    .bind(id)
                    .execute(pool)
                    .await?;
                }
                id
            }
            None => {
                info!("update_app_duration: auto-creating AppModel for '{}'", process_);
                let default_category_id = CategoryService::get_system_category_id(pool).await?;
                let result = sqlx::query(
                    "INSERT INTO AppModels (Name, CategoryID, TotalTime, File, IconFile, Description) VALUES (?, ?, 0, ?, ?, ?)",
                )
                .bind(&process_)
                .bind(default_category_id)
                .bind(&req.file)
                .bind(&req.icon_file)
                .bind(&req.description)
                .execute(pool)
                .await?;
                result.last_insert_rowid()
            }
        };

        sqlx::query("UPDATE AppModels SET TotalTime = TotalTime + ? WHERE ID = ?")
            .bind(duration_)
            .bind(app_id)
            .execute(pool)
            .await?;

        let daily_logs: Vec<(i64, NaiveDate, i64)> = sqlx::query_as(
            r#"
            SELECT ID, Date, Time FROM DailyLogModels
            WHERE Date >= ? AND Date <= ? AND AppModelID = ?
            "#,
        )
        .bind(start_time_.date().to_string())
        .bind(end_day_time.to_string())
        .bind(app_id)
        .fetch_all(pool)
        .await?;

        for (date_key, duration_val) in &duration_day_data {
            let date_str = date_key.to_string();
            let existing = daily_logs.iter().find(|(_, d, _)| *d == *date_key);

            if let Some((id, _, time)) = existing {
                let new_time = time + duration_val;
                let capped = if new_time > 86400 { 86400 } else { new_time };
                sqlx::query("UPDATE DailyLogModels SET Time = ? WHERE ID = ?")
                    .bind(capped)
                    .bind(id)
                    .execute(pool)
                    .await?;
            } else {
                sqlx::query(
                    r#"
                    INSERT INTO DailyLogModels (Date, AppModelID, Time)
                    VALUES (?, ?, ?)
                    "#,
                )
                .bind(&date_str)
                .bind(app_id)
                .bind(*duration_val)
                .execute(pool)
                .await?;
            }
        }

        let utc_start = local_naive_to_utc(local_hour_start);
        let utc_end = local_naive_to_utc(end_local_hour);
        let hours_logs: Vec<(i64, DateTime<Utc>, i64)> = sqlx::query_as(
            r#"
            SELECT ID, DataTime, Time FROM HoursLogModels
            WHERE DataTime >= ? AND DataTime <= ? AND AppModelID = ?
            "#,
        )
        .bind(utc_start)
        .bind(utc_end)
        .bind(app_id)
        .fetch_all(pool)
        .await?;

        for (dt_key, duration_val) in &duration_hours_data {
            let existing = hours_logs.iter().find(|(_, t, _)| *t == *dt_key);

            if let Some((id, _, time)) = existing {
                let new_time = time + duration_val;
                let capped = if new_time > 3600 { 3600 } else { new_time };
                sqlx::query("UPDATE HoursLogModels SET Time = ? WHERE ID = ?")
                    .bind(capped)
                    .bind(id)
                    .execute(pool)
                    .await?;
            } else {
                sqlx::query(
                    r#"
                    INSERT INTO HoursLogModels (DataTime, AppModelID, Time)
                    VALUES (?, ?, ?)
                    "#,
                )
                .bind(*dt_key)
                .bind(app_id)
                .bind(*duration_val)
                .execute(pool)
                .await?;
            }
        }

        Ok(())
    }
}

fn local_naive_to_utc(naive: NaiveDateTime) -> DateTime<Utc> {
    Local.from_local_datetime(&naive).unwrap().with_timezone(&Utc)
}
