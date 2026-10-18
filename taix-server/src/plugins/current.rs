use serde_json::{json, Value};
use sqlx::SqlitePool;
use std::path::Path;
use std::sync::atomic::Ordering;
use std::sync::Arc;

use crate::plugins::PluginState;
use crate::utils::host_timezone;

/// monitor 5s 一拍；静默超过这个时长视为空闲/睡眠（看门狗与读路径共用）
pub const HEARTBEAT_STALE_SECS: i64 = 90;

/// 连续段落盘文件，放在 data_dir 下
pub const CONTINUOUS_STATE_FILE: &str = "continuous.json";

/// 落盘刷新间隔；值不变就不写，空闲时零写入
const PERSIST_INTERVAL_SECS: i64 = 60;

/// 时钟回拨 / 超前的容忍量
const CLOCK_SKEW_SECS: i64 = 60;

fn now_epoch() -> i64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0)
}

#[derive(serde::Serialize, serde::Deserialize)]
struct ContinuousState {
    since: i64,
    #[serde(rename = "lastSeen")]
    last_seen: i64,
}

pub fn load_continuous(path: &Path) -> Option<(i64, i64)> {
    let text = std::fs::read_to_string(path).ok()?;
    let st: ContinuousState = serde_json::from_str(&text).ok()?;
    let now = now_epoch();
    if st.since < 0
        || st.last_seen <= 0
        || st.last_seen > now + CLOCK_SKEW_SECS
        || st.since > st.last_seen + 2
    {
        return None;
    }
    Some((st.since, st.last_seen))
}

/// 先写临时文件再 rename，崩溃时不会留下半截 JSON
fn save_continuous(path: &Path, since: i64, last_seen: i64) -> std::io::Result<()> {
    let body = serde_json::to_vec(&ContinuousState { since, last_seen })
        .map_err(std::io::Error::other)?;
    let tmp = path.with_extension("json.tmp");
    std::fs::write(&tmp, body)?;
    std::fs::rename(&tmp, path)
}

pub fn persist(state: &PluginState) {
    let since = state.continuous_since.load(Ordering::Relaxed);
    let last_seen = state.last_current_at.load(Ordering::Relaxed);
    let mut saved = state
        .continuous_saved
        .lock()
        .unwrap_or_else(|e| e.into_inner());
    if *saved == Some((since, last_seen)) {
        return;
    }
    match save_continuous(&state.continuous_path, since, last_seen) {
        Ok(()) => *saved = Some((since, last_seen)),
        Err(e) => tracing::warn!(
            target: "taix_server::plugins",
            "continuous state write failed: {}",
            e
        ),
    }
}

/// last = 0（还没收到过心跳，如 server 刚启动）不算过期
fn heartbeat_stale(last: i64, now: i64, stale_secs: i64) -> bool {
    last > 0 && now - last > stale_secs
}

/// 空闲/睡眠：显式睡眠标记，或心跳静默超过阈值（事件丢失 / monitor 崩溃的兜底）
fn is_idle(state: &PluginState, now: i64) -> bool {
    state.sleep_flag.load(Ordering::Relaxed)
        || heartbeat_stale(
            state.last_current_at.load(Ordering::Relaxed),
            now,
            state.current_stale_secs,
        )
}

/// 宿主机时区的日期标签（YYYY-MM-DD）
fn today_date() -> String {
    chrono::Utc::now()
        .with_timezone(&host_timezone())
        .format("%Y-%m-%d")
        .to_string()
}

/// 宿主机时区今天 0 点的 epoch 秒（DST 空洞日取当天第一个存在的本地时刻）
fn today_start_epoch() -> i64 {
    let tz = host_timezone();
    let today = chrono::Utc::now().with_timezone(&tz).date_naive();
    crate::utils::tz_date_to_utc_range(today, &tz).0.timestamp()
}

/// 该进程今日的**已结算**时长；跨 0 点的会话只计与今天的重叠部分
pub async fn today_seconds(pool: &SqlitePool, process: &str, day_start_epoch: i64) -> i64 {
    sqlx::query_scalar::<_, i64>(
        "SELECT COALESCE(SUM(MAX(0, (CAST(strftime('%s', StartDateTime) AS INTEGER) + Duration) \
         - MAX(CAST(strftime('%s', StartDateTime) AS INTEGER), ?2))), 0) \
         FROM AppDurationRequests \
         WHERE ProcessName = ?1 AND CAST(strftime('%s', StartDateTime) AS INTEGER) + Duration > ?2",
    )
    .bind(process)
    .bind(day_start_epoch)
    .fetch_one(pool)
    .await
    .unwrap_or(0)
}

pub async fn snapshot(state: &PluginState, pool: &SqlitePool) -> Value {
    let idle = is_idle(state, now_epoch());
    let continuous_since = state.continuous_since.load(Ordering::Relaxed);
    let app = state.current_app.read().await.clone();

    let app = match app {
        Some(mut app) => {
            if let Some(process) = app["process"].as_str().map(str::to_string) {
                let secs = today_seconds(pool, &process, today_start_epoch()).await;
                app["today"] = json!(today_date());
                app["todaySeconds"] = json!(secs);
            }
            Some(app)
        }
        None => None,
    };

    // 段起点 RFC3339 UTC；null = 空闲/睡眠中（连续使用类插件据此冻结）
    let continuous_since = if !idle && continuous_since > 0 {
        json!(chrono::DateTime::from_timestamp(continuous_since, 0)
            .map(|d| d.to_rfc3339_opts(chrono::SecondsFormat::Secs, true)))
    } else {
        Value::Null
    };

    json!({ "app": app, "sleep": idle, "continuousSince": continuous_since })
}

