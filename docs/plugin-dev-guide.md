# Taix 插件开发指南（Plugin Dev Guide）
---

## 0. TL;DR

- 插件 = 一个目录（`plugin.json` + 一个可执行文件），放进安装目录 `plugins/<id>/` 即装、删除即卸
- 进程由 **taix-shell** 拉起并守护；开关与配置在 webui；**插件绝不主动退出**（禁用 = shell kill）
- 语言仅 Rust。依赖两个共享 crate：`taix-plugin-sdk`（必需：通信/配置/生命周期）、`taix-notify`（可选：系统通知）
- 数据来源 = server 的 HTTP + SSE（本地进程，与客户端同权限；`http://127.0.0.1:37091`）
- 最小骨架见 §3.1（与 `taix-plugins/demo` 一致）；完整参考实现见 §11，reminder 的脚本接口见 §12

## 1. 运行时架构与生命周期

| 进程 | 职责 | 与插件的关系 |
|---|---|---|
| `taix-shell` | 托盘 + 看门狗：扫描 `plugins/`、按 registry 启停、崩溃重启、随 shell 退出 | 插件进程的父级（唯一拉起者） |
| `taix-server` | HTTP `127.0.0.1:37091`（REST + `/ui/*` + SSE `/api/events`）；从 monitor 收心跳并聚合 | 插件的数据源与配置源 |
| `taix-monitor-*` | 5s 上报当前前台应用 / 睡眠状态（经本地 socket / 命名管道） | 间接：current 数据的产生方 |
| 插件（侧车） | 独立进程，业务逻辑 | —— |

数据流：`monitor → (socket) → server 聚合 → (SSE) → 插件`；插件读历史统计走普通 REST。

关键生命周期语义（实现：`taix-shell/src/plugin_supervisor.rs`）：

- shell 每 **5s** 重读 `registry.json` 的 `enabled`；变化 → 拉起 / kill（热生效，无重启）
- `plugin.json` 的 mtime 变化 → kill + 重启（**热升级**：替换二进制后 touch 一下清单即生效）
- 崩溃重启退避**线性 5s→30s（封顶）**；可执行文件缺失按同一退避重试，连续 6 次找不到即放弃监督
- **禁用只有一条路径：shell kill**。插件自杀会被 supervise 无限重启（红线 §9）

## 2. 插件目录与 plugin.json

### 2.1 目录布局

```
<安装目录>/plugins/<id>/           # macOS: /Applications/TaixTools/plugins/<id>/
  plugin.json                      # 清单（下节）
  taix-plugin-<id>(.exe)           # 可执行文件，名字 = 包名（Cargo package.name）
  ui/config.js                     # 可选：插件自带的配置 UI 入口模块（§2.3）

<数据目录>/plugins/                 # macOS: ~/Library/Application Support/Taix ；Windows: <安装目录>/Data
  registry.json                    # 启用状态 + 配置（**仅 server 写**，原子替换；shell 只读 enabled）
  <id>/state.json                  # 插件自管状态（可选，SDK 的 StateStore）
```

### 2.2 plugin.json 全字段

```jsonc
{
  "id": "my-plugin",               // 必填 ^[a-z0-9-]{1,32}$，不得为保留字 "webui"（撞 SSE 订阅标识）
  "name": "我的插件",               // 必填，非空
  "version": "0.1.0",              // 必填，三段纯数字 x.y.z
  "entry": "taix-plugin-my-plugin",     // 必填，目录内文件名；禁止 "/" "\" "." ".."
  "platforms": [],                 // 缺省 [] = 全平台；取值仅 "windows" | "macos"
  "display": false,                // 显示型标记：普通侧车插件为 false
  "configSchema": [                // 缺省 [] = 无配置页；给 webui 的**渲染提示，不是校验契约**
    { "key": "workMinutes", "label": "专注时长(分)", "type": "number", "min": 1, "max": 120, "default": 25 }
  ],
  "configUi": "config.js",         // 可选：插件自带的配置 UI 模块（ui/ 下的单文件名，不含 : ? # % 与空白，§2.3）
  "permissions": []                // 预留字段：不生效；存在时仅做结构校验（非空、≤32 字符）
}
```

`configSchema.type` ∈ `string | text | number | bool | select | json`；`select` 带 `options: string[]`，
`number` 可带 `min` / `max`（透传为输入框属性，同为渲染提示）；
`json` = 任意 JSON 由插件自己解释。**server 不按 schema 校验取值**，插件必须自己容错（serde default + 钳制）。
有 `configUi` 时配置页渲染的是插件自己的界面（见 §2.3），`configSchema` 只用于播种 `default`；模块加载失败会回退到通用表单。

