use serde_json::{json, Value};
use std::io::{BufRead, BufReader, Write};
use std::path::{Path, PathBuf};
use std::sync::atomic::Ordering;
use std::sync::mpsc::{self, RecvTimeoutError};
use std::sync::Arc;
use std::time::Duration;

use taix_plugin_sdk::PluginApp;
use taix_server::plugins::PluginState;
use taix_server::routes::web_sentry::SentryState;
use taix_server::services::config::ConfigService;

const PLUGIN_ID: &str = "itdemo";
const TIMEOUT: Duration = Duration::from_secs(10);

#[derive(Debug)]
enum Event {
    Config { greet: String },
    Command(Option<Value>),
    Current(Value),
}

#[derive(Debug, serde::Deserialize)]
struct ItConfig {
    #[serde(default)]
    greet: String,
}

struct ServerHandle {
    serve: tokio::task::JoinHandle<()>,
    pool: sqlx::SqlitePool,
    plugins: Arc<PluginState>,
}

/// 「重启」要整个 runtime 关停：abort 只杀 accept 循环，SSE 长连接仍存活
struct TestServer {
    port: u16,
    rt: tokio::runtime::Runtime,
    handle: Option<ServerHandle>,
}

impl TestServer {
    fn start(data_dir: &Path, port: u16) -> TestServer {
        let rt = tokio::runtime::Builder::new_multi_thread()
            .enable_all()
            .build()
            .expect("tokio runtime 构建失败");
        let handle = rt
            .block_on(start_server(data_dir, port))
            .expect("server 启动失败");
        TestServer { port, rt, handle: Some(handle) }
    }

    fn stop(self) {
        drop(self.handle);
        self.rt.shutdown_timeout(Duration::from_secs(3));
    }
}

fn http_json(method: &str, port: u16, path: &str, body: Option<&Value>) -> Value {
    let url = format!("http://127.0.0.1:{}{}", port, path);
    let method = match method {
        "GET" => attohttpc::Method::GET,
        "POST" => attohttpc::Method::POST,
        "PUT" => attohttpc::Method::PUT,
        other => panic!("unsupported method {}", other),
    };
    let method_label = method.to_string();
    let sent = if let Some(b) = body {
        attohttpc::RequestBuilder::new(method, &url)
            .header("Content-Type", "application/json")
            .text(b.to_string())
            .send()
    } else {
        attohttpc::RequestBuilder::new(method, &url).send()
    };
    let resp = sent.expect("请求失败（server 未就绪?）");
    assert_eq!(resp.status().as_u16(), 200, "{} {} -> {}", method_label, path, resp.status());
    let v: Value = serde_json::from_str(&resp.text().unwrap()).expect("响应不是合法 JSON");
    assert_eq!(v["code"], 0, "{} {} 业务失败: {}", method_label, path, v);
    v["data"].clone()
}

fn post_command(port: u16, cmd: &str, data: Value) -> Value {
    http_json("POST", port, &format!("/api/plugins/{}/commands", PLUGIN_ID),
              Some(&json!({ "cmd": cmd, "data": data })))
}

fn http_json_raw(method: &str, port: u16, path: &str, body: Option<&Value>) -> Value {
    let url = format!("http://127.0.0.1:{}{}", port, path);
    let method = match method {
        "GET" => attohttpc::Method::GET,
        "POST" => attohttpc::Method::POST,
        "PUT" => attohttpc::Method::PUT,
        other => panic!("unsupported method {}", other),
    };
    let sent = if let Some(b) = body {
        attohttpc::RequestBuilder::new(method, &url)
            .header("Content-Type", "application/json")
            .text(b.to_string())
            .send()
    } else {
        attohttpc::RequestBuilder::new(method, &url).send()
    };
    let resp = sent.expect("请求失败");
    serde_json::from_str(&resp.text().unwrap()).expect("响应不是合法 JSON")
}

/// 把每帧 data: 送进 channel，验证订阅过滤用
fn open_sse(port: u16, sub: &str) -> mpsc::Receiver<String> {
    open_sse_after(port, sub, 0)
}

fn open_sse_after(port: u16, sub: &str, after: u64) -> mpsc::Receiver<String> {
    let (tx, rx) = mpsc::channel();
    let url = format!(
        "http://127.0.0.1:{}/api/events?sub={}&after={}",
        port, sub, after
    );
    std::thread::spawn(move || {
        let Ok(resp) = attohttpc::get(&url)
            .header("Accept", "text/event-stream")
            .send()
        else {
            return;
        };
        for line in BufReader::new(resp).lines() {
            let Ok(line) = line else { break };
            if let Some(rest) = line.strip_prefix("data:") {
                if tx.send(rest.trim_start().to_string()).is_err() {
                    break;
                }
            }
        }
    });
    rx
}

