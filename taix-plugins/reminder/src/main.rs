mod engine;

use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use serde::Deserialize;
use serde_json::{json, Value};
use taix_plugin_sdk::{PluginApp, PluginCtx, Reply, Request};

use engine::{run_script, RemindCandidate, Snapshot, DEFAULT_SCRIPT};

const PLUGIN_ID: &str = "reminder";
const TICK: Duration = Duration::from_secs(5);
const HEARTBEAT_TICKS: u64 = 6;
/// 全局口径：Asia/Shanghai
const TZ: &str = "Asia/Shanghai";
/// 其余命中让位，防连弹
const ROUND_DEFER_SECS: i64 = 300;

#[derive(Debug, Clone, Default, Deserialize)]
struct ReminderConfig {
    #[serde(default)]
    sound: bool,
    #[serde(default)]
    script: Option<String>,
}

struct Runtime {
    /// 连续使用段起点；None = 还没收到过 current
    continuous_since: Mutex<Option<i64>>,
    /// 规则 id → 冷却截止（epoch 秒）
    cooldowns: Mutex<HashMap<String, i64>>,
    last_notify: Mutex<Option<Value>>,
    current_process: Mutex<String>,
    sleeping: AtomicBool,
    sound: AtomicBool,
    script: Mutex<String>,
    /// 新存进来、还没试跑过的脚本
    candidate: Mutex<Option<String>>,
    /// 新脚本不可用的原因；提醒沿用旧脚本
    script_error: Mutex<Option<String>>,
    reported_error: Mutex<Option<String>>,
    ticks: AtomicU64,
}

impl Runtime {
    fn new() -> Self {
        Self {
            continuous_since: Mutex::new(None),
            cooldowns: Mutex::new(HashMap::new()),
            last_notify: Mutex::new(None),
            current_process: Mutex::new(String::new()),
            sleeping: AtomicBool::new(false),
            sound: AtomicBool::new(false),
            script: Mutex::new(DEFAULT_SCRIPT.to_string()),
            candidate: Mutex::new(None),
            script_error: Mutex::new(None),
            reported_error: Mutex::new(None),
            ticks: AtomicU64::new(0),
        }
    }
}

fn now_epoch() -> i64 {
    SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0)
}

fn rfc3339(epoch: i64) -> String {
    chrono::DateTime::from_timestamp(epoch, 0)
        .unwrap_or_else(chrono::Utc::now)
        .to_rfc3339_opts(chrono::SecondsFormat::Secs, true)
}

/// Asia/Shanghai 恒为 UTC+8
fn local_hour_weekday() -> (i64, i64) {
    use chrono::{Datelike, TimeZone, Timelike};
    let tz = chrono::FixedOffset::east_opt(8 * 3600).expect("+8 时区合法");
    let now = tz.from_utc_datetime(&chrono::Utc::now().naive_utc());
    (now.hour() as i64, now.weekday().number_from_monday() as i64)
}

fn pairs(value: &Value) -> Vec<(String, i64)> {
    value
        .as_array()
        .map(|rows| {
            rows.iter()
                .filter_map(|row| {
                    Some((
                        row["name"].as_str()?.to_string(),
                        row["seconds"].as_i64()?,
                    ))
                })
                .collect()
        })
        .unwrap_or_default()
}

fn parse_rfc3339(text: &str) -> Option<i64> {
    chrono::DateTime::parse_from_rfc3339(text)
        .ok()
        .map(|t| t.timestamp())
}

fn continuous_seconds(ctx: &PluginCtx, rt: &Runtime) -> i64 {
    if rt.sleeping.load(Ordering::Relaxed) {
        return 0;
    }
    let mut since = *rt.continuous_since.lock().unwrap();
    if since.is_none() {
        if let Ok(current) = ctx.get_json("/api/apptimer/current") {
            since = current["continuousSince"].as_str().and_then(parse_rfc3339);
            *rt.continuous_since.lock().unwrap() = since;
        }
    }
    since.map(|start| (now_epoch() - start).max(0)).unwrap_or(0)
}