校验与过滤行为（唯一实现：`taix-plugin-manifest`，server 与 shell 共用）：

- 校验失败（id / 名称 / 版本 / entry / 平台值 / 字段类型 / configUi 不合法）→ 该清单被跳过并告警，不影响其它插件
- `platforms` 不含当前系统 → **静默跳过（info 级）**，不是错误。「插件怎么不见了」先查这条
- 同 id 出现两次 → 保留先扫描到的，后者告警丢弃
- `entry` 解析（`resolve_entry`）：先按字面找；仅 Windows 再补一次 `.<exe>`。macOS 产物无扩展名，必须逐字写对

### 2.3 插件自带配置界面（configUi）

`configSchema` 的 6 种字段表达不了富交互（规则列表、代码编辑器、试跑面板）。声明 `configUi` 后，
配置页整体交给插件自己的界面渲染，webui 不含任何插件专属代码。

**契约（webui 侧实现：`taix-webui/src/pages/PluginConfig.svelte`）**

- 入口是 `ui/` 下的一个 ES 模块（`configUi` 是单文件名），由 server 的
  `GET /api/plugins/<id>/ui/*` 同源提供（只读 `<插件目录>/ui/`，拒绝 `..`／绝对路径／符号链接逃逸）
- 模块必须导出 `mount(el, api)`，返回可选的卸载函数：

```js
export function mount(el, api) {
  // 画界面到 el；编辑一律经 api.patch 回写宿主草稿
  return () => {/* 卸载：换页、重置重挂载、插件切换时调用 */}
}
```

- **宿主 API**（`api`）：
  | 成员 | 语义 |
  |---|---|
  | `id` / `version` / `schema` | 插件 id、版本、manifest 的 configSchema |
  | `values` | **挂载时**的配置快照（普通对象，非响应式） |
  | `patch(partial)` | 合并改动进宿主草稿 → 脏检测、保存栏、⌘S、离开守卫自动生效 |
  | `theme` / `onTheme(cb)` | `{ choice, resolved }`；切换主题与系统主题变化时回调 |
  | `reportError(msg \| null)` | 非致命错误，显示在宿主错误条（不触发回退） |
  | `getJson(path)` / `postJson(path, body)` | 同源 REST 薄封装，自动解 `{code,message,data}`，失败抛 `Error` |

- **职责划分**：保存栏、脏检测、重置、错误条由宿主负责；插件只管内容区。
  「放弃」（重置）会**重新挂载**模块并给到新的 `values` 快照——模块不要依赖宿主对快照的原地更新。
- **回退**：模块 import 失败或 `mount` 抛错 → 宿主显示错误条并回退 `configSchema` 通用表单（所以 schema 仍建议认真填）。
- **样式**：模块与宿主同页（非 iframe），可以直接使用全局设计令牌（`--card` `--text-2` `--accent` …）
  与全局类（`.card` `.switch` `.seg` `.notice` `.faint` `.spacer` `.hl-*` 等），暗色主题自动生效；
  自己的样式用 Svelte 作用域（或自行加前缀）避免外泄。`{@html}` 注入的高亮必须用全局类——组件作用域会被剔除。
- **信任模型**：模块与页面同 realm（可访问 `window`），与插件二进制同级信任；不接受不可信来源的插件（红线 §9.4）。
- **构建**：`taix-plugins/reminder/ui/` 是现成模板——独立 npm 工程（Svelte 5 + Vite，lib 模式单文件产出，
  `css: 'injected'` 免去单独 CSS 文件），`npm ci && npm run build` 产出 `ui/dist/config.js`，随插件目录一起分发。
  bundle 自带 Svelte 运行时，不要假设宿主暴露任何框架对象。
- **单文件建议**：入口模块与其相对 `import()` 的 chunk 都在 `ui/` 下即可被同源加载；资源上限 4 MiB/文件。
- **更新界面后要刷新页面**：ES 模块进浏览器的 module map 后不会再拉取，no-store 也绕不过；改完 `config.js` 刷新一次即可见新界面

**reminder 的实例**（参考实现）：`configUi: "config.js"`，界面包含可视化规则卡片、Lua 编辑器（行号 + 高亮）、
试运行面板；试运行 = `postJson` 发 `dry-run` 命令 + 轮询 `records?key=dryRun` 按 nonce 认领应答（§3.5／§4.1）。