fn expect_raw_command(rx: &mpsc::Receiver<String>, step: &str) {
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        let now = std::time::Instant::now();
        assert!(now < deadline, "等待 after 补投指令 {} 超时", step);
        match rx.recv_timeout(deadline - now) {
            Ok(payload) => {
                let v: Value = serde_json::from_str(&payload).expect("SSE 帧不是 JSON");
                if v["type"] == json!("command") {
                    assert_eq!(v["data"]["step"], json!(step), "补投指令内容不符: {v}");
                    return;
                }
            }
            Err(RecvTimeoutError::Timeout) => panic!("等待 after 补投指令 {} 超时", step),
            Err(RecvTimeoutError::Disconnected) => panic!("SSE 连接已断开"),
        }
    }
}

fn expect_no_raw_command(rx: &mpsc::Receiver<String>, ms: u64) {
    let deadline = std::time::Instant::now() + Duration::from_millis(ms);
    while std::time::Instant::now() < deadline {
        if let Ok(payload) = rx.recv_timeout(Duration::from_millis(50)) {
            let v: Value = serde_json::from_str(&payload).expect("SSE 帧不是 JSON");
            assert_ne!(
                v["type"],
                json!("command"),
                "after 游标之后的连接不应重放旧指令: {v}"
            );
        }
    }
}

fn wait_health(port: u16) {
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        if let Ok(resp) = attohttpc::get(format!("http://127.0.0.1:{}/api/health", port)).send() {
            if resp.is_success() {
                return;
            }
        }
        assert!(std::time::Instant::now() < deadline, "server 健康检查超时");
        std::thread::sleep(Duration::from_millis(100));
    }
}

fn free_port() -> u16 {
    std::net::TcpListener::bind("127.0.0.1:0")
        .unwrap()
        .local_addr()
        .unwrap()
        .port()
}

fn write_manifest(data_dir: &Path) {
    let dir = data_dir.join("plugins").join(PLUGIN_ID);
    std::fs::create_dir_all(&dir).unwrap();
    std::fs::write(
        dir.join("plugin.json"),
        json!({
            "id": PLUGIN_ID,
            "name": "IT Demo",
            "version": "0.1.0",
            "entry": "itdemo-bin",
            "platforms": [],
            "display": false,
            "configSchema": [
                { "key": "greet", "label": "问候语", "type": "string", "default": "hello" }
            ],
        })
        .to_string(),
    )
    .unwrap();
    // 这测试用进程内 SDK，不会真的执行 entry；但启用时会校验文件存在
    std::fs::write(dir.join("itdemo-bin"), b"").unwrap();
}

async fn start_server(data_dir: &Path, port: u16) -> anyhow::Result<ServerHandle> {
    // SDK 与 server 的 create_app（Origin 白名单）都读 TAIX_SERVER；
    // pipe 路径指向临时目录，避免与真实 Taix 实例冲突。
    // TAIX_TZ 固定上海：本测试的「今日」边界断言按 +8 计算，不能跟随宿主机时区
    std::env::set_var("TAIX_SERVER", format!("http://127.0.0.1:{}", port));
    std::env::set_var("TAIX_PIPE_PATH", data_dir.join("monitor.sock").display().to_string());
    std::env::set_var("TAIX_TZ", "Asia/Shanghai");
    std::env::remove_var("TAIX_WEBUI_DIR");

    let pool = taix_server::db::init_db(data_dir.join("data.db").to_str().unwrap(), "Asia/Shanghai").await?;
    let config_service = Arc::new(ConfigService::new(data_dir));
    let (web_enabled_tx, _rx) = tokio::sync::watch::channel(false);
    let (tx, _rx2) = tokio::sync::broadcast::channel(64);
    let sentry_state = Arc::new(SentryState {
        pool: pool.clone(),
        is_sleep: tokio::sync::RwLock::new(false),
        tx,
        web_favicons_dir: data_dir.join("WebFavicons"),
        semaphore: Arc::new(tokio::sync::Semaphore::new(8)),
    });

    let plugins_dir = data_dir.join("plugins");
    let plugin_state = Arc::new(PluginState::init(&plugins_dir, data_dir).await?);

    let app = taix_server::create_app(
        pool.clone(),
        config_service,
        web_enabled_tx,
        plugin_state.clone(),
        data_dir,
    );

    let listener = tokio::net::TcpListener::bind(("127.0.0.1", port)).await?;
    let serve = tokio::spawn(async move {
        let _ = axum::serve(listener, app).await;
    });
    tokio::spawn(taix_server::pipe::start(
        sentry_state,
        pool.clone(),
        plugin_state.clone(),
    ));
    // 与 main.rs 同一接线：心跳看门狗（静默 → 空闲广播）
    tokio::spawn(taix_server::plugins::current::run_idle_watchdog(
        plugin_state.clone(),
        pool.clone(),
    ));

    Ok(ServerHandle {
        serve,
        pool,
        plugins: plugin_state,
    })
}

