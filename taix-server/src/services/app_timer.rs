use chrono::{DateTime, Datelike, Duration, TimeZone, Timelike, Utc};
use sqlx::SqlitePool;
use std::collections::HashMap;
use std::time::Duration as StdDuration;
use tracing::{debug, info};

use crate::constants;
use crate::error::AppError;
use crate::models::request::UpdateAppDurationRequest;
use crate::services::category::CategoryService;
use crate::services::config::ConfigService;


const DEDUP_RETENTION_DAYS: i64 = 14;


const CLEANUP_INTERVAL: StdDuration = StdDuration::from_secs(constants::SECS_PER_DAY as u64);

pub struct AppTimerService;

impl AppTimerService {
    pub async fn update_app_duration(
        pool: &SqlitePool,
        req: UpdateAppDurationRequest,
        config_service: &ConfigService,
    ) -> Result<(), AppError> {
        let process_ = req.process_name;
        let duration_ = req.duration;
        let start_time_ = req.start_date_time;

        if process_.is_empty() || duration_ <= 0 {
            debug!("update_app_duration: skip empty process or zero duration");
            return Ok(());
        }

        // 应用忽略/白名单过滤
        if config_service.should_ignore_app(&process_, req.file.as_deref()).await {
            debug!("update_app_duration: app ignored by config: {}", process_);
            return Ok(());
        }

        // 过滤掉明显无效的时间
        if start_time_.year() < constants::MIN_VALID_YEAR {
            debug!("update_app_duration: skip invalid timestamp");
            return Ok(());
        }

        // 单次上报时长不超过 24 小时，防止异常膨胀数据库
        const MAX_DURATION_SECS: i64 = constants::SECS_PER_DAY;
        let mut duration_ = duration_;
        if duration_ > MAX_DURATION_SECS {
            tracing::warn!(
                "update_app_duration: duration {}s exceeds max {}s for {}, truncating",
                duration_, MAX_DURATION_SECS, process_
            );
            duration_ = MAX_DURATION_SECS;
        }

        let mut tx = pool.begin().await?;

        // 同一进程在同一开始时间的上报只处理一次（StartDateTime 统一为 UTC，RFC3339 格式）
        let start_time_key = start_time_.to_rfc3339_opts(chrono::SecondsFormat::Secs, true);
        let dedup_result = sqlx::query(
            r#"
            INSERT INTO AppDurationRequests (ProcessName, StartDateTime, Duration)
            VALUES (?, ?, ?)
            ON CONFLICT(ProcessName, StartDateTime) DO NOTHING
            "#,
        )
        .bind(&process_)
        .bind(&start_time_key)
        .bind(duration_)
        .execute(&mut *tx)
        .await?;

        if dedup_result.rows_affected() == 0 {
            debug!(
                "update_app_duration: duplicate request skipped for {}@{}",
                process_, start_time_key
            );
            tx.commit().await?;
            return Ok(());
        }

        debug!(
            "update_app_duration: process={}, duration={}, start={}",
            process_, duration_, start_time_
        );

        let start_time_max_hours_duration =
            (59 - start_time_.minute() as i64) * 60 + (60 - start_time_.second() as i64);
        let start_time_hours_duration = if duration_ > start_time_max_hours_duration {
            start_time_max_hours_duration
        } else {
            duration_
        };
        let out_hours_duration = duration_ - start_time_hours_duration;

        // UTC 小时整点，直接存入数据库
        let utc_hour_start = start_time_
            .with_minute(0)
            .unwrap()
            .with_second(0)
            .unwrap()
            .with_nanosecond(0)
            .unwrap();
        let mut duration_hours_data: HashMap<DateTime<Utc>, i64> = HashMap::new();
        duration_hours_data.insert(utc_hour_start, start_time_hours_duration);

        if out_hours_duration > 0 {
            let out_hours = out_hours_duration / 3600;
            for i in 1..=out_hours {
                let next_utc = utc_hour_start + Duration::hours(i);
                duration_hours_data.insert(next_utc, 3600);
            }
            if out_hours_duration % 3600 > 0 {
                let next_utc = utc_hour_start + Duration::hours(out_hours + 1);
                let duration = out_hours_duration - out_hours * 3600;
                duration_hours_data.insert(next_utc, duration);
            }
        }

        // DailyLog 按 UTC 日期汇总
        let utc_date = start_time_.date_naive();
        let utc_next_day_start = utc_date
            .succ_opt()
            .unwrap()
            .and_hms_opt(0, 0, 0)
            .unwrap();
        let start_time_max_day_duration = Utc.from_utc_datetime(&utc_next_day_start)
            .signed_duration_since(start_time_)
            .num_seconds();
        let start_time_day_duration = if duration_ > start_time_max_day_duration {
            start_time_max_day_duration
        } else {
            duration_
        };
        let out_day_duration = duration_ - start_time_day_duration;
        let mut duration_day_data = HashMap::new();
        duration_day_data.insert(utc_date, start_time_day_duration);

        if out_day_duration > 0 {
            let out_days = out_day_duration / 86400;
            let mut out_utc_date = utc_date;
            for _ in 0..out_days {
                out_utc_date = out_utc_date.succ_opt().unwrap();
                duration_day_data.insert(out_utc_date, 86400);
            }
            if out_day_duration % 86400 > 0 {
                out_utc_date = out_utc_date.succ_opt().unwrap();
                let duration = out_day_duration - out_days * 86400;
                duration_day_data.insert(out_utc_date, duration);
            }
        }

        let app_id = match sqlx::query_as::<_, (i64,)>("SELECT ID FROM AppModels WHERE Name = ?")
            .bind(&process_)
            .fetch_optional(&mut *tx)
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
                    .execute(&mut *tx)
                    .await?;
                }
                id
            }
            None => {
                info!("update_app_duration: auto-creating AppModel for '{}'", process_);
                let default_category_id = CategoryService::get_system_category_id(&mut *tx).await?;
                let result = sqlx::query(
                    "INSERT INTO AppModels (Name, Alias, CategoryID, TotalTime, File, IconFile, Description) VALUES (?, ?, ?, 0, ?, ?, ?)",
                )
                .bind(&process_)
                .bind(None::<&str>)
                .bind(default_category_id)
                .bind(&req.file)
                .bind(&req.icon_file)
                .bind(&req.description)
                .execute(&mut *tx)
                .await?;
                result.last_insert_rowid()
            }
        };

        sqlx::query("UPDATE AppModels SET TotalTime = TotalTime + ? WHERE ID = ?")
            .bind(duration_)
            .bind(app_id)
            .execute(&mut *tx)
            .await?;

        // DailyLog 使用 UPSERT，利用唯一索引 (Date, AppModelID) 保证并发安全
        for (date_key, duration_val) in &duration_day_data {
            let date_str = date_key.to_string();
            sqlx::query(
                r#"
                INSERT INTO DailyLogModels (Date, AppModelID, Time)
                VALUES (?, ?, ?)
                ON CONFLICT(Date, AppModelID)
                DO UPDATE SET Time = Time + excluded.Time
                "#,
            )
            .bind(&date_str)
            .bind(app_id)
            .bind(*duration_val)
            .execute(&mut *tx)
            .await?;
        }

        // HoursLog 使用 UPSERT，利用唯一索引 (AppModelID, DataTime) 保证并发安全
        // 同时封顶单小时 3600 秒，防止异常数据膨胀
        for (dt_key, duration_val) in &duration_hours_data {
            sqlx::query(
                r#"
                INSERT INTO HoursLogModels (DataTime, AppModelID, Time)
                VALUES (?, ?, ?)
                ON CONFLICT(AppModelID, DataTime)
                DO UPDATE SET Time = min(Time + excluded.Time, 3600)
                "#,
            )
            .bind(*dt_key)
            .bind(app_id)
            .bind(*duration_val)
            .execute(&mut *tx)
            .await?;
        }

        tx.commit().await?;

        Ok(())
    }


    async fn cleanup_expired_dedup_records(pool: &SqlitePool) -> Result<u64, sqlx::Error> {
        let result = sqlx::query(
            r#"
            DELETE FROM AppDurationRequests
            WHERE CreatedAt < datetime('now', '-' || ? || ' days')
            "#,
        )
        .bind(DEDUP_RETENTION_DAYS)
        .execute(pool)
        .await?;

        Ok(result.rows_affected())
    }

    // 首次 tick 立即触发，启动时先清一轮；Skip 避免休眠后连续补偿。
    pub async fn run_cleanup_task(pool: SqlitePool) {
        let mut interval = tokio::time::interval(CLEANUP_INTERVAL);
        interval.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);

        loop {
            interval.tick().await;

            match Self::cleanup_expired_dedup_records(&pool).await {
                Ok(0) => {
                    tracing::debug!("no expired AppDurationRequests records to clean up");
                }
                Ok(rows) => {
                    tracing::info!(deleted_rows = rows, "cleaned up expired AppDurationRequests");
                }
                Err(e) => {
                    tracing::error!(error = %e, "failed to clean up expired AppDurationRequests");
                }
            }
        }
    }
}