## 3. taix-plugin-sdk

### 3.1 最小骨架（与 `taix-plugins/demo` 基本一致，可直接抄）

```rust
use serde::Deserialize;
use serde_json::json;
use taix_plugin_sdk::PluginApp;

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
struct DemoConfig {
    #[serde(default = "default_work_minutes")]
    work_minutes: i64,
}
fn default_work_minutes() -> i64 { 25 }

fn main() {
    PluginApp::new("demo")
        .on_config_changed(|ctx, config: &DemoConfig| {
            ctx.record("config/workMinutes", &json!(config.work_minutes));
        })
        .on_command("start", |ctx, data| { ctx.push_state("running", data); })
        .on_command("pause", |ctx, _|    { ctx.push_state("paused", None); })
        .on_command("reset", |ctx, _|    { ctx.push_state("idle", None); })
        .run()
        .unwrap();
}
```

### 3.2 PluginApp（builder）

| 方法 | 签名要点 | 语义 |
|---|---|---|
| `new(id)` | `-> Self` | id 决定 SSE 订阅与路由，**必须与 plugin.json 一致** |
| `on_command(cmd, f)` | `f: Fn(&PluginCtx, Option<Value>) + Send + Sync + 'static` | 指令分发（SSE 定向 + poll 降级共用）；`data` 是指令附带的自由 JSON |
| `on_config_changed::<T, _>(f)` | `T: DeserializeOwned`，`f: Fn(&PluginCtx, &T)` | 首次加载成功即触发一次，此后仅 version 变化时触发；反序列化失败记 warn 并跳过 |
| `on_current(f)` | `f: Fn(&PluginCtx, &Value)` | 订阅全局 current 广播（应用切换 / 睡眠唤醒 / 心跳） |
| `on_tick(interval, f)` | `f: Fn(&PluginCtx)` | **专用线程**按间隔调用（首次立即触发）；回调里可做阻塞 IO（HTTP、通知） |
| `run()` | `-> anyhow::Result<()>` | 阻塞：日志 → 单实例锁 → 首次配置加载（1→30s 退避等 server 就绪）→ SSE 线程 + poll 降级线程（+ tick 线程）→ 挂起主线程，**永不返回** |
| `run_with_args(&[String])` | 同上，参数由调用方给定 | 集成测试/嵌入宿主用 |

### 3.3 回调线程模型（必须理解）

- `on_command` / `on_config_changed` / `on_current` 都在 **SSE 事件线程内同步执行**：必须快速返回，阻塞会拖慢指令消费。要慢活儿 → 起线程或用 `on_tick`
- `on_tick` 在专用线程，阻塞随意（tick 线程不服务 SSE/指令）
- SDK 启动时会**先等配置加载成功**（对 server 按 1→30s 退避重试），之后才起 SSE/tick 线程。也就是说：server 没起来时，插件的周期逻辑不会跑，但 `run()` 不报错、不退出

### 3.4 PluginCtx API

| 方法 | 返回 | 说明 |
|---|---|---|
| `id()` / `server_url()` | `&str` | 当前插件 id / server 地址 |
| `state_store()` | `&StateStore` | state.json 句柄（§3.6） |
| `config_raw()` / `config::<T>()` | `Option<Value>` / `Option<T>` | 配置缓存读取（`config` 反序列化失败返回 None） |
| `push_state(state, data)` | `bool` | 上报状态（webui 可见）；**同 state 字符串 5s 内不重发** |
| `push_state_now(state, data)` | `bool` | 跳过 5s 节流（state 名没变但内容必须立刻可见时用） |
| `record(key, &value)` | `bool` | 写 records KV（§6） |
| `get_json(path)` | `Result<Value>` | 读 server 既有 REST，返回 `data` 字段。例：`get_json("/api/data/today-summary?tz=Asia/Shanghai")` |
| `reply(key, &Reply)` | `bool` | 把一次问答的结果写进 records（§3.5） |

### 3.5 请求-响应约定（Request / Reply）

插件不监听端口，界面（webui）要「问插件一句话」只能：**下发指令 → 插件把答案写进 records → 界面按 nonce 认领**。

```jsonc
// 界面 → 插件（POST /api/plugins/:id/commands 的 data）
{"nonce":"abc123","script":"return nil"}
// 插件 → 界面（records 的 value）
{"nonce":"abc123","ok":true,"error":null,"data":{...}}
```