fn spawn_plugin(data_dir: &Path) -> mpsc::Receiver<Event> {
    let (tx, rx) = mpsc::channel();
    let args = vec![
        "run".to_string(),
        "--plugin".to_string(),
        PLUGIN_ID.to_string(),
        "--data-dir".to_string(),
        data_dir.display().to_string(),
    ];
    let app = PluginApp::new(PLUGIN_ID)
        .on_config_changed({
            let tx = tx.clone();
            move |_, c: &ItConfig| {
                let _ = tx.send(Event::Config { greet: c.greet.clone() });
            }
        })
        .on_command("ping", {
            let tx = tx.clone();
            move |_, data| {
                let _ = tx.send(Event::Command(data));
            }
        })
        .on_command("record", |ctx, data| {
            let d = data.expect("record 需要数据");
            ctx.record(d["k"].as_str().unwrap(), &d["v"]);
        })
        .on_command("push", |ctx, data| {
            ctx.push_state("working", data);
        })
        .on_current({
            let tx = tx.clone();
            move |_, ev| {
                let _ = tx.send(Event::Current(ev.clone()));
            }
        });
    std::thread::Builder::new()
        .name("it-plugin".into())
        .spawn(move || app.run_with_args(&args).expect("插件线程异常退出"))
        .unwrap();
    rx
}

fn expect_event(rx: &mpsc::Receiver<Event>, what: &str) -> Event {
    match rx.recv_timeout(TIMEOUT) {
        Ok(ev) => ev,
        Err(RecvTimeoutError::Timeout) => panic!("等待事件超时: {}", what),
        Err(RecvTimeoutError::Disconnected) => panic!("插件线程已退出: {}", what),
    }
}

fn expect_silence(rx: &mpsc::Receiver<Event>, ms: u64) {
    match rx.recv_timeout(Duration::from_millis(ms)) {
        Err(RecvTimeoutError::Timeout) => {} // 符合预期
        Ok(ev) => panic!("出现多余事件: {:?}", ev),
        Err(RecvTimeoutError::Disconnected) => panic!("插件线程已退出"),
    }
}

/// 等待指定 step 的指令；current 广播共用通道，跳过
fn expect_command(rx: &mpsc::Receiver<Event>, step: &str) {
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        let now = std::time::Instant::now();
        assert!(now < deadline, "等待指令 {} 超时", step);
        match rx.recv_timeout(deadline - now) {
            Ok(Event::Command(Some(d))) if d["step"] == json!(step) => return,
            Ok(Event::Command(other)) => panic!("指令内容不符，期望 {}: {:?}", step, other),
            Ok(Event::Current(_)) => continue,
            Ok(other) => panic!("意外事件，期望指令 {}: {:?}", step, other),
            Err(RecvTimeoutError::Timeout) => panic!("等待指令 {} 超时", step),
            Err(RecvTimeoutError::Disconnected) => panic!("插件线程已退出"),
        }
    }
}

#[cfg(unix)]
fn connect_monitor(data_dir: &Path) -> BufReader<std::os::unix::net::UnixStream> {
    let sock = data_dir.join("monitor.sock");
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        if let Ok(s) = std::os::unix::net::UnixStream::connect(&sock) {
            return BufReader::new(s);
        }
        assert!(std::time::Instant::now() < deadline, "连接 monitor socket 超时");
        std::thread::sleep(Duration::from_millis(100));
    }
}

