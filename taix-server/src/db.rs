use sqlx::sqlite::{SqliteConnectOptions, SqliteJournalMode, SqlitePoolOptions};
use sqlx::SqlitePool;
use std::path::Path;
use std::str::FromStr;
use tracing::{info, warn};


const BASELINE_SQL: &str = r#"
CREATE TABLE IF NOT EXISTS CategoryModels (
    ID INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    IconFile TEXT,
    Color TEXT,
    IsDirectoryMath INTEGER NOT NULL DEFAULT 0,
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

CREATE INDEX IF NOT EXISTS idx_appmodels_name ON AppModels(Name);
CREATE INDEX IF NOT EXISTS idx_appmodels_categoryid ON AppModels(CategoryID);
CREATE INDEX IF NOT EXISTS idx_dailylog_date_appid ON DailyLogModels(Date, AppModelID);
CREATE INDEX IF NOT EXISTS idx_hourslog_appid_datetime ON HoursLogModels(AppModelID, DataTime);
CREATE INDEX IF NOT EXISTS idx_websitemodels_domain ON WebSiteModels(Domain);
CREATE INDEX IF NOT EXISTS idx_websitemodels_categoryid ON WebSiteModels(CategoryID);
CREATE INDEX IF NOT EXISTS idx_weburlmodels_url ON WebUrlModels(Url);
CREATE INDEX IF NOT EXISTS idx_webbrowselog_logtime_siteid ON WebBrowseLogModels(LogTime, SiteId);
"#;


struct Migration {
    version: i64,
    name: &'static str,
    sql: &'static str,
}

const MIGRATIONS: &[Migration] = &[
    Migration {
        version: 1,
        name: "add_isystem_columns",
        sql: r#"
            ALTER TABLE CategoryModels ADD COLUMN IsSystem INTEGER NOT NULL DEFAULT 0;
            ALTER TABLE WebSiteCategoryModels ADD COLUMN IsSystem INTEGER NOT NULL DEFAULT 0;
        "#,
    },
    Migration {
        version: 2,
        name: "ensure_default_categories",
        sql: r#"
            INSERT OR IGNORE INTO CategoryModels (Name, IconFile, Color, IsSystem) VALUES ('未分类', 'avares://Taix/Resources/Icons/tai.ico', '#808080', 1);
            INSERT OR IGNORE INTO WebSiteCategoryModels (Name, IconFile, Color, IsSystem) VALUES ('未分类', 'avares://Taix/Resources/Icons/tai.ico', '#808080', 1);
            UPDATE CategoryModels SET IsSystem = 1 WHERE Name = '未分类' AND IsSystem = 0;
            UPDATE WebSiteCategoryModels SET IsSystem = 1 WHERE Name = '未分类' AND IsSystem = 0;
        "#,
    },
    Migration {
        version: 3,
        name: "fix_appmodels_categoryid",
        sql: r#"
            UPDATE AppModels SET CategoryID = COALESCE((SELECT ID FROM CategoryModels WHERE IsSystem = 1 LIMIT 1), 0) WHERE CategoryID = 0;
        "#,
    },
    Migration {
        version: 4,
        name: "fix_dailylog_date",
        sql: r#"
            UPDATE DailyLogModels SET Date = substr(Date, 1, 10) WHERE length(Date) > 10;
        "#,
    },
    Migration {
        version: 5,
        name: "create_indexes",
        sql: r#"
            CREATE INDEX IF NOT EXISTS idx_appmodels_name ON AppModels(Name);
            CREATE INDEX IF NOT EXISTS idx_appmodels_categoryid ON AppModels(CategoryID);
            CREATE INDEX IF NOT EXISTS idx_dailylog_date_appid ON DailyLogModels(Date, AppModelID);
            CREATE INDEX IF NOT EXISTS idx_hourslog_appid_datetime ON HoursLogModels(AppModelID, DataTime);
            CREATE INDEX IF NOT EXISTS idx_websitemodels_domain ON WebSiteModels(Domain);
            CREATE INDEX IF NOT EXISTS idx_websitemodels_categoryid ON WebSiteModels(CategoryID);
            CREATE INDEX IF NOT EXISTS idx_weburlmodels_url ON WebUrlModels(Url);
            CREATE INDEX IF NOT EXISTS idx_webbrowselog_logtime_siteid ON WebBrowseLogModels(LogTime, SiteId);
        "#,
    },
    Migration {
        version: 6,
        name: "fix_iconfile_paths",
        sql: r#"
            UPDATE WebSiteModels SET IconFile = SUBSTR(IconFile, INSTR(IconFile, 'WebFavicons\')) WHERE IconFile LIKE '%\WebFavicons\%';
            UPDATE WebUrlModels SET IconFile = SUBSTR(IconFile, INSTR(IconFile, 'WebFavicons\')) WHERE IconFile LIKE '%\WebFavicons\%';
            UPDATE WebSiteModels SET IconFile = 'WebFavicons\' || IconFile WHERE IconFile IS NOT NULL AND IconFile != '' AND IconFile NOT LIKE 'WebFavicons%';
            UPDATE WebUrlModels SET IconFile = 'WebFavicons\' || IconFile WHERE IconFile IS NOT NULL AND IconFile != '' AND IconFile NOT LIKE 'WebFavicons%';
            UPDATE AppModels SET IconFile = SUBSTR(IconFile, INSTR(IconFile, 'AppIcons\')) WHERE IconFile LIKE '%\AppIcons\%';
        "#,
    },
    Migration {
        version: 7,
        name: "fix_dailylog_date_residual",
        sql: r#"
            UPDATE DailyLogModels SET Date = substr(Date, 1, 10) WHERE length(Date) > 10;
        "#,
    },
    Migration {
        version: 8,
        name: "convert_hourslog_to_utc",
        sql: r#"
            UPDATE HoursLogModels SET DataTime = datetime(DataTime, '-8 hours');
        "#,
    },
    Migration {
        version: 9,
        name: "convert_webbrowse_to_utc",
        sql: r#"
            UPDATE WebBrowseLogModels SET LogTime = datetime(LogTime, '-8 hours');
        "#,
    },
    Migration {
        version: 10,
        name: "fix_websitemodels_categoryid",
        sql: r#"
            UPDATE WebSiteModels SET CategoryID = COALESCE((SELECT ID FROM WebSiteCategoryModels WHERE IsSystem = 1 LIMIT 1), 0) WHERE CategoryID = 0;
        "#,
    }
];

pub async fn init_db(db_path: &str) -> anyhow::Result<SqlitePool> {
    let path = Path::new(db_path);
    if let Some(parent) = path.parent() {
        tokio::fs::create_dir_all(parent).await?;
    }

    // 先用单连接跑迁移，避免一直占着文件句柄
    let temp_options = SqliteConnectOptions::from_str(&format!("sqlite:{}", db_path))?
        .create_if_missing(true);
    let temp_pool = SqlitePoolOptions::new()
        .max_connections(1)
        .connect_with(temp_options)
        .await?;

    migrate(&temp_pool, db_path).await?;
    temp_pool.close().await;

    // 建立业务连接池，并启动WAL模式
    let options = SqliteConnectOptions::from_str(&format!("sqlite:{}", db_path))?
        .journal_mode(SqliteJournalMode::Wal);
    let pool = SqlitePoolOptions::new()
        .max_connections(10)
        .connect_with(options)
        .await?;

    Ok(pool)
}

async fn migrate(pool: &SqlitePool, db_path: &str) -> anyhow::Result<()> {
    // 检测是否为新库（没有任何表）
    let table_count: i64 = sqlx::query_scalar(
        "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table'"
    )
    .fetch_one(pool)
    .await?;

    let is_fresh = table_count == 0;

    // 基线建表
    sqlx::query(BASELINE_SQL).execute(pool).await?;

    sqlx::query(
        r#"
        CREATE TABLE IF NOT EXISTS _migrations (
            version INTEGER PRIMARY KEY,
            name TEXT NOT NULL,
            applied_at TEXT NOT NULL DEFAULT (datetime('now', 'localtime'))
        )
        "#,
    )
    .execute(pool)
    .await?;

    // 新库：插入初始化数据，并直接标记所有迁移已执行
    if is_fresh {
        sqlx::query(
            r#"
            INSERT OR IGNORE INTO CategoryModels (Name, IconFile, Color, IsSystem) VALUES ('未分类', 'avares://Taix/Resources/Icons/tai.ico', '#808080', 1);
            INSERT OR IGNORE INTO WebSiteCategoryModels (Name, IconFile, Color, IsSystem) VALUES ('未分类', 'avares://Taix/Resources/Icons/tai.ico', '#808080', 1);
            "#,
        )
        .execute(pool)
        .await?;

        for m in MIGRATIONS {
            sqlx::query("INSERT INTO _migrations (version, name) VALUES (?, ?)")
                .bind(m.version)
                .bind(m.name)
                .execute(pool)
                .await?;
        }
        info!("新数据库初始化完成，已标记 {} 个迁移为已执行", MIGRATIONS.len());
        return Ok(());
    }

    let applied_versions: Vec<i64> =
        sqlx::query_scalar("SELECT version FROM _migrations ORDER BY version")
            .fetch_all(pool)
            .await?;

    // 算出还没执行的迁移
    let pending: Vec<&Migration> = MIGRATIONS
        .iter()
        .filter(|m| !applied_versions.contains(&m.version))
        .collect();

    if pending.is_empty() {
        return Ok(());
    }

    info!(
        "发现 {} 个待执行数据库迁移，准备备份数据文件...",
        pending.len()
    );
    for m in &pending {
        info!("  [待执行] v{} - {}", m.version, m.name);
    }

    pool.close().await;

    // 只在数据库从未执行过迁移时备份一次
    if applied_versions.is_empty() {
        let timestamp = chrono::Local::now().format("%Y%m%d_%H%M%S");
        let backup_path = format!("{}.backup.{}", db_path, timestamp);
        tokio::fs::copy(db_path, &backup_path).await?;
        info!("数据库已备份至: {}", backup_path);
    } else {
        info!("数据库已有迁移记录，跳过备份");
    }

    // 重新连上数据库执行迁移
    let migrate_pool = SqlitePoolOptions::new()
        .max_connections(1)
        .connect_with(
            SqliteConnectOptions::from_str(&format!("sqlite:{}", db_path))?
                .create_if_missing(false),
        )
        .await?;

    for migration in pending {
        info!("执行迁移 v{}: {}", migration.version, migration.name);

        let mut tx = migrate_pool.begin().await?;

        for stmt in migration.sql.split(';').map(str::trim).filter(|s| !s.is_empty()) {
            if let Err(e) = sqlx::query(stmt).execute(&mut *tx).await {
                let err_msg = e.to_string().to_lowercase();
                let is_index_stmt = stmt.to_lowercase().contains("create index");

                if err_msg.contains("duplicate column") || err_msg.contains("already exists") {
                    warn!(
                        "迁移 v{} 语句被跳过（已存在）: {}",
                        migration.version,
                        stmt.split_whitespace().take(6).collect::<Vec<_>>().join(" ")
                    );
                    continue;
                }

                // 索引创建失败降级为警告，不阻塞迁移
                if is_index_stmt {
                    warn!(
                        "迁移 v{} 索引创建失败（不影响业务运行）: {} | 错误: {}",
                        migration.version,
                        stmt.split_whitespace().take(6).collect::<Vec<_>>().join(" "),
                        e
                    );
                    continue;
                }

                return Err(anyhow::anyhow!(
                    "迁移 v{} [{}] 执行失败: {}\nSQL: {}",
                    migration.version,
                    migration.name,
                    e,
                    stmt
                ));
            }
        }

        // 写入迁移记录
        sqlx::query("INSERT INTO _migrations (version, name) VALUES (?, ?)")
            .bind(migration.version)
            .bind(migration.name)
            .execute(&mut *tx)
            .await?;

        tx.commit().await?;
        info!("迁移 v{} 执行成功", migration.version);
    }

    migrate_pool.close().await;
    Ok(())
}
