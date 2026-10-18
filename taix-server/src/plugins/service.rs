use serde_json::{json, Value};
use sqlx::SqlitePool;
use std::sync::atomic::Ordering;

use crate::constants;
use crate::error::AppError;

use super::registry::{self, PluginConfig, PluginRegistry};
use super::{PluginCommand, PluginState, PluginStatus};

fn now_rfc3339() -> String {
    chrono::Utc::now().to_rfc3339_opts(chrono::SecondsFormat::Secs, true)
}

/// 入队 + 唤醒投递泵；投递靠各订阅者自己的游标读同一份有界日志
pub fn push_command(
    state: &PluginState,
    id: &str,
    cmd: String,
    data: Option<Value>,
) -> PluginCommand {
    let seq = state.seq.fetch_add(1, Ordering::Relaxed);
    let command = PluginCommand { seq, cmd, data };

    {
        let mut queues = state.queues.lock().unwrap();
        let queue = queues.entry(id.to_string()).or_default();
        while queue.log.len() >= constants::PLUGIN_COMMAND_QUEUE_MAX {
            if let Some(dropped) = queue.log.pop_front() {
                tracing::warn!(
                    target: "taix_server::plugins",
                    "command log for '{}' is full ({}), dropping oldest seq={}",
                    id,
                    constants::PLUGIN_COMMAND_QUEUE_MAX,
                    dropped.seq
                );
            }
        }
        queue.log.push_back(command.clone());
    }

    state.notify_commands(id);
    command
}

/// poll 降级通道的投递；自带游标，与 SSE 泵互不影响
pub fn take_commands(state: &PluginState, id: &str) -> Vec<PluginCommand> {
    state.take_poll_commands(id)
}

pub async fn get_config(state: &PluginState, id: &str) -> PluginConfig {
    let registry = state.registry.read().await;
    registry
        .configs
        .get(id)
        .cloned()
        .unwrap_or_default()
}

pub async fn is_enabled(state: &PluginState, id: &str) -> bool {
    let registry = state.registry.read().await;
    registry.enabled.get(id).copied().unwrap_or(false)
}

/// 先落盘成功才替换内存；成功后 version+1 并广播
pub async fn save_config(
    state: &PluginState,
    id: &str,
    values: Value,
) -> Result<u32, AppError> {
    if !values.is_object() {
        return Err(AppError::Business("config values 必须是 JSON 对象".to_string()));
    }

    let _guard = state.registry_write.lock().await;
    let mut registry = state.registry.write().await;

    let mut snapshot: PluginRegistry = registry.clone();
    let config = snapshot.configs.entry(id.to_string()).or_default();
    config.values = values;
    config.version += 1;
    let version = config.version;

    registry::persist(&state.data_dir, &snapshot).await?;
    *registry = snapshot;
    drop(registry);
    drop(_guard);

    broadcast(state, json!({"type": "config", "id": id, "version": version}));
    Ok(version)
}

pub async fn set_enabled(state: &PluginState, id: &str, enabled: bool) -> Result<(), AppError> {
    let _guard = state.registry_write.lock().await;
    let mut registry = state.registry.write().await;

    let mut snapshot = registry.clone();
    snapshot.enabled.insert(id.to_string(), enabled);

    registry::persist(&state.data_dir, &snapshot).await?;
    *registry = snapshot;
    drop(registry);
    drop(_guard);

    if !enabled {
        state.states.write().await.remove(id);
        // 禁用后旧指令不能留到重新启用时再执行
        state.queues.lock().unwrap().remove(id);
        state.notify.lock().unwrap().remove(id);
    }

    broadcast(
        state,
        json!({
            "type": "plugin-state",
            "id": id,
            "state": if enabled { "enabled" } else { "disabled" },
            "data": { "enabled": enabled },
            "ts": now_rfc3339(),
        }),
    );
    Ok(())
}

pub async fn set_plugin_state(
    state: &PluginState,
    id: &str,
    plugin_state: String,
    data: Option<Value>,
) {
    let ts = now_rfc3339();
    let status = PluginStatus {
        state: plugin_state.clone(),
        data: data.clone(),
        ts: ts.clone(),
    };
    state.states.write().await.insert(id.to_string(), status);

    broadcast(
        state,
        json!({
            "type": "plugin-state",
            "id": id,
            "state": plugin_state,
            "data": data,
            "ts": ts,
        }),
    );
}

/// SDK 每 30s 重推状态做存活心跳；超过这个时长没有更新说明进程已死，按「未上报」返回
const STATUS_STALE_AFTER_SECS: i64 = 90;