```rust
use taix_plugin_sdk::{Request, Reply};
.on_command("dry-run", |ctx, data| {
    let Some(req) = Request::parse(data.as_ref()) else { return };   // 无 nonce 不认
    let script = req.str_field("script").unwrap_or_default();
    match run(script) {
        Ok(v)  => ctx.reply("dryRun", &Reply::ok(req.nonce.clone(), v)),
        Err(e) => ctx.reply("dryRun", &Reply::err(req.nonce.clone(), e.to_string())),
    };
})
```

约定：`nonce` 是保留字段（`payload` 里会被摘掉）；应答里不塞时间戳（用 records 行的 `updatedAt`）；
webui 等待窗口 150ms 间隔 × 4s，超时按「插件未响应」处理。

### 3.6 StateStore（state.json）

```rust
ctx.state_store().available();          // 无 --data-dir 时为 false
ctx.state_store().load::<MyState>();    // 文件不存在 → None；损坏 → 备份为 .json.corrupt 并返回 None（读取容忍 BOM）
ctx.state_store().save(&state)?;        // 原子写（tmp + rename），自动建父目录
```

用途：跨重启要活下来的插件私有状态（上次位置、冷却截止、累计值）。**只在状态变化时写**，不要每 tick 写。

### 3.7 启动参数与环境变量

shell 以固定参数拉起插件：`run --plugin <id> --data-dir <path>`（`run` 可省，便于手动调试）。
注意 `--plugin` 的值只作占位：SDK 以 `PluginApp::new(id)` 为实际身份，两者必须一致。

| 来源 | 变量 | 缺省 |
|---|---|---|
| 环境 | `TAIX_SERVER` | `http://127.0.0.1:37091`（末尾 `/` 自动裁掉） |
| 环境 | `TAIX_DATA_DIR` | 无（`--data-dir` 优先；两者都无 = 状态持久化关闭，仅无状态插件可接受） |
| 环境 | `RUST_LOG` | `info`（覆盖日志级别） |

未知命令行参数报错退出（保持接口纪律）。

### 3.8 SDK 内建行为（不需要插件自己做）

- **SSE 优先**：订阅 `GET /api/events?sub=<id>`，Accept 头 `text/event-stream`；断线按 1→2→4…30s 退避重连；重连后主动对齐配置
- **poll 降级**：SSE 连续 5 分钟没有收到任何字节后叠加 `GET /api/plugins/:id/poll`（每 2s），SSE 恢复自动停
- **seq 去重**：进程内 256 窗口（防御性；投递已收敛到 server 单一路径，插件不需要理解重放/水位）
- **配置缓存**：version 变化才重拉，平时零流量
- **单实例**：Windows 命名互斥量 `Local\TaixPluginMutex-<id>`；macOS 无锁（靠 shell 侧的进程名去重）
- **日志**：`taix-logging` 文件日志，落 `<插件可执行文件目录>/Logs/taix-plugin-<id>.YYYY-MM-DD.log`；debug 构建同时打 stdout；panic 也写日志（SyncFile）

## 4. REST 契约

通用约定：

- 基址 `http://127.0.0.1:37091`（`TAIX_SERVER` 可覆盖）
- 响应统一 `{"code":0,"message":"success","data":...}`；**失败也是 HTTP 200**，必须查 `code`（SDK 已内建）
- 字段 camelCase；时间 RFC3339 UTC；「今日」边界硬编码 Asia/Shanghai（已知债务）

### 4.1 插件契约端点

```
GET  /api/plugins                       列表 → [{id,name,version,display,entry,platforms,enabled,state,configVersion}]
GET  /api/plugins/:id                   详情（含 configSchema / configUi / status / records）
GET  /api/plugins/:id/config            → {version, values}
PUT  /api/plugins/:id/config            body {values: object} → {configVersion}（version+1 并广播 config 事件）
POST /api/plugins/:id/enabled           body {enabled: bool}（shell ≤5s 生效）
POST /api/plugins/:id/commands          body {cmd(非空 ≤64), data?} → 入队回执 {seq, cmd, data?}
GET  /api/plugins/:id/poll              → {config, configVersion, commands:[{seq, cmd, data?}]}（取走式）
POST /api/plugins/:id/events            body {state(非空 ≤64), data?} 状态上报
GET  /api/plugins/:id/state             → {state, data, ts} | null
POST /api/plugins/:id/records           body {key(非空 ≤128), value}  upsert
GET  /api/plugins/:id/records?key=...   → [{key, value, updatedAt}]（缺省返回全部，按 key 排序）
GET  /api/plugins/:id/ui/<file>         → 插件自带的配置 UI 资源（<插件目录>/ui/ 下，只读）
                                          响应头 no-store + nosniff；路径穿越一律 400/404；上限 4 MiB/文件
```