#[test]
fn transport_contract() {
    let data_dir: PathBuf = std::env::temp_dir().join(format!("taix-plugin-it-{}", std::process::id()));
    let _ = std::fs::remove_dir_all(&data_dir);
    std::fs::create_dir_all(&data_dir).unwrap();
    write_manifest(&data_dir);

    // ---------- S0: 启动 server #1 ----------
    let port = free_port();
    let srv = TestServer::start(&data_dir, port);
    wait_health(port);
    // 插件必须先启用：禁用时指令/状态/SSE 都会被拒
    http_json(
        "POST",
        port,
        &format!("/api/plugins/{}/enabled", PLUGIN_ID),
        Some(&json!({ "enabled": true })),
    );
    // ---------- S1a: 指令队列溢出 → 丢弃最旧 + poll 取走式 ----------
    for i in 0..20 {
        post_command(port, "overflow", json!({ "i": i }));
    }
    let poll = http_json("GET", port, &format!("/api/plugins/{}/poll", PLUGIN_ID), None);
    let cmds = poll["commands"].as_array().expect("commands 数组").clone();
    assert_eq!(cmds.len(), 16, "队列应保留最近 16 条，实际 {}", cmds.len());
    assert_eq!(cmds[0]["data"]["i"], json!(4), "最旧的 4 条应被丢弃");
    assert_eq!(cmds[15]["data"]["i"], json!(19));
    let poll2 = http_json("GET", port, &format!("/api/plugins/{}/poll", PLUGIN_ID), None);
    assert_eq!(poll2["commands"].as_array().unwrap().len(), 0, "第二次 poll 应为空（取走式）");

    // ---------- S1b: 预置 3 条指令（插件上线后走 SSE 重放） ----------
    for step in ["a", "b", "c"] {
        post_command(port, "ping", json!({ "step": step }));
    }

    // ---------- S1c: 启动插件：初始配置 + 重放恰好一次 ----------
    let rx = spawn_plugin(&data_dir);
    match expect_event(&rx, "初始配置") {
        Event::Config { greet } => assert_eq!(
            greet, "",
            "server 只存原始 values（未配置时为空对象），字段默认由插件侧 serde(default) 兜底"
        ),
        other => panic!("首个事件应是配置，实际 {:?}", other),
    }
    for step in ["a", "b", "c"] {
        expect_command(&rx, step);
    }
    expect_silence(&rx, 500);

    // ---------- S2: 指令实时下发（SSE live） ----------
    post_command(port, "ping", json!({ "step": "live" }));
    expect_command(&rx, "live");
    expect_silence(&rx, 500);

    // ---------- S3: 配置热重载 ----------
    http_json("PUT", port, &format!("/api/plugins/{}/config", PLUGIN_ID),
              Some(&json!({ "values": { "greet": "hi" } })));
    match expect_event(&rx, "配置变更 1") {
        Event::Config { greet } => assert_eq!(greet, "hi"),
        other => panic!("配置事件异常: {:?}", other),
    }
    http_json("PUT", port, &format!("/api/plugins/{}/config", PLUGIN_ID),
              Some(&json!({ "values": { "greet": "yo" } })));
    match expect_event(&rx, "配置变更 2") {
        Event::Config { greet } => assert_eq!(greet, "yo"),
        other => panic!("配置事件异常: {:?}", other),
    }
    let cfg = http_json("GET", port, &format!("/api/plugins/{}/config", PLUGIN_ID), None);
    assert_eq!(cfg["version"], json!(2));

    // ---------- S4: records 往返 ----------
    post_command(port, "record", json!({ "k": "it/key", "v": { "n": 7 } }));
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        let rows = http_json("GET", port,
                             &format!("/api/plugins/{}/records?key=it%2Fkey", PLUGIN_ID), None);
        if rows.as_array().map_or(false, |r| !r.is_empty()) {
            assert_eq!(rows[0]["value"]["n"], json!(7));
            assert_eq!(rows[0]["key"], json!("it/key"));
            break;
        }
        assert!(std::time::Instant::now() < deadline, "records 未写入");
        std::thread::sleep(Duration::from_millis(200));
    }

    // ---------- S5: 状态上报节流（同 state 5s 内不重发） ----------
    // 指令异步处理：轮询等状态到位
    post_command(port, "push", json!({ "n": 1 }));
    wait_state_n(port, 1);
    assert_eq!(
        http_json("GET", port, &format!("/api/plugins/{}/state", PLUGIN_ID), None)["state"],
        json!("working")
    );

    post_command(port, "push", json!({ "n": 2 }));
    std::thread::sleep(Duration::from_millis(800));
    let state2 = http_json("GET", port, &format!("/api/plugins/{}/state", PLUGIN_ID), None);
    assert_eq!(state2["data"]["n"], json!(1), "5s 内同 state 应被节流（data 不变）");

    std::thread::sleep(Duration::from_millis(4600));
    post_command(port, "push", json!({ "n": 3 }));
    wait_state_n(port, 3);

    // ---------- S6: current 广播 + 睡眠唤醒（pipe，unix socket） ----------
    #[cfg(unix)]
    {
        let mut monitor = connect_monitor(&data_dir);
        let mut send_line = |v: Value| {
            let mut line = v.to_string();
            line.push('\n');
            monitor.get_mut().write_all(line.as_bytes()).unwrap();
            monitor.get_mut().flush().unwrap();
        };

        send_line(json!({ "t": "current", "p": "ITTestApp", "s": now_epoch() }));
        match expect_event(&rx, "current 事件") {
            Event::Current(ev) => {
                assert_eq!(ev["type"], json!("current"));
                assert_eq!(ev["app"]["process"], json!("ITTestApp"));
            }
            other => panic!("current 事件异常: {:?}", other),
        }
        // reminder 已是侧车，睡眠语义从通用端点断言
        let current = http_json("GET", port, "/api/apptimer/current", None);
        assert_eq!(current["app"]["process"], json!("ITTestApp"));
        for field in ["process", "sessionId", "since", "today", "todaySeconds"] {
            assert!(current["app"].get(field).is_some(), "current.app 缺少字段 {}", field);
        }
        assert_eq!(current["sleep"], json!(false));
        assert!(
            current["continuousSince"].as_str().is_some(),
            "current 应带 continuousSince（连续使用段起点，插件据此算连续时长）"
        );

        send_line(json!({ "t": "sleep" }));
        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["sleep"] == json!(true) {
                assert_eq!(
                    current["continuousSince"],
                    json!(null),
                    "睡眠中不保留连续使用段（唤醒会重开一段）"
                );
                break;
            }
            assert!(std::time::Instant::now() < deadline, "睡眠状态未生效");
            std::thread::sleep(Duration::from_millis(200));
        }

        send_line(json!({ "t": "wake" }));
        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["sleep"] == json!(false) {
                // 睡前快照已结算：唤醒后先清掉，等下一拍 current 再填，
                // 否则睡眠时长会被 includeOngoing 算成旧应用的进行中
                assert_eq!(
                    current["app"],
                    json!(null),
                    "唤醒后睡前快照应被丢弃"
                );
                assert!(
                    current["continuousSince"].as_str().is_some(),
                    "唤醒应开启新的连续使用段"
                );
                break;
            }
            assert!(std::time::Instant::now() < deadline, "唤醒状态未生效");
            std::thread::sleep(Duration::from_millis(200));
        }

        send_line(json!({ "t": "current", "p": "ITTestApp", "s": now_epoch() }));
        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["app"]["process"] == json!("ITTestApp") {
                break;
            }
            assert!(std::time::Instant::now() < deadline, "唤醒后心跳未恢复前台应用");
            std::thread::sleep(Duration::from_millis(200));
        }
    }

    // ---------- S7: server 重启后指令仍可达（seq 种子回归锁） ----------
    // 回归锁：seq 从 1 起数会撞旧 seen 被去重；启动毫秒做种子必须可达
    srv.stop();
    let srv = TestServer::start(&data_dir, port);
    wait_health(port);

    post_command(port, "ping", json!({ "step": "after-restart" }));
    expect_command(&rx, "after-restart");

    post_command(port, "ping", json!({ "step": "p1" }));
    let p2 = post_command(port, "ping", json!({ "step": "p2" }));
    let p2_seq = p2["seq"].as_u64().expect("p2 指令缺少 seq");
    expect_command(&rx, "p1");
    expect_command(&rx, "p2");

    // after 游标：从 p2 之后连接不应重放，从 p2 之前连接必须补投 p2
    let fresh = open_sse_after(port, PLUGIN_ID, p2_seq);
    expect_no_raw_command(&fresh, 500);
    let replay = open_sse_after(port, PLUGIN_ID, p2_seq - 1);
    expect_raw_command(&replay, "p2");

    let poll = http_json("GET", port, &format!("/api/plugins/{}/poll", PLUGIN_ID), None);
    let steps: Vec<&str> = poll["commands"]
        .as_array()
        .unwrap()
        .iter()
        .filter_map(|c| c["data"]["step"].as_str())
        .collect();
    assert!(
        steps.contains(&"p1") && steps.contains(&"p2"),
        "SSE 已投递的指令必须仍能被 poll 补投（每订阅者游标）: {steps:?}"
    );
    let poll = http_json("GET", port, &format!("/api/plugins/{}/poll", PLUGIN_ID), None);
    assert_eq!(
        poll["commands"].as_array().unwrap().len(),
        0,
        "poll 自己的游标推进后不再重复返回"
    );

    // ---------- S9: sub=webui 只收广播事件，不收该插件的指令 ----------
    let webui = open_sse(port, "webui");
    http_json(
        "POST",
        port,
        &format!("/api/plugins/{}/events", PLUGIN_ID),
        Some(&json!({ "state": "running", "data": { "n": 99 } })),
    );
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        match webui.recv_timeout(Duration::from_millis(200)) {
            Ok(payload) => {
                let v: Value = serde_json::from_str(&payload).expect("SSE 帧不是 JSON");
                if v["type"] == json!("plugin-state") && v["id"] == json!(PLUGIN_ID) {
                    assert_eq!(v["state"], json!("running"), "广播状态内容不符: {v}");
                    break;
                }
            }
            Err(RecvTimeoutError::Timeout) => {
                assert!(std::time::Instant::now() < deadline, "webui 订阅未收到 plugin-state 广播");
            }
            Err(RecvTimeoutError::Disconnected) => panic!("webui SSE 连接断开"),
        }
    }
    post_command(port, "ping", json!({ "step": "webui-should-not-see" }));
    // webui 只可见状态广播；只断言「没有 command 帧」
    let until = std::time::Instant::now() + Duration::from_millis(800);
    while std::time::Instant::now() < until {
        if let Ok(payload) = webui.recv_timeout(Duration::from_millis(200)) {
            let v: Value = serde_json::from_str(&payload).expect("SSE 帧不是 JSON");
            assert_ne!(
                v["type"],
                json!("command"),
                "webui 订阅不应收到 command（那是 sub=<plugin-id> 定向投递的）: {v}"
            );
        }
    }

    // ---------- S10: 通用「今日概览」端点（提醒插件的数据来源） ----------
    let summary = http_json("GET", port, "/api/data/today-summary", None);
    for field in ["date", "totalSeconds", "apps", "categories"] {
        assert!(summary.get(field).is_some(), "today-summary 缺少字段 {}", field);
    }
    assert_eq!(summary["totalSeconds"], json!(0), "空库今日总量应为 0");
    assert!(summary["apps"].as_array().unwrap().is_empty());
    assert!(summary["categories"].as_array().unwrap().is_empty());

    // ---------- S11: includeOngoing=1 把未结算的进行中会话并入今日 ----------
    #[cfg(unix)]
    {
        let mut monitor = connect_monitor(&data_dir);
        let pool = &srv.handle.as_ref().unwrap().pool;
        srv.rt.block_on(async {
            sqlx::query("INSERT INTO CategoryModels (Name) VALUES ('IT分类')")
                .execute(pool)
                .await
                .unwrap();
            sqlx::query(
                "INSERT INTO AppModels (Name, CategoryID) \
                 VALUES ('OngoingApp', (SELECT ID FROM CategoryModels WHERE Name = 'IT分类'))",
            )
            .execute(pool)
            .await
            .unwrap();
        });
        let since = now_epoch() - 600;
        let mut line = json!({ "t": "current", "p": "OngoingApp", "s": since }).to_string();
        line.push('\n');
        monitor.get_mut().write_all(line.as_bytes()).unwrap();
        monitor.get_mut().flush().unwrap();

        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["app"]["process"] == json!("OngoingApp") {
                break;
            }
            assert!(std::time::Instant::now() < deadline, "current 心跳未生效");
            std::thread::sleep(Duration::from_millis(200));
        }

        let now = now_epoch();
        let day_start = now - (now + 8 * 3600).rem_euclid(86_400);
        let expected = now - since.max(day_start);
        let live = http_json("GET", port, "/api/data/today-summary?includeOngoing=1", None);
        let total = live["totalSeconds"].as_i64().unwrap();
        assert!(
            (total - expected).abs() <= 15,
            "includeOngoing 应含进行中会话，预期约 {} 秒，实际 {}",
            expected,
            total
        );
        assert_eq!(live["apps"][0]["name"], json!("OngoingApp"));
        assert_eq!(live["apps"][0]["seconds"], json!(total));
        let cat = live["categories"]
            .as_array()
            .expect("categories 数组")
            .iter()
            .find(|r| r["name"] == json!("IT分类"))
            .expect("进行中会话应计入对应分类");
        assert_eq!(cat["seconds"], json!(total), "分类口径应与总量一致");

        let plain = http_json("GET", port, "/api/data/today-summary", None);
        assert_eq!(plain["totalSeconds"], json!(0), "默认参数不应含进行中会话");
    }

    // ---------- S12: 心跳静默 = 空闲/睡眠（看门狗兜底 + 连续段重开） ----------
    #[cfg(unix)]
    {
        let rfc = |epoch: i64| {
            chrono::DateTime::from_timestamp(epoch, 0)
                .unwrap()
                .to_rfc3339_opts(chrono::SecondsFormat::Secs, true)
        };

        // 基线：正在进行中的会话有连续段
        let before = http_json("GET", port, "/api/apptimer/current", None);
        assert_eq!(before["sleep"], json!(false), "基线应是清醒状态");
        let old_since = before["continuousSince"]
            .as_str()
            .expect("基线应有连续段")
            .to_string();

        // 订阅广播：静默升级为空闲后，看门狗应推一条 sleep=true 的 current 信封
        let webui = open_sse(port, "webui");

        // 模拟 monitor 静默：把最近心跳拨到阈值之前（等价 monitor 崩溃 / 空闲事件丢失 / 系统睡眠）
        let plugins = srv.handle.as_ref().unwrap().plugins.clone();
        plugins
            .last_current_at
            .store(now_epoch() - 3600, Ordering::Relaxed);

        // 读路径立即按空闲口径回答，不等广播
        let current = http_json("GET", port, "/api/apptimer/current", None);
        assert_eq!(current["sleep"], json!(true), "心跳静默应视为空闲/睡眠");
        assert_eq!(
            current["continuousSince"],
            json!(null),
            "空闲期不保留连续使用段"
        );

        // 进行中会话必须剔除，避免「已结算 + ongoing」的重复计量
        let live = http_json("GET", port, "/api/data/today-summary?includeOngoing=1", None);
        assert_eq!(
            live["totalSeconds"],
            json!(0),
            "静默后 ongoing 不应再计入今日: {live}"
        );

        // 看门狗补广播（≤2s 节拍）：插件据此冻结
        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            match webui.recv_timeout(Duration::from_millis(200)) {
                Ok(payload) => {
                    let v: Value = serde_json::from_str(&payload).expect("SSE 帧不是 JSON");
                    if v["type"] == json!("current") && v["sleep"] == json!(true) {
                        assert_eq!(v["continuousSince"], json!(null));
                        break;
                    }
                }
                Err(RecvTimeoutError::Timeout) => {
                    assert!(
                        std::time::Instant::now() < deadline,
                        "看门狗未广播 sleep=true"
                    );
                }
                Err(RecvTimeoutError::Disconnected) => panic!("webui SSE 连接断开"),
            }
        }

        // 心跳恢复 = 用户回来：退出空闲，连续段从恢复时刻重开
        let mut monitor = connect_monitor(&data_dir);
        let mut send_line = |v: Value| {
            let mut line = v.to_string();
            line.push('\n');
            monitor.get_mut().write_all(line.as_bytes()).unwrap();
            monitor.get_mut().flush().unwrap();
        };

        let resumed_at = now_epoch();
        send_line(json!({ "t": "current", "p": "OngoingApp", "s": resumed_at }));
        let deadline = std::time::Instant::now() + TIMEOUT;
        let resumed = loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["sleep"] == json!(false) {
                break current;
            }
            assert!(std::time::Instant::now() < deadline, "恢复心跳后应退出空闲");
            std::thread::sleep(Duration::from_millis(200));
        };
        let new_since = resumed["continuousSince"]
            .as_str()
            .expect("恢复后应开新连续段")
            .to_string();
        assert_ne!(new_since, old_since, "连续段必须从恢复时刻重开，不得沿用旧起点");
        assert!(
            new_since.as_str() >= rfc(resumed_at - 2).as_str(),
            "新段起点应接近恢复时刻: {new_since}"
        );

        // 常规节拍（应用切换）不重置连续段
        send_line(json!({ "t": "current", "p": "OngoingApp2", "s": now_epoch() }));
        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["app"]["process"] == json!("OngoingApp2") {
                assert_eq!(
                    current["continuousSince"].as_str(),
                    Some(new_since.as_str()),
                    "常规心跳/应用切换不得重置连续段"
                );
                break;
            }
            assert!(std::time::Instant::now() < deadline, "第二拍心跳未生效");
            std::thread::sleep(Duration::from_millis(200));
        }
    }

    // ---------- S13: 跨零点的已结算会话按「与今天重叠」计入 ----------
    #[cfg(unix)]
    {
        let now = now_epoch();
        // 北京时间今天 0 点（与 server 的 Asia/Shanghai 口径一致）
        let day_start = now - (now + 8 * 3600).rem_euclid(86_400);
        // 昨天 23:40 开始、2400s：今天占 20 分钟，昨天占 20 分钟
        let started = day_start - 20 * 60;
        let mut monitor = connect_monitor(&data_dir);
        let mut line = json!({ "t": "app", "p": "MidnightApp", "d": 2400, "a": started })
            .to_string();
        line.push('\n');
        monitor.get_mut().write_all(line.as_bytes()).unwrap();
        monitor.get_mut().flush().unwrap();

        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let summary = http_json("GET", port, "/api/data/today-summary", None);
            let row = summary["apps"]
                .as_array()
                .and_then(|rows| rows.iter().find(|r| r["name"] == json!("MidnightApp")));
            if let Some(row) = row {
                let seconds = row["seconds"].as_i64().unwrap();
                assert!(
                    (seconds - 1200).abs() <= 5,
                    "跨零点会话今日只应计 1200s，实际 {seconds}"
                );
                let total = summary["totalSeconds"].as_i64().unwrap();
                assert!(
                    (total - 1200).abs() <= 5,
                    "总量同样只计今天部分，实际 {total}"
                );
                break;
            }
            assert!(std::time::Instant::now() < deadline, "跨零点会话未写入");
            std::thread::sleep(Duration::from_millis(200));
        }

        // app.todaySeconds 与 today-summary 同口径
        let mut monitor = connect_monitor(&data_dir);
        let mut line =
            json!({ "t": "current", "p": "MidnightApp", "s": now_epoch() - 10 }).to_string();
        line.push('\n');
        monitor.get_mut().write_all(line.as_bytes()).unwrap();
        monitor.get_mut().flush().unwrap();
        let deadline = std::time::Instant::now() + TIMEOUT;
        loop {
            let current = http_json("GET", port, "/api/apptimer/current", None);
            if current["app"]["process"] == json!("MidnightApp") {
                let secs = current["app"]["todaySeconds"].as_i64().unwrap();
                assert!(
                    (secs - 1200).abs() <= 5,
                    "app.todaySeconds 与 today-summary 口径不一致: {secs}"
                );
                break;
            }
            assert!(std::time::Instant::now() < deadline, "current 心跳未生效");
            std::thread::sleep(Duration::from_millis(200));
        }
    }

    {
        let now = now_epoch();
        let day_start = now - (now + 8 * 3600).rem_euclid(86_400);
        let start_key = chrono::DateTime::from_timestamp(day_start, 0)
            .unwrap()
            .to_rfc3339_opts(chrono::SecondsFormat::Secs, true);
        for duration in [60, 120, 60] {
            http_json(
                "POST",
                port,
                "/api/apptimer/duration",
                Some(&json!({
                    "processName": "LegacyApp",
                    "duration": duration,
                    "startDateTime": start_key,
                })),
            );
        }
        let summary = http_json("GET", port, "/api/data/today-summary", None);
        let row = summary["apps"]
            .as_array()
            .and_then(|rows| rows.iter().find(|r| r["name"] == json!("LegacyApp")))
            .expect("无 sid 的会话应写入今日统计");
        assert_eq!(
            row["seconds"],
            json!(180),
            "同进程同秒的 60s/120s 两段会话应分开，重复上报仍去重: {summary}"
        );

        // 未来时间戳应被拒绝
        let future = chrono::DateTime::from_timestamp(now + 3600, 0)
            .unwrap()
            .to_rfc3339_opts(chrono::SecondsFormat::Secs, true);
        http_json(
            "POST",
            port,
            "/api/apptimer/duration",
            Some(&json!({
                "processName": "LegacyApp",
                "duration": 60,
                "startDateTime": future,
            })),
        );
        let summary = http_json("GET", port, "/api/data/today-summary", None);
        let seconds = summary["apps"]
            .as_array()
            .and_then(|rows| rows.iter().find(|r| r["name"] == json!("LegacyApp")))
            .map(|r| r["seconds"].as_i64().unwrap())
            .unwrap_or(0);
        assert_eq!(seconds, 180, "未来时间戳不应写入: {summary}");
    }

    // ---------- 禁用语义：指令/状态被拒，状态不再显示 ----------
    http_json(
        "POST",
        port,
        &format!("/api/plugins/{}/enabled", PLUGIN_ID),
        Some(&json!({ "enabled": false })),
    );
    let cmd = http_json_raw(
        "POST",
        port,
        &format!("/api/plugins/{}/commands", PLUGIN_ID),
        Some(&json!({ "cmd": "ping", "data": { "step": "disabled" } })),
    );
    assert_eq!(cmd["code"], json!(409), "禁用时指令应被拒绝: {cmd}");

    let poll = http_json_raw(
        "GET",
        port,
        &format!("/api/plugins/{}/poll", PLUGIN_ID),
        None,
    );
    assert_eq!(poll["code"], json!(409), "禁用时 poll 应被拒绝: {poll}");

    let state = http_json("GET", port, &format!("/api/plugins/{}/state", PLUGIN_ID), None);
    assert_eq!(state, json!(null), "禁用后不应再返回状态: {state}");

    let detail = http_json("GET", port, &format!("/api/plugins/{}", PLUGIN_ID), None);
    assert_eq!(detail["enabled"], json!(false));
    assert_eq!(detail["status"], json!(null), "禁用插件不显示运行状态: {detail}");

    // ---------- 清理 ----------
    srv.stop();
    let _ = std::fs::remove_dir_all(&data_dir);
}

fn now_epoch() -> i64 {
    std::time::SystemTime::now()
        .duration_since(std::time::UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0)
}

/// 轮询等状态 data.n 到达；指令处理是异步链路
fn wait_state_n(port: u16, n: i64) -> Value {
    let deadline = std::time::Instant::now() + TIMEOUT;
    loop {
        let s = http_json("GET", port, &format!("/api/plugins/{}/state", PLUGIN_ID), None);
        if s["data"]["n"] == json!(n) {
            return s;
        }
        assert!(std::time::Instant::now() < deadline, "状态未达到 n={}，当前: {}", n, s);
        std::thread::sleep(Duration::from_millis(200));
    }
}