pub async fn get_status(state: &PluginState, id: &str) -> Option<PluginStatus> {
    if !is_enabled(state, id).await {
        return None;
    }
    let status = state.states.read().await.get(id).cloned()?;
    match chrono::DateTime::parse_from_rfc3339(&status.ts) {
        Ok(ts) if chrono::Utc::now().signed_duration_since(ts).num_seconds() > STATUS_STALE_AFTER_SECS => {
            tracing::debug!(
                target: "taix_server::plugins",
                "status for '{}' is stale ({}), reporting as none",
                id,
                status.ts
            );
            None
        }
        _ => Some(status),
    }
}

#[derive(Debug, serde::Serialize)]
#[serde(rename_all = "camelCase")]
pub struct PluginRecordRow {
    pub key: String,
    pub value: Value,
    pub updated_at: String,
}

pub async fn upsert_record(
    pool: &SqlitePool,
    plugin_id: &str,
    key: &str,
    value: &Value,
) -> Result<(), AppError> {
    let key = key.trim();
    let value_json = serde_json::to_string(value)
        .map_err(|e| AppError::Internal(format!("序列化 record 失败: {}", e)))?;

    sqlx::query(
        r#"
        INSERT INTO PluginRecords (PluginID, RecordKey, Value, UpdatedAt)
        VALUES (?1, ?2, ?3, ?4)
        ON CONFLICT(PluginID, RecordKey)
        DO UPDATE SET Value = excluded.Value, UpdatedAt = excluded.UpdatedAt
        "#,
    )
    .bind(plugin_id)
    .bind(key)
    .bind(value_json)
    .bind(now_rfc3339())
    .execute(pool)
    .await?;

    // 写后即裁：上限必须随时成立，不能等每天的维护任务
    if let Ok(trimmed) = trim_records_for(pool, plugin_id).await {
        if trimmed > 0 {
            tracing::debug!(
                target: "taix_server::plugins",
                "trimmed {} record(s) beyond the cap for '{}'",
                trimmed,
                plugin_id
            );
        }
    }

    Ok(())
}

pub async fn get_records(
    pool: &SqlitePool,
    plugin_id: &str,
    key: Option<&str>,
) -> Result<Vec<PluginRecordRow>, AppError> {
    fn parse_row(key: String, value: String, updated_at: String) -> PluginRecordRow {
        let value = serde_json::from_str(&value).unwrap_or(Value::Null);
        PluginRecordRow {
            key,
            value,
            updated_at,
        }
    }

    // 写入端会 trim，查询同样要 trim，否则带空白的 key 存进去后按原样查不回来
    let key = key.map(|k| k.trim().to_string());

    let rows = match key.as_deref() {
        Some(key) => {
            sqlx::query_as::<_, (String, String, String)>(
                "SELECT RecordKey, Value, UpdatedAt FROM PluginRecords WHERE PluginID = ?1 AND RecordKey = ?2",
            )
            .bind(plugin_id)
            .bind(key)
            .fetch_optional(pool)
            .await?
            .into_iter()
            .collect::<Vec<_>>()
        }
        None => {
            sqlx::query_as::<_, (String, String, String)>(
                "SELECT RecordKey, Value, UpdatedAt FROM PluginRecords WHERE PluginID = ?1 ORDER BY RecordKey",
            )
            .bind(plugin_id)
            .fetch_all(pool)
            .await?
        }
    };

    Ok(rows
        .into_iter()
        .map(|(k, v, u)| parse_row(k, v, u))
        .collect())
}

/// 每插件只保留最新 N 个 key，超出删最旧。
/// 刻意不按时间清：插件持久状态不能随统计保留期消失。
/// 同一秒内时间戳并列时用 rowid 兜底，保证淘汰顺序确定
pub async fn trim_records_for(pool: &SqlitePool, plugin_id: &str) -> Result<u64, sqlx::Error> {
    let result = sqlx::query(
        r#"
        DELETE FROM PluginRecords
        WHERE PluginID = ?1 AND rowid NOT IN (
            SELECT rowid FROM PluginRecords
            WHERE PluginID = ?1
            ORDER BY UpdatedAt DESC, rowid DESC
            LIMIT ?2
        )
        "#,
    )
    .bind(plugin_id)
    .bind(constants::PLUGIN_RECORDS_MAX_PER_PLUGIN)
    .execute(pool)
    .await?;
    Ok(result.rows_affected())
}

pub async fn trim_records(pool: &SqlitePool) -> Result<u64, sqlx::Error> {
    let owners: Vec<(String,)> = sqlx::query_as("SELECT DISTINCT PluginID FROM PluginRecords")
        .fetch_all(pool)
        .await?;

    let mut deleted = 0u64;
    for (plugin_id,) in owners {
        deleted += trim_records_for(pool, &plugin_id).await?;
    }

    Ok(deleted)
}

fn broadcast(state: &PluginState, event: Value) {
    // 无订阅者时 send 失败是常态；指令已入队，重连后重放
    let _ = state.tx.send(event.to_string());
}