`:id` 必须先在安装目录 `plugins/` 里存在（清单扫描目录 = **server 可执行文件旁的 `plugins/`**），
否则一律 404 `plugin 'x' not found`。清单缓存外，404 路径会**限量热重扫**（≤1 次/秒）——拷入目录即装，无需重启 server。

### 4.2 数据端点（插件可读的既有 REST，常用）

```
GET /api/apptimer/current       当前前台应用快照（monitor 心跳聚合）
    → { app: {process, title, iconPath, since, today, todaySeconds} | null,
        sleep: bool, continuousSince: RFC3339 | null }
    · title 目前恒为 null（占位）；today/todaySeconds = 该进程今日**已结束**会话之和（不含进行中的会话）
    · continuousSince = 连续使用段起点（睡眠中为 null）；"连续使用满 N 分钟"类插件直接读它
GET /api/data/today-summary?tz=Asia/Shanghai[&includeOngoing=1]
    → { date, totalSeconds, apps:[{name,seconds}], categories:[{name,seconds}] }
    · includeOngoing=1 把进行中的会话计入三个口径（跨 0 点钳制到当日 0 点），提醒类插件用；默认不含
```

其余统计/配置路由（`/api/data/*`、`/api/webdata/*`、`/api/appdata*`、`/api/config` 等）与客户端同权限、
可直接 `ctx.get_json()` 读取；完整路由清单以 `taix-server/src/routes.rs` 为准。

## 5. SSE 事件总线与指令投递

```
GET /api/events?sub=<plugin-id|webui>     # text/event-stream；keep-alive 注释帧 15s
```

四类信封（`data:` 一行 JSON）：

```jsonc
{"type":"command","id":"demo","seq":12,"cmd":"start","data":{}}    // 定向：只发给该插件
{"type":"config","id":"demo","version":3}                          // 定向：version 变化才重拉
{"type":"current","app":{...}|null,"sleep":false,"continuousSince":"..."}  // 广播：webui 与所有插件
{"type":"plugin-state","id":"demo","state":"running","data":{},"ts":"...Z"} // 广播
```

投递语义：

- 指令**不走广播**：每插件一个**有界队列（上限 16，溢出丢最旧）+ server 侧投递游标**；SSE 投递泵与 `poll` 共用
  同一条取走路径（`take_pending`），订阅建立时先按序补发未投递的指令（含断线期间攒下的）
- 「取走即视为已投递」（至多一次）；唯一边角是订阅竞态（先订阅广播、再取队列）在极端并发下可能重复一次——
  由 SDK 的 seq 窗口兜底，**插件不需要自己写去重逻辑**
- 全局 seq 用**server 启动时刻毫秒**做种子（防止重启后新 seq 撞进插件去重窗口被误弃）
- `sub=webui` 只收广播（current / plugin-state），收不到指令与 config
- 服务端广播通道容量 256；订阅者积压（lagged）时丢事件并告警（current 是尽力推送，丢一两拍不影响正确性）

## 6. 持久化三件套（决策表）

| 存哪 | 谁写 | 读法 | 适用 | 生命周期 |
|---|---|---|---|---|
| `registry.json` 的 configs | **仅 server** | 插件经 `GET config`（SDK 缓存） | 用户可改的配置（webui 表单） | 永久；version 递增 |
| `<id>/state.json` | 插件自己 | `ctx.state_store()` | 插件私有状态（位置、冷却、累计） | 永久（插件负责） |
| records（SQLite KV） | 插件 POST | `ctx.record()` / 界面 GET | 界面-插件问答交接、调试快照 | **每插件最多 512 条**（按 UpdatedAt 保留最新，超出删最旧），运行期记录，非永久 |

选择：能被用户改的 → config；只想自己知道的 → state.json；要给界面看的 → records。

## 7. 系统通知（taix-notify）

```rust
pub struct NotifyOptions {
    pub sound: Option<&'static str>,        // None = 静默；Windows 声音名 / macOS 系统声名（如 "default"）
    pub actions: Vec<(String, String)>,     // 交互按钮 (id, label)；macOS 忽略（平台限制）
    pub aumid: Option<String>,              // 仅 Windows 编译期字段；缺省 "Taix.Plugin"（与安装器注册一致）
}
pub fn notify(title: &str, body: &str, opts: &NotifyOptions) -> anyhow::Result<()>
```

