use crate::migrations;
use sqlx::sqlite::{SqliteConnectOptions, SqliteJournalMode, SqlitePoolOptions};
use sqlx::SqlitePool;
use std::path::Path;
use std::str::FromStr;
use tracing::{info, warn};

pub(crate) const DEFAULT_CATEGORY: &str = "未分类";
pub(crate) const DEFAULT_ICON: &str = "avares://Taix/Resources/Icons/tai.ico";
pub(crate) const DEFAULT_COLOR: &str = "#e4e3df";

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
    let needs_backup = migrations::has_pending_migrations(&pool).await;

    if needs_backup && path.exists() && tokio::fs::metadata(path).await?.len() > 0 {
        // WAL 模式下未 checkpoint 的数据仍在 data.db-wal 中，直接复制主文件会丢失最近写入
        // 备份前先同步 WAL 到主库，保证备份完整
        if let Err(e) = sqlx::query("PRAGMA wal_checkpoint(TRUNCATE)")
            .execute(&pool)
            .await
        {
            warn!("备份前 WAL checkpoint 失败，备份可能不完整: {}", e);
        }

        let backup = format!(
            "{}.backup.{}",
            db_path,
            chrono::Utc::now().format("%Y%m%d_%H%M%S")
        );
        tokio::fs::copy(db_path, &backup).await?;
        info!("旧数据库已备份至: {}", backup);
    }

    migrations::run(&pool, tz_id).await?;
    pool.close().await;

    let opts = SqliteConnectOptions::from_str(&format!("sqlite:{}", db_path))?
        .journal_mode(SqliteJournalMode::Wal);
    let pool = SqlitePoolOptions::new()
        .max_connections(4)
        .connect_with(opts)
        .await?;

    Ok(pool)
}