fn envelope(snap: &Value) -> Value {
    json!({
        "type": "current",
        "app": snap["app"].clone(),
        "sleep": snap["sleep"].clone(),
        "continuousSince": snap["continuousSince"].clone(),
    })
}

pub async fn update_and_broadcast(state: &PluginState, pool: &SqlitePool, app: Value) {
    let now = now_epoch();
    // 静默期后的首拍 = 用户回来了：退出空闲并重开连续段；
    // 常规节拍（5s 一拍）与应用切换都不动连续段
    let restarted = is_idle(state, now) || state.continuous_since.load(Ordering::Relaxed) == 0;
    if restarted {
        state.sleep_flag.store(false, Ordering::Relaxed);
        state.continuous_since.store(now, Ordering::Relaxed);
    }
    state.last_current_at.store(now, Ordering::Relaxed);
    if restarted {
        persist(state);
    }
    *state.current_app.write().await = Some(app);
    let snap = snapshot(state, pool).await;
    let _ = state.tx.send(envelope(&snap).to_string());
}

pub async fn set_sleep(state: &PluginState, pool: &SqlitePool, sleeping: bool) {
    state.sleep_flag.store(sleeping, Ordering::Relaxed);
    // 唤醒即开新段
    let now = now_epoch();
    state
        .continuous_since
        .store(if sleeping { 0 } else { now }, Ordering::Relaxed);
    if !sleeping {
        // 显式唤醒也算一次「平台活着」的信号，避免读路径立刻又按静默判空闲
        state.last_current_at.store(now, Ordering::Relaxed);
        // 睡前快照已经结算，清掉，等下一拍 current 再填
        *state.current_app.write().await = None;
    }
    persist(state);
    let snap = snapshot(state, pool).await;
    let _ = state.tx.send(envelope(&snap).to_string());
}

/// monitor 明确表示「当前没有可统计的前台应用」时清理快照，
/// 但仍算平台活着（刷新心跳时间，避免看门狗把连续段冻结）
pub async fn clear_current(state: &PluginState, pool: &SqlitePool) {
    state.last_current_at.store(now_epoch(), Ordering::Relaxed);
    state.sleep_flag.store(false, Ordering::Relaxed);
    *state.current_app.write().await = None;
    let snap = snapshot(state, pool).await;
    let _ = state.tx.send(envelope(&snap).to_string());
}

/// 未结算的进行中会话只有这里能给出；空闲/睡眠期间没有进行中会话
/// current 快照可能还停在刚结算的会话上，按 sessionId 精确剔除
pub async fn ongoing_session(state: &PluginState, pool: &SqlitePool) -> Option<(String, i64)> {
    if is_idle(state, now_epoch()) {
        return None;
    }
    let app = state.current_app.read().await.clone()?;
    let process = app["process"].as_str()?.to_string();
    let since = chrono::DateTime::parse_from_rfc3339(app["since"].as_str()?)
        .ok()?
        .timestamp();
    let session_id = app["sessionId"].as_str()?;

    let mut settled = sqlx::query_scalar::<_, i64>(
        "SELECT 1 FROM AppDurationRequests WHERE SessionId = ?1 LIMIT 1",
    )
    .bind(session_id)
    .fetch_optional(pool)
    .await
    .map(|row| row.is_some())
    .unwrap_or(false);

    // 兼容旧版 monitor：兜底 key 是 process@since，不含时长。
    // 新会话 id 不含 @，不会走到这里
    if !settled && session_id.contains('@') {
        let since_key = chrono::DateTime::from_timestamp(since, 0)
            .map(|d| d.to_rfc3339_opts(chrono::SecondsFormat::Secs, true));
        if let Some(since_key) = since_key {
            settled = sqlx::query_scalar::<_, i64>(
                "SELECT 1 FROM AppDurationRequests \
                 WHERE ProcessName = ?1 AND StartDateTime = ?2 LIMIT 1",
            )
            .bind(&process)
            .bind(since_key)
            .fetch_optional(pool)
            .await
            .map(|row| row.is_some())
            .unwrap_or(false);
        }
    }

    if settled {
        return None;
    }
    Some((process, since))
}

/// 心跳看门狗：monitor 静默（崩溃 / 事件丢失 / 平台没上报空闲）超过阈值时，
/// 按「空闲」广播一次 current 信封，让插件冻结；之后首个心跳自动重开连续段
pub async fn run_idle_watchdog(state: Arc<PluginState>, pool: SqlitePool) {
    let mut tick = tokio::time::interval(std::time::Duration::from_secs(2));
    tick.set_missed_tick_behavior(tokio::time::MissedTickBehavior::Skip);
    let mut last_persist = now_epoch();
    loop {
        tick.tick().await;
        let now = now_epoch();
        if now - last_persist >= PERSIST_INTERVAL_SECS {
            persist(&state);
            last_persist = now;
        }
        let last = state.last_current_at.load(Ordering::Relaxed);
        if state.sleep_flag.load(Ordering::Relaxed)
            || !heartbeat_stale(last, now, state.current_stale_secs)
        {
            continue;
        }
        tracing::info!(
            target: "taix_server::plugins",
            "current 心跳静默 {}s，按空闲处理",
            now - last
        );
        set_sleep(&state, &pool, true).await;
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn heartbeat_staleness_boundaries() {
        assert!(!heartbeat_stale(0, 10_000, 90), "从未收到心跳不算过期");
        assert!(!heartbeat_stale(1_000, 1_090, 90), "恰好等于阈值不算过期");
        assert!(heartbeat_stale(1_000, 1_091, 90));
    }
}