fn build_snapshot(ctx: &PluginCtx, rt: &Runtime) -> anyhow::Result<Snapshot> {
    let summary = ctx.get_json(&format!("/api/data/today-summary?tz={TZ}&includeOngoing=1"))?;
    let sleeping = rt.sleeping.load(Ordering::Relaxed);
    let (hour, weekday) = local_hour_weekday();

    Ok(Snapshot {
        continuous_seconds: continuous_seconds(ctx, rt),
        sleeping,
        today_total_seconds: summary["totalSeconds"].as_i64().unwrap_or(0),
        apps: pairs(&summary["apps"]),
        categories: pairs(&summary["categories"]),
        current_process: rt.current_process.lock().unwrap().clone(),
        hour,
        weekday,
    })
}

fn snapshot_view(snap: &Snapshot) -> Value {
    let top = |rows: &Vec<(String, i64)>| {
        rows.iter()
            .take(10)
            .map(|(name, secs)| json!({ "name": name, "seconds": secs }))
            .collect::<Vec<_>>()
    };
    json!({
        "continuousSeconds": snap.continuous_seconds,
        "todayTotalSeconds": snap.today_total_seconds,
        "currentProcess": snap.current_process,
        "hour": snap.hour,
        "weekday": snap.weekday,
        "apps": top(&snap.apps),
        "categories": top(&snap.categories),
    })
}

fn notify(title: &str, body: &str, sound: bool) {
    // 通知是阻塞 IPC，放专用线程
    let (t, b) = (title.to_string(), body.to_string());
    std::thread::spawn(move || {
        let opts = taix_notify::NotifyOptions {
            sound: sound.then_some("default"),
            ..Default::default()
        };
        if let Err(e) = taix_notify::notify(&t, &b, &opts) {
            tracing::warn!(target: "taix_plugin_reminder", "通知发送失败: {e:#}");
        }
    });
}

/// 错误变化立即报，平时 30s 心跳
fn report_state(ctx: &PluginCtx, rt: &Runtime, snap: &Snapshot, force: bool) {
    let ticks = rt.ticks.fetch_add(1, Ordering::Relaxed) + 1;
    if !force && ticks % HEARTBEAT_TICKS != 0 {
        return;
    }

    let script_error = rt.script_error.lock().unwrap().clone();
    let last_notify = rt.last_notify.lock().unwrap().clone();
    let cooldown_until = {
        let now = now_epoch();
        rt.cooldowns
            .lock()
            .unwrap()
            .values()
            .copied()
            .filter(|until| *until > now)
            .min()
    };

    let mut data = json!({
        "continuousSeconds": snap.continuous_seconds,
        "todayTotalSeconds": snap.today_total_seconds,
        "currentProcess": snap.current_process,
        "hour": snap.hour,
        "weekday": snap.weekday,
    });

    match script_error {
        Some(err) => {
            let mut reported = rt.reported_error.lock().unwrap();
            if !force && reported.as_deref() == Some(err.as_str()) {
                return;
            }
            *reported = Some(err.clone());
            data["error"] = json!(err);
            if let Some(until) = cooldown_until {
                data["cooldownUntil"] = json!(rfc3339(until));
            }
            if let Some(last) = last_notify {
                data["lastNotify"] = last;
            }
            ctx.push_state_now("error", Some(data));
        }
        None => {
            *rt.reported_error.lock().unwrap() = None;
            data["info"] = json!("监控中");
            if let Some(until) = cooldown_until {
                data["cooldownUntil"] = json!(rfc3339(until));
            }
            if let Some(last) = last_notify {
                data["lastNotify"] = last;
            }
            if force {
                ctx.push_state_now("running", Some(data));
            } else {
                ctx.push_state("running", Some(data));
            }
        }
    }
}

