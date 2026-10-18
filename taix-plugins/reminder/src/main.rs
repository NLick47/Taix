#![cfg_attr(all(windows, not(debug_assertions)), windows_subsystem = "windows")]

mod engine;

use std::collections::HashMap;
use std::sync::atomic::{AtomicBool, AtomicI64, AtomicU64, Ordering};
use std::sync::{Arc, Mutex};
use std::time::{Duration, SystemTime, UNIX_EPOCH};

use serde::{Deserialize, Serialize};
use serde_json::{json, Value};
use taix_plugin_sdk::{PluginApp, PluginCtx, Reply, Request};

use engine::{run_script, RemindCandidate, Snapshot};

const PLUGIN_ID: &str = "reminder";
/// 与 plugin.json 的 name 一致，Windows 通知头部显示的就是它
const PLUGIN_NAME: &str = "使用时长提醒";
const TICK: Duration = Duration::from_secs(5);
const HEARTBEAT_TICKS: u64 = 6;
/// 两次 tick 的墙钟差超过它 = 进程被挂起过（系统睡眠/唤醒），该拍只做状态对齐
const RESUMED_GAP_SECS: i64 = 60;
/// 其余命中让位，防连弹
const ROUND_DEFER_SECS: i64 = 300;
/// 兜底探测 /api/apptimer/current 的最小间隔
const CURRENT_PROBE_INTERVAL: i64 = 60;
/// 通知图标，编译进二进制，免得依赖部署时拷文件
const ICON_PNG: &[u8] = include_bytes!("../assets/icon.png");

#[derive(Debug, Clone, Default, Deserialize)]
struct ReminderConfig {
    #[serde(default)]
    sound: bool,
    #[serde(default)]
    script: Option<String>,
}

struct Runtime {
    /// 通知身份（Windows 的 AUMID 与已落盘图标）；首次发通知时才建，
    /// 因为建身份要读写注册表，失败只靠日志报，而 SDK 的日志在 run() 里才就绪
    identity: Mutex<Option<taix_notify::AppIdentity>>,
    /// 连续使用段起点；None = 空闲或还没收到过 current
    continuous_since: Mutex<Option<i64>>,
    /// 上次兜底探测 /api/apptimer/current 的 epoch；0 = 从未探测
    current_probed: AtomicI64,
    /// SSE current 事件计数器；探测结果落地前用它确认「期间没有更新的状态」
    current_events: AtomicU64,
    gate: Mutex<Gate>,
    /// state.json 只读一次，之后以内存为准
    state_loaded: AtomicBool,
    /// 上一拍 tick 的 epoch 秒；0 = 还没跑过（用于识别系统睡眠造成的空档）
    last_tick_at: AtomicI64,
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
            identity: Mutex::new(None),
            continuous_since: Mutex::new(None),
            current_probed: AtomicI64::new(0),
            current_events: AtomicU64::new(0),
            gate: Mutex::new(Gate::default()),
            state_loaded: AtomicBool::new(false),
            last_tick_at: AtomicI64::new(0),
            last_notify: Mutex::new(None),
            current_process: Mutex::new(String::new()),
            sleeping: AtomicBool::new(false),
            sound: AtomicBool::new(false),
            script: Mutex::new(String::new()),
            candidate: Mutex::new(None),
            script_error: Mutex::new(None),
            reported_error: Mutex::new(None),
            ticks: AtomicU64::new(0),
        }
    }
}

/// 提醒闸门：冷却（间隔）+ 每天仅一次。存 state.json，重启后不丢
#[derive(Default, Serialize, Deserialize)]
struct Gate {
    /// 规则 id → 冷却截止（epoch 秒）
    cooldowns: HashMap<String, i64>,
    /// 规则 id → 上次提醒的本地日期；只记 oncePerDay 的规则
    fired_on: HashMap<String, String>,
}

impl Gate {
    fn prune(&mut self, now: i64) {
        self.cooldowns.retain(|_, until| *until > now);
    }

