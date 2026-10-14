use serde_json::{json, Value};
use sqlx::SqlitePool;
use std::sync::atomic::Ordering;

use crate::plugins::PluginState;

const TZ: chrono_tz::Tz = chrono_tz::Asia::Shanghai;

pub(crate) fn today_bounds() -> (String, String) {
    let now_sh = chrono::Utc::now().with_timezone(&TZ);
    let today = now_sh.format("%Y-%m-%d").to_string();
    let start_utc = TZ
        .with_ymd_and_hms(
            now_sh.year(),
            now_sh.month(),
            now_sh.day(),
            0, 0, 0,
        )
        .single()
        .map(|t| t.with_timezone(&chrono::Utc).to_rfc3339_opts(chrono::SecondsFormat::Secs, true))
        .unwrap_or_default();
    (today, start_utc)
}

use chrono::{Datelike, TimeZone};

pub async fn today_seconds(pool: &SqlitePool, process: &str, today_start_utc: &str) -> i64 {
    sqlx::query_scalar::<_, i64>(
        "SELECT COALESCE(SUM(Duration), 0) FROM AppDurationRequests \
         WHERE ProcessName = ?1 AND StartDateTime >= ?2",
    )
    .bind(process)
    .bind(today_start_utc)
    .fetch_one(pool)
    .await
    .unwrap_or(0)
}

pub async fn snapshot(state: &PluginState, pool: &SqlitePool) -> Value {
    let sleep = state.sleep_flag.load(Ordering::Relaxed);
    let continuous_since = state.continuous_since.load(Ordering::Relaxed);
    let app = state.current_app.read().await.clone();

    let app = match app {
        Some(mut app) => {
            if let Some(process) = app["process"].as_str().map(str::to_string) {
                let (today, today_start) = today_bounds();
                let secs = today_seconds(pool, &process, &today_start).await;
                app["today"] = json!(today);
                app["todaySeconds"] = json!(secs);
            }
            Some(app)
        }
        None => None,
    };

    // 段起点 RFC3339 UTC；null = 睡眠中
    let continuous_since = if continuous_since > 0 {
        json!(chrono::DateTime::from_timestamp(continuous_since, 0)
            .map(|d| d.to_rfc3339_opts(chrono::SecondsFormat::Secs, true)))
    } else {
        Value::Null
    };

    json!({ "app": app, "sleep": sleep, "continuousSince": continuous_since })
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
    *state.current_app.write().await = Some(app);
    let snap = snapshot(state, pool).await;
    let _ = state.tx.send(envelope(&snap).to_string());
}

pub async fn set_sleep(state: &PluginState, pool: &SqlitePool, sleeping: bool) {
    state.sleep_flag.store(sleeping, Ordering::Relaxed);
    // 唤醒即开新段
    let now = std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0);
    state
        .continuous_since
        .store(if sleeping { 0 } else { now }, Ordering::Relaxed);
    let snap = snapshot(state, pool).await;
    let _ = state.tx.send(envelope(&snap).to_string());
}

/// 未结算的进行中会话只有这里能给出
pub async fn ongoing_session(state: &PluginState) -> Option<(String, i64)> {
    if state.sleep_flag.load(Ordering::Relaxed) {
        return None;
    }
    let app = state.current_app.read().await.clone()?;
    let process = app["process"].as_str()?.to_string();
    let since = chrono::DateTime::parse_from_rfc3339(app["since"].as_str()?)
        .ok()?
        .timestamp();
    Some((process, since))
}