fn tick(ctx: &PluginCtx, rt: &Arc<Runtime>) {
    if rt.sleeping.load(Ordering::Relaxed) {
        return;
    }

    let snap = match build_snapshot(ctx, rt) {
        Ok(snap) => snap,
        Err(e) => {
            tracing::warn!(target: "taix_plugin_reminder", "取今日数据失败: {e:#}");
            return;
        }
    };

    // 新脚本先试跑，失败沿用上一份
    if let Some(candidate) = rt.candidate.lock().unwrap().take() {
        match run_script(&candidate, &snap) {
            Ok(_) => {
                *rt.script.lock().unwrap() = candidate;
                *rt.script_error.lock().unwrap() = None;
                tracing::info!(target: "taix_plugin_reminder", "脚本已生效");
            }
            Err(e) => {
                tracing::warn!(target: "taix_plugin_reminder", "新脚本不可用，沿用上一份: {e}");
                *rt.script_error.lock().unwrap() = Some(e);
            }
        }
        report_state(ctx, rt, &snap, true);
    }

    let script = rt.script.lock().unwrap().clone();
    let candidates = match run_script(&script, &snap) {
        // 脚本报错不叫停提醒，只上报 error 态
        Ok(candidates) => candidates,
        Err(e) => {
            let mut reported = rt.reported_error.lock().unwrap();
            if reported.as_deref() != Some(e.as_str()) {
                tracing::warn!(target: "taix_plugin_reminder", "脚本错误: {e}");
                *reported = Some(e.clone());
                ctx.push_state_now("error", Some(json!({ "error": e })));
            }
            return;
        }
    };

    if candidates.is_empty() {
        report_state(ctx, rt, &snap, false);
        return;
    }

    let now = now_epoch();
    let action = {
        let mut cooldowns = rt.cooldowns.lock().unwrap();
        let Some(action) = pick_candidate(&candidates, &mut cooldowns, now) else {
            return;
        };
        action
    };

    notify(&action.title, &action.body, rt.sound.load(Ordering::Relaxed));
    tracing::info!(target: "taix_plugin_reminder", "已提醒: {}", action.title);
    *rt.last_notify.lock().unwrap() = Some(json!({
        "id": action.id,
        "title": action.title,
        "body": action.body,
        "at": rfc3339(now),
    }));
    report_state(ctx, rt, &snap, true);
}

/// 按序取第一条不在冷却中的；其余顺延 ROUND_DEFER_SECS
fn pick_candidate(
    candidates: &[RemindCandidate],
    cooldowns: &mut HashMap<String, i64>,
    now: i64,
) -> Option<RemindCandidate> {
    cooldowns.retain(|_, until| *until > now);
    let index = candidates
        .iter()
        .position(|c| cooldowns.get(&c.id).map_or(true, |until| *until <= now))?;
    let action = candidates[index].clone();
    cooldowns.insert(action.id.clone(), now + action.cooldown_minutes * 60);
    for c in candidates {
        if c.id != action.id {
            let until = cooldowns.entry(c.id.clone()).or_insert(0);
            if *until < now + ROUND_DEFER_SECS {
                *until = now + ROUND_DEFER_SECS;
            }
        }
    }
    Some(action)
}