    fn cooldown_left(&self, id: &str, now: i64) -> i64 {
        self.cooldowns
            .get(id)
            .map(|until| (*until - now).max(0))
            .unwrap_or(0)
    }

    fn fired_today(&self, id: &str, today: &str) -> bool {
        self.fired_on.get(id).map(|d| d.as_str()) == Some(today)
    }

    fn is_blocked(&self, c: &RemindCandidate, now: i64, today: &str) -> bool {
        self.cooldown_left(&c.id, now) > 0
            || (c.once_per_day && self.fired_today(&c.id, today))
    }

    /// 只选择，不落冷却；通知真正发出后才 commit
    fn select(
        &self,
        candidates: &[RemindCandidate],
        now: i64,
        today: &str,
    ) -> Option<RemindCandidate> {
        candidates
            .iter()
            .find(|c| !self.is_blocked(c, now, today))
            .cloned()
    }

    /// 通知成功后落冷却/每日一次，其余命中顺延 ROUND_DEFER_SECS
    fn commit(
        &mut self,
        action: &RemindCandidate,
        candidates: &[RemindCandidate],
        now: i64,
        today: &str,
    ) {
        if action.once_per_day {
            self.fired_on
                .insert(action.id.clone(), today.to_string());
        }
        self.cooldowns
            .insert(action.id.clone(), now + action.cooldown_minutes * 60);

        for c in candidates {
            if c.id == action.id || self.fired_today(&c.id, today) {
                continue;
            }
            let until = self.cooldowns.entry(c.id.clone()).or_insert(0);
            if *until < now + ROUND_DEFER_SECS {
                *until = now + ROUND_DEFER_SECS;
            }
        }
    }