- Windows：Toast（AUMID 必须与安装器注册值一致）；macOS：`osascript display notification`
  （无 bundle 的 CLI 进程用 notify-rust 不弹横幅——实测结论，别换回去）
- 多行/制表文本内部已做 AppleScript 转义（拆段拼 `linefeed`/`tab`），直接用 `\n` `\t` 即可
- 调用是**阻塞**的（等通知进程返回），建议挪到独立线程，别占 tick 线程；失败只记日志不 panic

## 8. 开发、联调与发布

### 8.1 构建

仓库**无 workspace**：每个 crate 独立、以 path 相互引用。`cd taix-plugins/<id> && cargo build --release`，
产物 `target/release/taix-plugin-<id>`（名字 = Cargo `package.name` = manifest `entry`）。
release profile 照抄现有插件：

```toml
[profile.release]
opt-level = "z"
lto = true
codegen-units = 1
panic = "abort"
strip = true
[profile.release.package."*"]
opt-level = "z"
```

声明了 `configUi` 的插件还要构建自带界面（独立 npm 工程，见 §2.3）：
`cd taix-plugins/<id>/ui && npm ci && npm run build` → `ui/dist/config.js`；同目录 `npm test` 跑界面单测。

### 8.2 隔离联调（推荐工作流，不影响正式版）

```bash
# 1) 数据目录副本 + 起一份自己的 server
cp -R "$HOME/Library/Application Support/Taix" /tmp/taix-dev-data     # macOS（可先用真实库，今日数据即真实）
cd taix-server && cargo build
TAIX_SERVER=http://127.0.0.1:37191 TAIX_INSTANCE_NAME=TaixDevServer \
TAIX_PIPE_PATH=/tmp/taix-dev-daemon.sock \
  ./target/debug/taix-server --data-dir /tmp/taix-dev-data

# 2) 插件放进 server 的清单扫描目录（server exe 旁的 plugins/）并手动运行
mkdir -p taix-server/target/debug/plugins/<id>
cp taix-plugins/<id>/plugin.json taix-plugins/<id>/target/release/taix-plugin-<id> taix-server/target/debug/plugins/<id>/
#    有 configUi 的话，界面产物也要在（否则配置页回退通用表单）：
if [ -f taix-plugins/<id>/ui/dist/config.js ]; then
  mkdir -p taix-server/target/debug/plugins/<id>/ui
  cp taix-plugins/<id>/ui/dist/config.js taix-server/target/debug/plugins/<id>/ui/
fi
cd taix-plugins/<id>
TAIX_SERVER=http://127.0.0.1:37191 ./target/release/taix-plugin-<id> run --plugin <id> --data-dir /tmp/taix-dev-data

# 3) 模拟 monitor 心跳（验证 current / SSE 链路）
python3 - <<'EOF'
import json, socket, time
s = socket.socket(socket.AF_UNIX, socket.SOCK_STREAM); s.connect("/tmp/taix-dev-daemon.sock")
s.sendall((json.dumps({"t":"current","p":"WorkBuddy","s":int(time.time())-600})+"\n").encode()); s.close()
EOF
# 其它消息：{"t":"sleep"} / {"t":"wake"}；会话结算 {"t":"app","p":"Chrome","d":120,"a":<开始epoch>}
```

隔离三件套：`TAIX_SERVER`（端口）、`TAIX_INSTANCE_NAME`（server 单实例锁）、`TAIX_PIPE_PATH`（monitor 管道）。
隔离环境里没有 shell 监督，插件由你手动起；`enabled` 开关只在正式环境驱动 shell。
调试用 `curl http://127.0.0.1:37191/api/plugins` 看清单与状态。

### 8.3 契约集成测试

`cd taix-plugin-it && cargo test`（仓库无 workspace，不能 `-p`）。它用**真实 server 应用 + 真实 SDK**锁死协议，
改传输层/契约前先跑。锁定的行为：指令队列溢出（丢最旧）与取走式 poll、预置指令重放恰好一次、实时下发（SSE）、
配置热重载、records 往返、状态上报节流、current 广播与睡眠唤醒（pipe）、server 重启后指令仍可达（seq 种子回归）、
指令不重复投递（SSE 与 poll 共用投递游标）、`sub=webui` 只收广播、today-summary 契约、`includeOngoing` 计入未结算会话。

### 8.4 新增随包插件：落位清单

随包分发需要动两处：

