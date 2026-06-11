use crate::constants;
use anyhow::Context;
use chrono::TimeZone;
use sqlx::sqlite::{SqliteConnectOptions, SqliteJournalMode, SqlitePoolOptions};
use sqlx::SqlitePool;
use std::path::Path;
use std::str::FromStr;
use tracing::{info, warn};

pub(crate) const DEFAULT_CATEGORY: &str = "未分类";
pub(crate) const DEFAULT_ICON: &str = "avares://Taix/Resources/Icons/tai.ico";
pub(crate) const DEFAULT_COLOR: &str = "#e4e3df";

const BASELINE_SQL: &str = r#"
CREATE TABLE IF NOT EXISTS CategoryModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    IconFile TEXT,
    Color TEXT,
    IsDirectoryMatch INTEGER NOT NULL DEFAULT 0,
    Directories TEXT,
    IsSystem INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS AppModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Alias TEXT,
    Description TEXT,
    File TEXT,
    CategoryID INTEGER NOT NULL DEFAULT 0,
    IconFile TEXT,
    TotalTime INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (CategoryID) REFERENCES CategoryModels(ID)
);

CREATE TABLE IF NOT EXISTS DailyLogModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Date TEXT NOT NULL,
    AppModelID INTEGER NOT NULL,
    Time INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS HoursLogModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    DataTime TEXT NOT NULL,
    AppModelID INTEGER NOT NULL,
    Time INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS WebSiteCategoryModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    IconFile TEXT,
    Color TEXT,
    IsSystem INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS WebSiteModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT,
    Domain TEXT NOT NULL,
    Alias TEXT,
    CategoryID INTEGER NOT NULL DEFAULT 0,
    IconFile TEXT,
    Duration INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS WebUrlModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Title TEXT,
    Url TEXT NOT NULL,
    IconFile TEXT
);

CREATE TABLE IF NOT EXISTS WebBrowseLogModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    UrlId INTEGER NOT NULL,
    LogTime TEXT NOT NULL,
    Duration INTEGER NOT NULL DEFAULT 0,
    SiteId INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS AppDurationRequests (
    ProcessName TEXT NOT NULL,
    StartDateTime TEXT NOT NULL,
    Duration INTEGER NOT NULL,
    CreatedAt TEXT NOT NULL DEFAULT (datetime('now')),
    PRIMARY KEY (ProcessName, StartDateTime)
);