fn main() {
    let rt = Arc::new(Runtime::new());

    let cfg_rt = rt.clone();
    let current_rt = rt.clone();
    let cmd_rt = rt.clone();
    let tick_rt = rt.clone();

    PluginApp::new(PLUGIN_ID)
        .on_config_changed(move |_, cfg: &ReminderConfig| {
            cfg_rt.sound.store(cfg.sound, Ordering::Relaxed);
            let script = cfg
                .script
                .clone()
                .filter(|s| !s.trim().is_empty())
                .unwrap_or_else(|| DEFAULT_SCRIPT.to_string());
            *cfg_rt.candidate.lock().unwrap() = Some(script);
        })
        .on_current(move |_, event| {
            let sleeping = event["sleep"].as_bool().unwrap_or(false);
            if let Some(app) = event["app"].as_object() {
                *current_rt.current_process.lock().unwrap() =
                    app["process"].as_str().unwrap_or_default().to_string();
            }
            current_rt.sleeping.store(sleeping, Ordering::Relaxed);
            *current_rt.continuous_since.lock().unwrap() = event["continuousSince"]
                .as_str()
                .and_then(parse_rfc3339);
        })
        .on_command("dry-run", move |ctx, data| {
            let Some(req) = Request::parse(data.as_ref()) else {
                tracing::warn!(target: "taix_plugin_reminder", "dry-run 缺少 nonce，忽略");
                return;
            };
            let script = req.str_field("script").unwrap_or_default().to_string();

            let reply = match build_snapshot(ctx, &cmd_rt) {
                Ok(snap) => match run_script(&script, &snap) {
                    Ok(candidates) => {
                        let now = now_epoch();
                        let cooldowns = cmd_rt.cooldowns.lock().unwrap();
                        let rows: Vec<Value> = candidates
                            .iter()
                            .map(|c| {
                                let left = cooldowns
                                    .get(&c.id)
                                    .map(|until| (*until - now).max(0))
                                    .unwrap_or(0);
                                json!({
                                    "id": c.id,
                                    "title": c.title,
                                    "body": c.body,
                                    "cooldownMinutes": c.cooldown_minutes,
                                    "cooldownLeftSeconds": left,
                                })
                            })
                            .collect();
                        let picked = candidates
                            .iter()
                            .position(|c| cooldowns.get(&c.id).map_or(true, |until| *until <= now));
                        Reply::ok(
                            req.nonce.clone(),
                            json!({
                                "candidates": rows,
                                "pickedIndex": picked,
                                "snapshot": snapshot_view(&snap),
                            }),
                        )
                    }
                    Err(e) => Reply::err(req.nonce.clone(), e),
                },
                Err(e) => Reply::err(req.nonce.clone(), format!("取今日数据失败: {e:#}")),
            };
            ctx.reply("dryRun", &reply);
        })
        .on_tick(TICK, move |ctx| tick(ctx, &tick_rt))
        .run()
        .expect("reminder 插件启动失败");
}

#[cfg(test)]
mod tests {
    use super::*;

    fn candidate(id: &str, cooldown_minutes: i64) -> RemindCandidate {
        RemindCandidate {
            id: id.to_string(),
            title: id.to_string(),
            body: String::new(),
            cooldown_minutes,
        }
    }

    const NOW: i64 = 1_000_000;

    #[test]
    fn picks_first_candidate_not_cooling() {
        let mut cooldowns = HashMap::from([("a".to_string(), NOW + 60)]);
        let action = pick_candidate(&[candidate("a", 30), candidate("b", 30)], &mut cooldowns, NOW)
            .expect("b 可提醒");
        assert_eq!(action.id, "b");
        assert_eq!(cooldowns["b"], NOW + 30 * 60);
        assert_eq!(cooldowns["a"], NOW + ROUND_DEFER_SECS);
    }

    #[test]
    fn others_are_deferred_by_round_gap() {
        let mut cooldowns = HashMap::new();
        let action = pick_candidate(&[candidate("a", 30), candidate("b", 30)], &mut cooldowns, NOW)
            .expect("a 可提醒");
        assert_eq!(action.id, "a");
        assert_eq!(cooldowns["a"], NOW + 30 * 60);
        assert_eq!(cooldowns["b"], NOW + ROUND_DEFER_SECS);
    }

    #[test]
    fn long_cooldown_is_not_shortened() {
        let mut cooldowns = HashMap::from([("b".to_string(), NOW + 3600)]);
        let action = pick_candidate(&[candidate("a", 30), candidate("b", 30)], &mut cooldowns, NOW)
            .expect("a 可提醒");
        assert_eq!(action.id, "a");
        assert_eq!(cooldowns["b"], NOW + 3600);
    }

    #[test]
    fn returns_none_when_all_cooling() {
        let mut cooldowns = HashMap::from([
            ("a".to_string(), NOW + 60),
            ("b".to_string(), NOW + 60),
        ]);
        assert!(
            pick_candidate(&[candidate("a", 30), candidate("b", 30)], &mut cooldowns, NOW).is_none()
        );
    }

    #[test]
    fn expired_cooldowns_are_cleaned() {
        let mut cooldowns = HashMap::from([("old".to_string(), NOW - 5)]);
        let action = pick_candidate(&[candidate("a", 30)], &mut cooldowns, NOW).expect("a 可提醒");
        assert_eq!(action.id, "a");
        assert!(!cooldowns.contains_key("old"));
    }
}