    /// 测试/纯逻辑用：选中并立即落闸门
    #[cfg(test)]
    fn pick(
        &mut self,
        candidates: &[RemindCandidate],
        now: i64,
        today: &str,
    ) -> Option<RemindCandidate> {
        let action = self.select(candidates, now, today)?;
        self.commit(&action, candidates, now, today);
        Some(action)
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

/// 宿主机本地时区；返回 (时, 周一=1…周日=7, 日期)。
fn local_clock() -> (i64, i64, String) {
    use chrono::{Datelike, Local, Timelike};
    let now = Local::now();
    (
        now.hour() as i64,
        now.weekday().number_from_monday() as i64,
        now.format("%Y-%m-%d").to_string(),
    )
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
    let now = now_epoch();
    let since = *rt.continuous_since.lock().unwrap();

    // 持锁做阻塞 IO 会卡住 on_current 回调线程，所以只在锁外请求、拿到值再写回
    if should_probe(rt.current_probed.load(Ordering::Relaxed), now) {
        let events = rt.current_events.load(Ordering::Relaxed);
        if let Ok(current) = ctx.get_json("/api/apptimer/current") {
            rt.current_probed.store(now, Ordering::Relaxed);
            // 探测期间有 current 事件到达 = SSE 状态更新鲜，丢弃这次探测结果
            if rt.current_events.load(Ordering::Relaxed) == events {
                let (sleeping, start) = parse_probe(&current);
                rt.sleeping.store(sleeping, Ordering::Relaxed);
                *rt.continuous_since.lock().unwrap() = start;
                return start.map(|start| (now - start).max(0)).unwrap_or(0);
            }
        }
    }
    since.map(|start| (now - start).max(0)).unwrap_or(0)
}

/// 探测响应 → (睡眠/空闲中, 连续段起点)；continuousSince=null = 无段，
/// 插件重启或错过 SSE 后靠它一次对齐睡眠状态与连续段
fn parse_probe(current: &Value) -> (bool, Option<i64>) {
    (
        current["sleep"].as_bool().unwrap_or(false),
        current["continuousSince"].as_str().and_then(parse_rfc3339),
    )
}

/// 每 60s 与 server 对齐一次睡眠状态与连续段起点。SSE 断线时收不到 current
/// 事件，这里是唯一的自愈通道——所以不能只在「没有起点」时才探：起点看起来
/// 正常但已经过时（期间发生过 sleep/wake），会按旧起点继续计时，最长要等
/// 12 小时才被兜底发现。
fn should_probe(last_probe: i64, now: i64) -> bool {
    last_probe + CURRENT_PROBE_INTERVAL <= now
}

/// 相邻两拍墙钟差超阈值 = 进程被挂起过（系统睡眠/唤醒），需要重新对齐状态
fn resumed_from_gap(last_tick: i64, now: i64) -> bool {
    last_tick > 0 && now - last_tick > RESUMED_GAP_SECS
}

fn build_snapshot(ctx: &PluginCtx, rt: &Runtime) -> anyhow::Result<Snapshot> {
    let summary = ctx.get_json("/api/data/today-summary?includeOngoing=1")?;
    let sleeping = rt.sleeping.load(Ordering::Relaxed);
    let (hour, weekday, _) = local_clock();

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

/// 首次调用时建身份并缓存；注册失败只记日志，通知仍走共享 AUMID
fn identity(rt: &Runtime) -> taix_notify::AppIdentity {
    let mut slot = rt.identity.lock().unwrap();
    if let Some(identity) = slot.clone() {
        return identity;
    }
    let identity = taix_notify::app_identity(PLUGIN_ID, PLUGIN_NAME, ICON_PNG);
    *slot = Some(identity.clone());
    identity
}

/// 同步等待通知结果（最多 5s）；失败时不落冷却，下一轮可重试
fn notify(rt: &Runtime, title: &str, body: &str, sound: bool) -> bool {
    let (t, b) = (title.to_string(), body.to_string());
    let identity = identity(rt);
    let (tx, rx) = std::sync::mpsc::channel();
    std::thread::spawn(move || {
        let opts = taix_notify::NotifyOptions {
            aumid: identity.aumid,
            sound: sound.then_some("default"),
            icon: identity.icon,
            ..Default::default()
        };
        let ok = match taix_notify::notify(&t, &b, &opts) {
            Ok(()) => true,
            Err(e) => {
                tracing::warn!(target: "taix_plugin_reminder", "通知发送失败: {e:#}");
                false
            }
        };
        let _ = tx.send(ok);
    });
    match rx.recv_timeout(Duration::from_secs(5)) {
        Ok(ok) => ok,
        Err(_) => {
            tracing::warn!(target: "taix_plugin_reminder", "通知发送超时");
            false
        }
    }
}

/// 错误变化立即报，平时 30s 心跳
fn report_state(ctx: &PluginCtx, rt: &Runtime, snap: &Snapshot, force: bool) {
    let ticks = rt.ticks.fetch_add(1, Ordering::Relaxed) + 1;
    if !force && !ticks.is_multiple_of(HEARTBEAT_TICKS) {
        return;
    }

    let script_error = rt.script_error.lock().unwrap().clone();
    let last_notify = rt.last_notify.lock().unwrap().clone();
    let cooldown_until = {
        let now = now_epoch();
        rt.gate
            .lock()
            .unwrap()
            .cooldowns
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
            let changed = {
                let mut reported = rt.reported_error.lock().unwrap();
                let changed = reported.as_deref() != Some(err.as_str());
                if changed {
                    *reported = Some(err.clone());
                }
                changed
            };
            data["error"] = json!(err);
            if let Some(until) = cooldown_until {
                data["cooldownUntil"] = json!(rfc3339(until));
            }
            if let Some(last) = last_notify {
                data["lastNotify"] = last;
            }
            if changed || force {
                ctx.push_state_now("error", Some(data));
            } else {
                ctx.push_state("error", Some(data));
            }
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
    if !rt.state_loaded.swap(true, Ordering::Relaxed) {
        if let Some(saved) = ctx.state_store().load::<Gate>() {
            *rt.gate.lock().unwrap() = saved;
        }
    }

    if rt.sleeping.load(Ordering::Relaxed) {
        tracing::debug!(target: "taix_plugin_reminder", "空闲/睡眠中，跳过本轮");
        return;
    }

    let now = now_epoch();
    let last_tick = rt.last_tick_at.swap(now, Ordering::Relaxed);
    let resumed = resumed_from_gap(last_tick, now);
    if resumed {
        // 系统睡眠/挂起后进程恢复：本地连续段与睡眠标记可能都已过时。
        // 清空连续段并强制探测对齐，本拍不评估规则，避免「开盖瞬间弹陈旧时长」
        rt.current_probed.store(0, Ordering::Relaxed);
        *rt.continuous_since.lock().unwrap() = None;
        tracing::info!(
            target: "taix_plugin_reminder",
            "tick 空档 {}s（系统睡眠/挂起），本拍只做状态对齐",
            now - last_tick
        );
    }

    let snap = match build_snapshot(ctx, rt) {
        Ok(snap) => snap,
        Err(e) => {
            tracing::warn!(target: "taix_plugin_reminder", "取今日数据失败: {e:#}");
            return;
        }
    };
    tracing::debug!(
        target: "taix_plugin_reminder",
        "tick: continuous={}s today={}s apps={} cats={} proc={}",
        snap.continuous_seconds,
        snap.today_total_seconds,
        snap.apps.len(),
        snap.categories.len(),
        snap.current_process
    );

    if resumed {
        report_state(ctx, rt, &snap, false);
        return;
    }

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
            // 文案没变时也要推一次，否则界面上的详情会停在第一次报错那一刻
            let changed = {
                let mut reported = rt.reported_error.lock().unwrap();
                let changed = reported.as_deref() != Some(e.as_str());
                if changed {
                    *reported = Some(e.clone());
                }
                changed
            };
            if changed {
                tracing::warn!(target: "taix_plugin_reminder", "脚本错误: {e}");
                ctx.push_state_now("error", Some(json!({ "error": e })));
            } else {
                ctx.push_state("error", Some(json!({ "error": e })));
            }
            return;
        }
    };

    if candidates.is_empty() {
        report_state(ctx, rt, &snap, false);
        return;
    }

    let now = now_epoch();
    let (_, _, today) = local_clock();

    let action = {
        let mut gate = rt.gate.lock().unwrap();
        // 已删规则的冷却可能残留，复用其 id 会被旧冷却压制
        gate.prune(now);
        gate.select(&candidates, now, &today)
    };

    let Some(action) = action else {
        // 全在冷却：仍要发心跳，否则详情页的剩余时间与连续时长一起冻住
        tracing::debug!(
            target: "taix_plugin_reminder",
            "命中 {} 条但全在冷却/今天已提醒，本轮不提醒",
            candidates.len()
        );
        report_state(ctx, rt, &snap, false);
        return;
    };

    let delivered = notify(
        rt,
        &action.title,
        &action.body,
        rt.sound.load(Ordering::Relaxed),
    );
    if !delivered {
        // 没送达不落 fired_on；短退避防每 5s 重试风暴
        rt.gate
            .lock()
            .unwrap()
            .cooldowns
            .insert(action.id.clone(), now + 60);
        tracing::warn!(
            target: "taix_plugin_reminder",
            "提醒未送达，60s 后重试 id={}",
            action.id
        );
        report_state(ctx, rt, &snap, false);
        return;
    }
    tracing::info!(
        target: "taix_plugin_reminder",
        "已提醒 id={} title={} body={}",
        action.id,
        action.title,
        action.body
    );
    *rt.last_notify.lock().unwrap() = Some(json!({
        "id": action.id,
        "title": action.title,
        "body": action.body,
        "at": rfc3339(now),
    }));
    {
        let mut gate = rt.gate.lock().unwrap();
        gate.commit(&action, &candidates, now, &today);
        if let Err(e) = ctx.state_store().save(&*gate) {
            tracing::warn!(target: "taix_plugin_reminder", "冷却状态未持久化: {e:#}");
        }
    }
    report_state(ctx, rt, &snap, true);
}

fn main() {
    let rt = Arc::new(Runtime::new());

    // 启动即主动申请 macOS 通知权限：首次询问发生在「启用插件/登录」时刻，
    // 而不是第一次提醒才弹（未授权也不会丢提醒——发送时会先尝试权限、超时回退 osascript）
    #[cfg(not(target_os = "windows"))]
    std::thread::spawn(taix_notify::request_permission);

    let cfg_rt = rt.clone();
    let current_rt = rt.clone();
    let cmd_rt = rt.clone();
    let tick_rt = rt.clone();

    PluginApp::new(PLUGIN_ID)
        .on_config_changed(move |_, cfg: &ReminderConfig| {
            cfg_rt.sound.store(cfg.sound, Ordering::Relaxed);
            let script = cfg.script.clone().unwrap_or_default();
            *cfg_rt.candidate.lock().unwrap() = Some(script);
        })
        .on_current(move |_, event| {
            current_rt.current_events.fetch_add(1, Ordering::Relaxed);
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
                        let (_, _, today) = local_clock();
                        let gate = cmd_rt.gate.lock().unwrap();
                        let rows: Vec<Value> = candidates
                            .iter()
                            .map(|c| {
                                json!({
                                    "id": c.id,
                                    "title": c.title,
                                    "body": c.body,
                                    "cooldownMinutes": c.cooldown_minutes,
                                    "cooldownLeftSeconds": gate.cooldown_left(&c.id, now),
                                    "oncePerDay": c.once_per_day,
                                    "firedToday": gate.fired_today(&c.id, &today),
                                })
                            })
                            .collect();
                        let picked = candidates
                            .iter()
                            .position(|c| !gate.is_blocked(c, now, &today));
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
            once_per_day: false,
        }
    }

    fn candidate_once(id: &str, cooldown_minutes: i64) -> RemindCandidate {
        RemindCandidate {
            once_per_day: true,
            ..candidate(id, cooldown_minutes)
        }
    }

    const NOW: i64 = 1_000_000;
    const DAY: &str = "2026-10-13";

    /// 按真实节拍模拟 seconds 秒，返回实际提醒的 (时刻, 规则 id)
    fn simulate(hits: &[RemindCandidate], seconds: i64) -> Vec<(i64, String)> {
        let step = TICK.as_secs() as i64;
        let mut gate = Gate::default();
        let mut fired = Vec::new();
        let mut tick = 0i64;
        while tick * step < seconds {
            let now = NOW + tick * step;
            gate.prune(now);
            if let Some(action) = gate.pick(hits, now, DAY) {
                fired.push((now, action.id.clone()));
            }
            tick += 1;
        }
        fired
    }

    #[test]
    fn picks_first_candidate_not_cooling() {
        let mut gate = Gate::default();
        gate.cooldowns.insert("a".to_string(), NOW + 60);
        let action = gate
            .pick(&[candidate("a", 30), candidate("b", 30)], NOW, DAY)
            .expect("b 可提醒");
        assert_eq!(action.id, "b");
        assert_eq!(gate.cooldowns["b"], NOW + 30 * 60);
        assert_eq!(gate.cooldowns["a"], NOW + ROUND_DEFER_SECS);
    }

    #[test]
    fn others_are_deferred_by_round_gap() {
        let mut gate = Gate::default();
        let action = gate
            .pick(&[candidate("a", 30), candidate("b", 30)], NOW, DAY)
            .expect("a 可提醒");
        assert_eq!(action.id, "a");
        assert_eq!(gate.cooldowns["a"], NOW + 30 * 60);
        assert_eq!(gate.cooldowns["b"], NOW + ROUND_DEFER_SECS);
    }

    #[test]
    fn long_cooldown_is_not_shortened() {
        let mut gate = Gate::default();
        gate.cooldowns.insert("b".to_string(), NOW + 3600);
        let action = gate
            .pick(&[candidate("a", 30), candidate("b", 30)], NOW, DAY)
            .expect("a 可提醒");
        assert_eq!(action.id, "a");
        assert_eq!(gate.cooldowns["b"], NOW + 3600);
    }

    #[test]
    fn returns_none_when_all_cooling() {
        let mut gate = Gate::default();
        gate.cooldowns.insert("a".to_string(), NOW + 60);
        gate.cooldowns.insert("b".to_string(), NOW + 60);
        assert!(gate.pick(&[candidate("a", 30), candidate("b", 30)], NOW, DAY).is_none());
    }

    #[test]
    fn expired_cooldowns_are_cleaned() {
        let mut gate = Gate::default();
        gate.cooldowns.insert("old".to_string(), NOW - 5);
        gate.prune(NOW);
        assert!(!gate.cooldowns.contains_key("old"), "过期冷却应被清掉");
        assert_eq!(gate.cooldown_left("old", NOW), 0);
    }

    #[test]
    fn cooldown_repeats_at_exact_interval() {
        // 连续使用规则：冷却 30 分钟，条件一直成立 → 应每 30 分钟提醒一次
        let fired = simulate(&[candidate("a", 30)], 24 * 3600);
        let times: Vec<i64> = fired.iter().map(|(t, _)| *t - NOW).collect();

        assert_eq!(times[0], 0, "首次命中立即提醒");
        assert_eq!(times[1], 30 * 60, "30 分钟整才可再次提醒");
        assert!(
            times.windows(2).all(|w| w[1] - w[0] == 30 * 60),
            "间隔应恒为 30 分钟：{times:?}"
        );
        assert_eq!(fired.len(), 48, "24 小时内应提醒 48 次：{times:?}");
    }

    #[test]
    fn cooldown_blocks_the_ticks_in_between() {
        let fired = simulate(&[candidate("a", 30)], 30 * 60);
        assert_eq!(fired.len(), 1, "冷却期内不应重复提醒");
    }

    #[test]
    fn two_hits_never_fire_back_to_back() {
        // a 冷却 30 分钟、b 冷却 60 分钟，两者一直命中
        let fired = simulate(&[candidate("a", 30), candidate("b", 60)], 3 * 3600);
        let seq: Vec<(i64, String)> = fired
            .iter()
            .map(|(t, id)| (*t - NOW, id.clone()))
            .collect();

        assert_eq!(seq[0], (0, "a".to_string()), "先按脚本顺序提醒 a");
        assert_eq!(seq[1], (300, "b".to_string()), "b 让位 5 分钟");
        assert_eq!(seq[2], (1800, "a".to_string()), "a 冷却到期");
        assert_eq!(seq[3], (3600, "a".to_string()));

        let min_gap = seq.windows(2).map(|w| w[1].0 - w[0].0).min().unwrap();
        assert_eq!(min_gap, 300, "任意两次提醒至少隔 5 分钟：{seq:?}");
    }

    #[test]
    fn once_per_day_fires_only_on_first_day() {
        let mut gate = Gate::default();
        let hits = [candidate_once("a", 1)];

        assert_eq!(gate.pick(&hits, NOW, DAY).map(|c| c.id), Some("a".to_string()));
        assert!(gate.is_blocked(&hits[0], NOW + 120, DAY), "冷却已过但当天已提醒");
        assert!(gate.pick(&hits, NOW + 120, DAY).is_none());
        assert!(gate.pick(&hits, NOW + 3600, DAY).is_none());

        assert_eq!(
            gate.pick(&hits, NOW + 3600, "2026-10-14").map(|c| c.id),
            Some("a".to_string()),
            "跨天自动重置"
        );
    }

    #[test]
    fn once_per_day_does_not_block_other_rules() {
        let mut gate = Gate::default();
        let hits = [candidate_once("a", 30), candidate("b", 30)];

        assert_eq!(gate.pick(&hits, NOW, DAY).map(|c| c.id), Some("a".to_string()));
        assert_eq!(
            gate.pick(&hits, NOW + ROUND_DEFER_SECS, DAY).map(|c| c.id),
            Some("b".to_string()),
            "a 当天不再提醒后应轮到 b"
        );
    }

    #[test]
    fn repeated_rule_keeps_firing_next_day() {
        let mut gate = Gate::default();
        let hits = [candidate("a", 30)];
        assert!(gate.pick(&hits, NOW, DAY).is_some());
        assert!(gate.pick(&hits, NOW + 31 * 60, "2026-10-14").is_some());
    }

    #[test]
    fn probe_is_throttled_to_a_minute() {
        assert!(should_probe(0, NOW), "从未探测过要先探测一次");
        assert!(!should_probe(NOW, NOW + 5), "刚探测过不要每拍请求");
        assert!(!should_probe(NOW, NOW + CURRENT_PROBE_INTERVAL - 1), "差 1 秒仍在窗口内");
        assert!(should_probe(NOW, NOW + CURRENT_PROBE_INTERVAL), "整 60 秒放开");
        assert!(should_probe(NOW, NOW + 12 * 3600), "长时间后同样放开");
    }

    #[test]
    fn resume_gap_detection() {
        assert!(!resumed_from_gap(0, NOW), "首拍没有上一拍，不算恢复");
        assert!(!resumed_from_gap(NOW - 5, NOW), "常规 5s 节拍不算");
        assert!(!resumed_from_gap(NOW - RESUMED_GAP_SECS, NOW), "恰好阈值不算");
        assert!(resumed_from_gap(NOW - RESUMED_GAP_SECS - 1, NOW), "超过阈值视为恢复");
        assert!(!resumed_from_gap(NOW + 5, NOW), "时钟回拨不算恢复");
    }

    #[test]
    fn local_clock_matches_system_local_time() {
        // 与 chrono::Local 对齐：开发机不在 +8 时能抓住“又写死 +8”的回归。
        // 跨时区完整验证在 script/verify_local_timezone.py（真身进程 + TZ）
        use chrono::{Datelike, Timelike};
        let (hour, weekday, date) = local_clock();
        let now = chrono::Local::now();
        assert_eq!(hour, now.hour() as i64);
        assert_eq!(weekday, now.weekday().number_from_monday() as i64);
        assert_eq!(date, now.format("%Y-%m-%d").to_string());
    }

    #[test]
    fn probe_result_carries_sleep_state_and_segment() {
        // 空闲/睡眠：sleep=true 且无连续段 —— 两者都要落地，插件据此冻结
        let sleeping = json!({ "sleep": true, "continuousSince": null });
        assert_eq!(parse_probe(&sleeping), (true, None));

        let awake = json!({ "sleep": false, "continuousSince": "1970-01-01T00:01:00Z" });
        assert_eq!(parse_probe(&awake), (false, Some(60)));

        // 字段缺失按「醒着、无段」兜底，不 panic
        assert_eq!(parse_probe(&json!({})), (false, None));
    }

    #[test]
    fn gate_survives_restart() {
        let mut gate = Gate::default();
        gate.pick(&[candidate_once("a", 30), candidate("b", 45)], NOW, DAY)
            .expect("a 可提醒");

        let mut restored: Gate =
            serde_json::from_str(&serde_json::to_string(&gate).unwrap()).unwrap();
        assert_eq!(restored.cooldown_left("b", NOW), ROUND_DEFER_SECS);
        assert!(restored.is_blocked(&candidate_once("a", 30), NOW + 600, DAY), "当天已提醒应跨重启保留");
        assert!(restored.pick(&[candidate_once("a", 30)], NOW + 600, DAY).is_none());
        assert!(
            restored
                .pick(&[candidate_once("a", 30)], NOW + 31 * 60, "2026-10-14")
                .is_some(),
            "冷却过期且换了日期应可再提醒"
        );
    }
}