CREATE TABLE IF NOT EXISTS AppSessions (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    AppModelID INTEGER NOT NULL,
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    Duration INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_appsessions_start ON AppSessions(StartTime);
CREATE INDEX IF NOT EXISTS idx_appsessions_app_start ON AppSessions(AppModelID, StartTime);

CREATE INDEX IF NOT EXISTS idx_appmodels_name ON AppModels(Name);
CREATE INDEX IF NOT EXISTS idx_appmodels_categoryid ON AppModels(CategoryID);
CREATE INDEX IF NOT EXISTS idx_websitemodels_domain ON WebSiteModels(Domain);
CREATE INDEX IF NOT EXISTS idx_websitemodels_categoryid ON WebSiteModels(CategoryID);
CREATE INDEX IF NOT EXISTS idx_weburlmodels_url ON WebUrlModels(Url);
CREATE INDEX IF NOT EXISTS idx_webbrowselog_logtime_siteid ON WebBrowseLogModels(LogTime, SiteId);
"#;

const DATA_FIXES_SQL: &str = r#"
UPDATE AppModels SET CategoryID = COALESCE((SELECT ID FROM CategoryModels WHERE IsSystem = 1 LIMIT 1), 0) WHERE CategoryID = 0;
UPDATE WebSiteModels SET CategoryID = COALESCE((SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1), 0) WHERE CategoryID = 0;
UPDATE DailyLogModels SET Date = substr(Date, 1, 10) WHERE length(Date) > 10;
UPDATE WebSiteModels SET IconFile = SUBSTR(IconFile, INSTR(IconFile, 'WebFavicons' || char(92))) WHERE IconFile LIKE '%' || char(92) || 'WebFavicons' || char(92) || '%';
UPDATE WebUrlModels SET IconFile = SUBSTR(IconFile, INSTR(IconFile, 'WebFavicons' || char(92))) WHERE IconFile LIKE '%' || char(92) || 'WebFavicons' || char(92) || '%';
UPDATE WebSiteModels SET IconFile = 'WebFavicons' || char(92) || IconFile WHERE IconFile IS NOT NULL AND IconFile != '' AND IconFile NOT LIKE 'WebFavicons%';
UPDATE WebUrlModels SET IconFile = 'WebFavicons' || char(92) || IconFile WHERE IconFile IS NOT NULL AND IconFile != '' AND IconFile NOT LIKE 'WebFavicons%';
UPDATE AppModels SET IconFile = SUBSTR(IconFile, INSTR(IconFile, 'AppIcons' || char(92))) WHERE IconFile LIKE '%' || char(92) || 'AppIcons' || char(92) || '%';
"#;

const INDEXES_SQL: &str = r#"
DROP INDEX IF EXISTS idx_dailylog_date_appid;
DELETE FROM DailyLogModels WHERE EXISTS (
    SELECT 1 FROM DailyLogModels AS d2
    WHERE d2.Date = DailyLogModels.Date
    AND d2.AppModelID = DailyLogModels.AppModelID
    AND d2.ID < DailyLogModels.ID
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_dailylog_date_appid ON DailyLogModels(Date, AppModelID);

DROP INDEX IF EXISTS idx_hourslog_appid_datetime;
DELETE FROM HoursLogModels WHERE EXISTS (
    SELECT 1 FROM HoursLogModels AS h2
    WHERE h2.DataTime = HoursLogModels.DataTime
    AND h2.AppModelID = HoursLogModels.AppModelID
    AND h2.ID < HoursLogModels.ID
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_hourslog_appid_datetime ON HoursLogModels(AppModelID, DataTime);

DELETE FROM WebBrowseLogModels WHERE EXISTS (
    SELECT 1 FROM WebBrowseLogModels AS w2
    WHERE w2.LogTime = WebBrowseLogModels.LogTime
    AND w2.UrlId = WebBrowseLogModels.UrlId
    AND w2.ID < WebBrowseLogModels.ID
);
CREATE UNIQUE INDEX IF NOT EXISTS idx_webbrowselog_logtime_urlid ON WebBrowseLogModels(LogTime, UrlId);
"#;

const PERF_INDEXES_SQL: &str = r#"
CREATE INDEX IF NOT EXISTS idx_hourslog_datetime ON HoursLogModels(DataTime);
CREATE INDEX IF NOT EXISTS idx_webbrowselog_siteid ON WebBrowseLogModels(SiteId);
ANALYZE;
"#;

/// 初始化数据库连接并执行迁移
/// tz_id: C# 历史数据使用的本地时区，如 "Asia/Shanghai"
pub async fn init_db(db_path: &str, tz_id: &str) -> anyhow::Result<SqlitePool> {
    let path = Path::new(db_path);
    if let Some(parent) = path.parent() {
        tokio::fs::create_dir_all(parent).await?;
    }

    let opts = SqliteConnectOptions::from_str(&format!("sqlite:{}", db_path))?.create_if_missing(true);
    let pool = SqlitePoolOptions::new()
        .max_connections(1)
        .connect_with(opts)
        .await?;

    // 检查是否有待执行迁移，只有在有迁移时才备份
    let needs_backup = {
        let table_exists: Option<(i64,)> = sqlx::query_as(
            "SELECT 1 FROM sqlite_master WHERE type='table' AND name='_migrations'"
        )
        .fetch_optional(&pool)
        .await?;

        if table_exists.is_none() {
            false // 全新数据库，不需要备份
        } else {
            let steps = [
                "baseline", "add_columns", "rename_columns", "data_fixes", "indexes",
                "utc_hourslog", "utc_webbrowse", "utc_appduration", "utc_daily", "utc_daily_rebuild", "defaults",
                "perf_indexes", "app_sessions",
            ];
            let mut has_pending = false;
            for step in steps {
                let row: Option<(i64,)> = sqlx::query_as("SELECT 1 FROM _migrations WHERE step = ?")
                    .bind(step)
                    .fetch_optional(&pool)
                    .await?;
                if row.is_none() {
                    has_pending = true;
                    break;
                }
            }
            has_pending
        }
    };

    if needs_backup && path.exists() && tokio::fs::metadata(path).await?.len() > 0 {
        let backup = format!(
            "{}.backup.{}",
            db_path,
            chrono::Utc::now().format("%Y%m%d_%H%M%S")
        );
        tokio::fs::copy(db_path, &backup).await?;
        info!("旧数据库已备份至: {}", backup);
    }

    migrate(&pool, tz_id).await?;
    pool.close().await;

    let opts = SqliteConnectOptions::from_str(&format!("sqlite:{}", db_path))?
        .journal_mode(SqliteJournalMode::Wal);
    let pool = SqlitePoolOptions::new()
        .max_connections(4)
        .connect_with(opts)
        .await?;

    Ok(pool)
}

async fn migrate(pool: &SqlitePool, tz_id: &str) -> anyhow::Result<()> {
    let mut tx = pool.begin().await.context("开启迁移事务失败")?;

    sqlx::query(
        "CREATE TABLE IF NOT EXISTS _migrations (
            step TEXT PRIMARY KEY,
            executed_at TEXT NOT NULL DEFAULT (datetime('now'))
        )"
    )
    .execute(&mut *tx)
    .await?;

    if !is_done(&mut tx, "baseline").await? {
        execute_batch(&mut tx, BASELINE_SQL).await.context("基线表结构")?;
        mark_done(&mut tx, "baseline").await?;
    }

    if !is_done(&mut tx, "add_columns").await? {
        add_column_if_missing(&mut tx, "CategoryModels", "IsSystem", "INTEGER NOT NULL DEFAULT 0").await?;
        add_column_if_missing(&mut tx, "WebSiteCategoryModels", "IsSystem", "INTEGER NOT NULL DEFAULT 0").await?;
        mark_done(&mut tx, "add_columns").await?;
    }

    if !is_done(&mut tx, "rename_columns").await? {
        rename_column_if_exists(&mut tx, "CategoryModels", "IsDirectoryMath", "IsDirectoryMatch").await?;
        mark_done(&mut tx, "rename_columns").await?;
    }

    if !is_done(&mut tx, "data_fixes").await? {
        execute_batch(&mut tx, DATA_FIXES_SQL).await.context("数据修复")?;
        mark_done(&mut tx, "data_fixes").await?;
    }

    if !is_done(&mut tx, "indexes").await? {
        execute_batch(&mut tx, INDEXES_SQL).await.context("索引重建")?;
        mark_done(&mut tx, "indexes").await?;
    }

    if !is_done(&mut tx, "utc_hourslog").await? {
        convert_local_to_utc(&mut tx, "HoursLogModels", "DataTime", "ID", tz_id).await?;
        mark_done(&mut tx, "utc_hourslog").await?;
    }

    if !is_done(&mut tx, "utc_webbrowse").await? {
        convert_local_to_utc(&mut tx, "WebBrowseLogModels", "LogTime", "ID", tz_id).await?;
        mark_done(&mut tx, "utc_webbrowse").await?;
    }

    if !is_done(&mut tx, "utc_appduration").await? {
        convert_local_to_utc(&mut tx, "AppDurationRequests", "StartDateTime", "rowid", tz_id).await?;
        mark_done(&mut tx, "utc_appduration").await?;
    }

    // DailyLogModels.Date 在 C# 时代为本地日期，直接按 00:00 转 UTC 会导致日期错位
    // 此处改为基于 HoursLogModels（已转为 UTC）按 UTC 日期重新聚合重建
    if !is_done(&mut tx, "utc_daily").await? {
        rebuild_daily_log_from_hours(&mut tx).await?;
        mark_done(&mut tx, "utc_daily").await?;
    }

    // 对已经执行过旧版 utc_daily 的数据库进行修复，重新从 HoursLog 重建
    if !is_done(&mut tx, "utc_daily_rebuild").await? {
        rebuild_daily_log_from_hours(&mut tx).await?;
        mark_done(&mut tx, "utc_daily_rebuild").await?;
    }

    if !is_done(&mut tx, "defaults").await? {
        ensure_default_categories(&mut tx).await?;
        mark_done(&mut tx, "defaults").await?;
    }

    if !is_done(&mut tx, "perf_indexes").await? {
        execute_batch(&mut tx, PERF_INDEXES_SQL).await.context("性能优化索引")?;
        mark_done(&mut tx, "perf_indexes").await?;
    }

    if !is_done(&mut tx, "app_sessions").await? {
        sqlx::query(
            r#"
            CREATE TABLE IF NOT EXISTS AppSessions (
                ID INTEGER PRIMARY KEY AUTOINCREMENT,
                AppModelID INTEGER NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NOT NULL,
                Duration INTEGER NOT NULL DEFAULT 0
            );
            CREATE INDEX IF NOT EXISTS idx_appsessions_start ON AppSessions(StartTime);
            CREATE INDEX IF NOT EXISTS idx_appsessions_app_start ON AppSessions(AppModelID, StartTime);
            "#
        )
        .execute(&mut *tx)
        .await
        .context("创建 AppSessions 表")?;
        mark_done(&mut tx, "app_sessions").await?;
    }

    tx.commit().await.context("提交迁移事务失败")?;
    info!("数据库迁移完成");
    Ok(())
}

async fn is_done(tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>, step: &str) -> anyhow::Result<bool> {
    let row: Option<(i64,)> = sqlx::query_as("SELECT 1 FROM _migrations WHERE step = ?")
        .bind(step)
        .fetch_optional(&mut **tx)
        .await?;
    Ok(row.is_some())
}

async fn mark_done(tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>, step: &str) -> anyhow::Result<()> {
    sqlx::query("INSERT OR IGNORE INTO _migrations (step) VALUES (?)")
        .bind(step)
        .execute(&mut **tx)
        .await?;
    Ok(())
}

async fn execute_batch(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
    sql: &str,
) -> anyhow::Result<()> {
    for stmt in sql.split(';').map(str::trim).filter(|s| !s.is_empty()) {
        sqlx::query(stmt)
            .execute(&mut **tx)
            .await
            .with_context(|| format!("SQL 执行失败: {}", stmt))?;
    }
    Ok(())
}

async fn add_column_if_missing(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
    table: &str,
    column: &str,
    definition: &str,
) -> anyhow::Result<()> {
    let exists = sqlx::query(
        "SELECT 1 FROM pragma_table_info(?) WHERE name = ?"
    )
    .bind(table)
    .bind(column)
    .fetch_optional(&mut **tx)
    .await?
    .is_some();

    if !exists {
        let sql = format!("ALTER TABLE {} ADD COLUMN {} {}", table, column, definition);
        sqlx::query(&sql).execute(&mut **tx).await?;
        info!("表 {} 添加列 {}", table, column);
    }
    Ok(())
}

async fn rename_column_if_exists(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
    table: &str,
    old_name: &str,
    new_name: &str,
) -> anyhow::Result<()> {
    let exists = sqlx::query(
        "SELECT 1 FROM pragma_table_info(?) WHERE name = ?"
    )
    .bind(table)
    .bind(old_name)
    .fetch_optional(&mut **tx)
    .await?
    .is_some();

    if exists {
        let sql = format!("ALTER TABLE {} RENAME COLUMN {} TO {}", table, old_name, new_name);
        sqlx::query(&sql).execute(&mut **tx).await?;
        info!("表 {} 列 {} -> {} 重命名完成", table, old_name, new_name);
    }
    Ok(())
}

/// 将本地时间字符串（2025-06-04 21:00:00）转为 UTC RFC 3339（2025-06-04T13:00:00+00:00）
/// 按 ID 分批处理，每批 1000 条，已含 T 的行自动跳过
async fn convert_local_to_utc(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
    table: &str,
    column: &str,
    id_column: &str,
    tz_id: &str,
) -> anyhow::Result<()> {
    let tz: chrono_tz::Tz = tz_id.parse()
        .map_err(|_| anyhow::anyhow!("无效时区: {}", tz_id))?;

    let utc_count: i64 = sqlx::query_scalar(
        &format!("SELECT COUNT(*) FROM {} WHERE {} LIKE '%T%'", table, column)
    )
    .fetch_one(&mut **tx)
    .await?;

    if utc_count > 0 {
        info!("表 {} 已包含 {} 条 UTC 格式数据，仅转换剩余旧格式", table, utc_count);
    }

    let total: i64 = sqlx::query_scalar(&format!("SELECT COUNT(*) FROM {}", table))
        .fetch_one(&mut **tx)
        .await?;

    if total == 0 {
        return Ok(());
    }

    info!("转换表 {}.{}（{} 条记录）...", table, column, total);

    let batch_size = constants::MIGRATION_BATCH_SIZE;
    let mut converted = 0;
    let mut last_id = 0i64;

    loop {
        let rows: Vec<(i64, String)> = sqlx::query_as(&format!(
            "SELECT {}, {} FROM {} WHERE {} > ? AND {} NOT LIKE '%T%' ORDER BY {} LIMIT {}",
            id_column, column, table, id_column, column, id_column, batch_size
        ))
        .bind(last_id)
        .fetch_all(&mut **tx)
        .await?;

        if rows.is_empty() {
            break;
        }

        let batch_count = rows.len();
        for (id, time_str) in rows {
            let naive = match chrono::NaiveDateTime::parse_from_str(&time_str, "%Y-%m-%d %H:%M:%S")
                .or_else(|_| chrono::NaiveDateTime::parse_from_str(&time_str, "%Y-%m-%d %H:%M:%S%.f"))
            {
                Ok(dt) => dt,
                Err(e) => {
                    warn!("表 {} 行 {} 时间 '{}' 解析失败: {}，跳过", table, id, time_str, e);
                    continue;
                }
            };

            let utc_str = match tz.from_local_datetime(&naive) {
                chrono::LocalResult::Single(dt) => {
                    dt.with_timezone(&chrono::Utc)
                        .to_rfc3339_opts(chrono::SecondsFormat::Secs, false)
                }
                chrono::LocalResult::Ambiguous(earliest, _) => {
                    earliest
                        .with_timezone(&chrono::Utc)
                        .to_rfc3339_opts(chrono::SecondsFormat::Secs, false)
                }
                chrono::LocalResult::None => {
                    warn!(
                        "表 {} 时间 '{}' 在目标时区 {} 不存在（DST 跳变），按 UTC 处理",
                        table, time_str, tz_id
                    );
                    chrono::Utc
                        .from_utc_datetime(&naive)
                        .to_rfc3339_opts(chrono::SecondsFormat::Secs, false)
                }
            };

            sqlx::query(&format!(
                "UPDATE {} SET {} = ? WHERE {} = ?",
                table, column, id_column
            ))
            .bind(&utc_str)
            .bind(id)
            .execute(&mut **tx)
            .await?;

            last_id = id;
        }

        converted += batch_count;
        info!("表 {} 已处理 {} / {} 条", table, converted, total);
    }

    info!("表 {} 转换完成: {} 条", table, converted);
    Ok(())
}

/// 从 HoursLogModels 按 UTC 日期聚合重建 DailyLogModels
/// 适用于 C# 旧数据迁移：HoursLog 已转为 UTC 后，按 UTC 日期重新生成 DailyLog
/// 该操作是幂等的
async fn rebuild_daily_log_from_hours(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
) -> anyhow::Result<()> {
    let before_count: i64 = sqlx::query_scalar("SELECT COUNT(*) FROM DailyLogModels")
        .fetch_one(&mut **tx)
        .await?;

    info!("从 HoursLog 重建 DailyLog（当前 {} 条）...", before_count);

    sqlx::query("DELETE FROM DailyLogModels")
        .execute(&mut **tx)
        .await?;

    let inserted = sqlx::query(
        r#"
        INSERT INTO DailyLogModels (Date, AppModelID, Time)
        SELECT substr(DataTime, 1, 10), AppModelID, SUM(Time)
        FROM HoursLogModels
        GROUP BY substr(DataTime, 1, 10), AppModelID
        "#
    )
    .execute(&mut **tx)
    .await?
    .rows_affected();

    info!("DailyLog 重建完成: 清理 {} 条旧记录，插入 {} 条新记录", before_count, inserted);
    Ok(())
}

async fn ensure_default_categories(
    tx: &mut sqlx::Transaction<'_, sqlx::Sqlite>,
) -> anyhow::Result<()> {
    sqlx::query(
        "INSERT OR IGNORE INTO CategoryModels (Name, IconFile, Color, Directories, IsDirectoryMatch, IsSystem) VALUES (?, ?, ?, '[]', 0, 1)"
    )
    .bind(DEFAULT_CATEGORY)
    .bind(DEFAULT_ICON)
    .bind(DEFAULT_COLOR)
    .execute(&mut **tx)
    .await?;

    sqlx::query(
        "INSERT OR IGNORE INTO WebSiteCategoryModels (Name, IconFile, Color, IsSystem) VALUES (?, ?, ?, 1)"
    )
    .bind(DEFAULT_CATEGORY)
    .bind(DEFAULT_ICON)
    .bind(DEFAULT_COLOR)
    .execute(&mut **tx)
    .await?;

    sqlx::query("UPDATE CategoryModels SET IsSystem = 1 WHERE Name = ? AND IsSystem = 0")
        .bind(DEFAULT_CATEGORY)
        .execute(&mut **tx)
        .await?;

    sqlx::query("UPDATE WebSiteCategoryModels SET IsSystem = 1 WHERE Name = ? AND IsSystem = 0")
        .bind(DEFAULT_CATEGORY)
        .execute(&mut **tx)
        .await?;

    Ok(())
}