1. `taix-installer/build-dmg.sh`（macOS）：构建步骤 + 拷贝 `plugins/<id>/`（含 `ui/dist/config.js`）+ 代码签名循环（`plugins/*/taix-plugin-*` 已被通配覆盖；reminder 是现成落位）
2. `.github/workflows/release.yml`：对应平台 job 加 `Build taix-plugin-<id>` step；`Swatinem/rust-cache` 的 workspaces 列表加插件目录；
   Windows 打包段校验插件产物存在——缺产物必须让构建**失败**（防止打出空插件列表的包）。
   插件带 `configUi` 时，同样要在打包段构建并校验 `ui/dist/config.js`

## 9. 红线与预算

1. **插件绝不能自杀**：禁用/更新只有 shell kill 一条路；自杀 → supervise 无限重启
2. stdout/stderr 被 shell 丢弃：日志一律 `tracing`（SDK 已接入 taix-logging，落 `<插件目录>/Logs/`）
3. release `panic = "abort"`：任何 panic = 进程秒死（shell 会重启，但用户看到的是反复闪退）——回调里别裸 `unwrap`
4. **本地信任模型**：插件 = 本地全权进程（可读写全部 REST、可读数据目录）；无沙箱无签名，只装可信来源的插件
5. 无 workspace：不要加根 workspace；每 crate 自带 Cargo.lock
6. 资源预算（2026-10 实测）：SDK 地板 ≈6MB RSS / 0.69MB 二进制；+Lua（reminder）≈7MB / 1.03MB
7. 「今日」边界硬编码 Asia/Shanghai（已知债务）；macOS 通知无动作按钮（平台限制）
8. 浏览器扩展通道 WS `:8908` 绑 0.0.0.0 且无鉴权（与插件系统无关的历史债务，勿照抄进新组件）

## 10. 常见坑（FAQ）

| 症状 | 先查 |
|---|---|
| 插件放进目录但列表没有 | `platforms` 是否含当前系统；manifest 校验失败（server 日志 warn）；同 id 重名 |
| 列表有但没进程 | registry 未启用；`entry` 文件名与产物不符（macOS 无 `.exe`；Windows 可省 `.exe`） |
| 改了 plugin.json | shell ≤5s 热升级（kill + 重启新版本）；只换二进制不改清单不触发重载 |
| webui 点指令没反应 | 插件进程是否活着；队列上限 16（溢出丢最旧）；SSE 断线时指令在队列等重连（重启后仍可达） |
| 状态不刷新 | `push_state` 同 state 5s 节流；要立刻可见用 `push_state_now` |
| 指令收到但订阅/数据对不上 | `PluginApp::new(id)` 与清单 id / `--plugin` 必须一致（订阅按 id 过滤） |
| 端口 37091 被正式版占用 | 隔离联调用 §8.2 的三件套换端口/锁/管道 |
| `todaySeconds` 恒为 0 | DB 里没有该进程今日**已完成**的会话（进行中会话不入库；用 `includeOngoing` 或本地叠加） |
| 正式版没起来 | 本机还有同名调试进程：`pgrep -x taix-server` 后 kill，shell 1~2s 自动拉起 |

## 11. 参考实现索引

| 插件/模块 | 形态 | 演示了什么 |
|---|---|---|
| `taix-plugins/demo` | headless 极简 | SDK 全链路最小用法（配置/指令/状态/records） |
| `taix-plugins/reminder` | headless 完整 | tick 拉数据、Lua 沙箱规则、命中列表按规则冷却、命令+records 问答、通知、错误态上报（脚本接口见 §12） |
| `taix-plugins/reminder/ui` | 自带配置界面 | `configUi` 契约的参考实现：mount/patch、宿主 API 封装（`bridge.ts`）、Svelte 单文件构建（§2.3） |
| `taix-plugin-it/tests/transport.rs` | 契约测试 | 协议行为的可执行形式（改传输层先跑它） |

核心源码（按需深读）：`taix-plugin-sdk/{lib,ctx,sse,transport,state,options,request}.rs`、
`taix-notify/src/lib.rs`、`taix-plugin-manifest/src/lib.rs`（清单校验唯一实现）、
`taix-server/src/{routes/plugins.rs, routes/events.rs, plugins/*.rs}`、`taix-shell/src/plugin_supervisor.rs`、
`taix-webui/src/pages/PluginConfig.svelte`（configUi 挂载与宿主 API 的宿主侧实现）。

## 12. 参考：reminder 插件的 Lua 脚本接口

`taix-plugins/reminder`（使用时长提醒）把 Lua 用户脚本当规则引擎——「tick 拉数据 + 沙箱求值」形态的完整参考。
每 5 秒一拍：拉 `GET /api/data/today-summary?tz=Asia/Shanghai&includeOngoing=1`（含进行中的会话）+
读 `current` 事件的 `continuousSince` → 组快照喂给脚本；睡眠（空闲段）期间冻结：不评估、不提醒。

**脚本契约**：返回本轮**命中的候选列表**（也兼容返回单个表），每条含 `id / title / body / cooldownMinutes`：

- `title` 必填（缺了报错）；`body` 可省；`id` 可省——缺省按 **0 起的序号**回退（单表 = 固定 id，等价旧「全局冷却」语义）
- `cooldownMinutes` 缺省 60、**钳制到 1–1440**（防 0 冷却导致的通知风暴）
- `oncePerDay`（可选布尔）：当天只提醒一次，按本地日期记账、跨天自动重置；冷却仍然生效
- 上限：单轮最多 32 条候选；标题截 120 字符、正文截 400 字符
- 返回 `nil` 或空表 = 本轮不提醒；返回类型不对 / 缺 title = 报错（界面上可见）

**提醒策略（插件侧）**：按顺序取**第一条未被拦住**的弹系统通知；冷却**按 id 各自记账**；
同轮其余命中让位 5 分钟（不缩短已有的更长冷却，已「当天提醒过」的规则不再让位）。
每拍都清理过期冷却，避免规则删除后残留的 id 压制同名新规则。
闸门（冷却截止 + 当天已提醒）存 `state.json`：插件重启、自动更新后不会把刚提醒过的规则再弹一遍。

**试运行**：webui 下发指令 `dry-run`（`data: {script, nonce}`）→ 插件执行并把结果写进 records `dryRun`
（信封 `{nonce, ok, error, data:{candidates, pickedIndex, snapshot}}`，`candidates[i]` 含 `cooldownLeftSeconds` / `oncePerDay` / `firedToday`，`pickedIndex` 指向当前会提醒的那条、
`null` = 命中的都在冷却或当天已提醒过；**不消耗冷却**）→ webui 按 nonce 认领（§3.5）。

**沙箱**：mlua（Lua 5.4 vendored），标准库白名单 `coroutine / table / math / string / utf8`
（**不含 io / os / package / debug**）；1MB 内存上限 + 指令预算（约 2 亿条指令，防死循环）。

`ctx` 字段（只读）：

| 字段 | 类型 | 说明 |
|---|---|---|
| `continuousSeconds` / `continuousMinutes` | number | 连续使用时长（来自 server 的 `current.continuousSince`，睡眠中为 0） |
| `todayTotalSeconds` / `todayTotalMinutes` | number | 今日全部应用累计（含进行中会话） |
| `apps` | table | 今日按进程秒数表（键 = 进程名，降序前 200；含进行中会话） |
| `categories` | table | 今日按分类秒数表（按 `AppModels.Name = ProcessName` 联表近似；含进行中会话） |
| `currentProcess` | string | 当前前台进程名 |
| `hour` / `weekday` | number | 本地（Asia/Shanghai）小时；1 = 周一 … 7 = 周日 |
| `sleeping` | boolean | 是否处于空闲 / 睡眠段 |

辅助函数：`todayAppMinutes(pattern)`（进程名子串匹配、大小写不敏感，返回累计分钟）、
`todayCategoryMinutes(name)`、`fmtHMS(secs)`。

**内置默认脚本**：三条示例规则——连续使用满 60 分钟（r1）、「娱乐」分类累计满 120 分钟（r2）、
22 点后含 "game" 的应用累计满 30 分钟（r3）。配置里保存的新脚本在下一拍先试跑：通过即生效；
编译/运行失败则**沿用上一份可用脚本**并把错误上报为 error 状态（webui 配置页可见），恢复后自动翻回 running。

**可视化规则**（webui `src/lib/rules.ts`）：模式 = `可视化规则` 时前端把规则编译成上面的 Lua 存进
`values.script`，server 与插件只认 `script`。规则字段：`type`（连续 / 每日总时长 / 按分类 / 按应用）、
`minutes` 阈值、`match`（分类名或进程名子串，**逗号分隔多个、时长求和**）、`window` 时段、
`cooldown` 间隔、`repeat`（`repeat` 重复提醒 / `once` 每天一次 → 编译出 `oncePerDay`）。
未选分类/应用的规则编译成 `if false then`，不会因缺数据误报；用户文本进 Lua 前统一转义。
改过脚本后再保存可视化规则会覆盖脚本，界面会提示。
