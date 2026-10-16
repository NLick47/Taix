use mlua::{Lua, LuaOptions, StdLib};
use serde::Serialize;
use std::cell::{Cell, RefCell};
use std::rc::Rc;

const LUA_MEMORY_LIMIT: usize = 1_000_000;
const LUA_INSTRUCTION_BATCH: u32 = 50_000;
const LUA_INSTRUCTION_BUDGET: u32 = 4_000; // ≈2 亿条指令
const MAX_CANDIDATES: usize = 32;
const TITLE_MAX_CHARS: usize = 120;
const BODY_MAX_CHARS: usize = 400;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Snapshot {
    /// 睡眠中为 0
    pub continuous_seconds: i64,
    pub sleeping: bool,
    pub today_total_seconds: i64,
    /// 进程名 → 今日秒数
    pub apps: Vec<(String, i64)>,
    /// 分类名 → 今日秒数
    pub categories: Vec<(String, i64)>,
    pub current_process: String,
    pub hour: i64,
    /// 1=周一 … 7=周日
    pub weekday: i64,
}

const PRELUDE: &str = r#"
function todayAppMinutes(pattern)
  local total = 0
  for name, secs in pairs(ctx.apps) do
    if string.find(string.lower(name), string.lower(tostring(pattern)), 1, true) then
      total = total + secs
    end
  end
  return math.floor(total / 60)
end

function todayCategoryMinutes(name)
  local secs = ctx.categories[name]
  if secs then return math.floor(secs / 60) end
  return 0
end

function fmtDuration(secs)
  local t = math.max(0, math.floor(tonumber(secs) or 0))
  if t < 60 then return t .. " 秒" end
  local m = t // 60
  if m < 60 then return m .. " 分钟" end
  local h, rem = m // 60, m % 60
  if rem == 0 then return h .. " 小时" end
  return h .. " 小时 " .. rem .. " 分钟"
end

-- 分钟 → 同上；todayAppMinutes / continuousMinutes 等已是分钟
function fmtMinutes(mins)
  return fmtDuration(math.max(0, math.floor(tonumber(mins) or 0)) * 60)
end
"#;

#[cfg(test)]
pub const SAMPLE_SCRIPT: &str = r#"-- 使用时长提醒脚本（Lua）。每 5 秒执行一次：
-- 把本轮命中的规则按顺序放进 out 并返回；插件只提醒第一条不在冷却中的，
-- 其余命中让位 5 分钟。也可直接返回单个 {title=..., body=..., cooldownMinutes=30}，
-- 返回 nil 或空表不提醒。可用数据见「实时数据」面板；辅助函数：todayAppMinutes /
-- todayCategoryMinutes / fmtDuration(秒) / fmtMinutes(分钟)。修改保存后即时生效。

local out = {}

-- 1) 连续使用满 60 分钟，提醒休息（空闲后自动清零）
if ctx.continuousMinutes >= 60 then
  out[#out + 1] = {
    id = "r1",
    title = "该休息一下了",
    body = "已连续使用 " .. fmtMinutes(ctx.continuousMinutes),
    cooldownMinutes = 30,
  }
end

-- 2) 「娱乐」分类今天累计满 2 小时
if todayCategoryMinutes("娱乐") >= 120 then
  out[#out + 1] = {
    id = "r2",
    title = "娱乐时间到头啦",
    body = "今天娱乐分类已用 " .. fmtMinutes(todayCategoryMinutes("娱乐")),
    cooldownMinutes = 120,
  }
end

-- 3) 22 点后，含 "game" 的应用今天用了 30 分钟以上（进程名子串匹配）
if ctx.hour >= 22 and todayAppMinutes("game") >= 30 then
  out[#out + 1] = {
    id = "r3",
    title = "夜深了",
    body = "游戏今天已用 " .. fmtMinutes(todayAppMinutes("game")),
    cooldownMinutes = 240,
  }
end

return out
"#;

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RemindCandidate {
    pub id: String,
    pub title: String,
    pub body: String,
    pub cooldown_minutes: i64,
    pub once_per_day: bool,
}

// 显式白名单：ALL_SAFE 含 io，不能用于用户脚本
fn fresh_lua() -> Result<(Lua, Rc<Cell<u32>>), mlua::Error> {
    let libs = StdLib::COROUTINE | StdLib::TABLE | StdLib::MATH | StdLib::STRING | StdLib::UTF8;
    let lua = Lua::new_with(libs, LuaOptions::new())?;
    lua.set_memory_limit(LUA_MEMORY_LIMIT)?;
    let used = Rc::new(Cell::new(0u32));
    let hook_used = used.clone();
    lua.set_hook(
        mlua::HookTriggers::new().every_nth_instruction(LUA_INSTRUCTION_BATCH),
        move |_lua, _dbg| {
            hook_used.set(hook_used.get() + LUA_INSTRUCTION_BATCH);
            if hook_used.get() >= LUA_INSTRUCTION_BATCH * LUA_INSTRUCTION_BUDGET {
                Err(mlua::Error::RuntimeError(
                    "script exceeded instruction budget".to_string(),
                ))
            } else {
                Ok(mlua::VmState::Continue)
            }
        },
    );
    Ok((lua, used))
}

fn build_ctx_table(lua: &Lua, snap: &Snapshot) -> Result<mlua::Table, mlua::Error> {
    let ctx = lua.create_table()?;

    let apps = lua.create_table()?;
    for (name, secs) in &snap.apps {
        apps.set(name.as_str(), *secs)?;
    }
    ctx.set("apps", apps)?;

    let categories = lua.create_table()?;
    for (name, secs) in &snap.categories {
        categories.set(name.as_str(), *secs)?;
    }
    ctx.set("categories", categories)?;

    ctx.set("continuousSeconds", snap.continuous_seconds)?;
    ctx.set("continuousMinutes", snap.continuous_seconds / 60)?;
    ctx.set("todayTotalSeconds", snap.today_total_seconds)?;
    ctx.set("todayTotalMinutes", snap.today_total_seconds / 60)?;
    ctx.set("sleeping", snap.sleeping)?;
    ctx.set("currentProcess", snap.current_process.as_str())?;
    ctx.set("hour", snap.hour)?;
    ctx.set("weekday", snap.weekday)?;

    Ok(ctx)
}

thread_local! {
    /// 每线程一个 Lua 引擎缓存，与脚本文本绑定；脚本变化才重建。
    /// tick（5s 一拍）稳态下不再重建 VM / 重编译脚本
    static ENGINE: RefCell<Option<(String, Engine)>> = const { RefCell::new(None) };
    #[cfg(test)]
    static ENGINE_BUILDS: Cell<u32> = const { Cell::new(0) };
}

/// 编译产物 + 运行环境缓存（引用绑定的 Lua 状态）：
/// - 稳态（脚本不变）只做「清环境 + 建 ctx + 执行」；
/// - 脚本变更时整个重建，顺带清掉上一份脚本可能污染的基础库
struct Engine {
    lua: Lua,
    /// 已消耗指令数（钩子累加）；每轮运行前清零，长跑不累计误杀
    used: Rc<Cell<u32>>,
    /// 运行环境：脚本全局写在这里，每轮清空重用；`__index` 链到基础库
    env: mlua::Table,
    chunk: mlua::Function,
}

impl Engine {
    fn new(script: &str) -> Result<Self, String> {
        #[cfg(test)]
        ENGINE_BUILDS.with(|c| c.set(c.get() + 1));

        let (lua, used) = fresh_lua().map_err(|e| format!("Lua 初始化失败: {e}"))?;

        let base = lua.create_table().map_err(|e| format!("Lua 初始化失败: {e}"))?;
        for pair in lua.globals().pairs::<mlua::Value, mlua::Value>() {
            let (key, value) = pair.map_err(|e| format!("Lua 初始化失败: {e}"))?;
            if let mlua::Value::String(name) = &key {
                if name.to_string_lossy() == "_G" {
                    continue;
                }
            }
            base.set(key, value).map_err(|e| format!("Lua 初始化失败: {e}"))?;
        }
        let env = lua.create_table().map_err(|e| format!("Lua 初始化失败: {e}"))?;
        let meta = lua.create_table().map_err(|e| format!("Lua 初始化失败: {e}"))?;
        meta.set("__index", base).map_err(|e| format!("Lua 初始化失败: {e}"))?;
        env.set_metatable(Some(meta));

        let full = format!("{PRELUDE}\n{script}");
        let chunk = lua
            .load(&full)
            .set_name("reminder")
            .set_environment(env.clone())
            .into_function()
            .map_err(|e| format!("脚本编译失败: {e}"))?;

        Ok(Self { lua, used, env, chunk })
    }

    fn run(&mut self, snap: &Snapshot) -> Result<Vec<RemindCandidate>, String> {
        self.used.set(0);
        let _ = self.lua.gc_collect();

        clear_table(&self.env).map_err(|e| format!("构建上下文失败: {e}"))?;
        let ctx = build_ctx_table(&self.lua, snap).map_err(|e| format!("构建上下文失败: {e}"))?;
        self.env.set("ctx", ctx).map_err(|e| e.to_string())?;

        let result: mlua::Value = self
            .chunk
            .call(())
            .map_err(|e| format!("脚本运行失败: {e}"))?;
        match result {
            mlua::Value::Nil => Ok(Vec::new()),
            mlua::Value::Table(table) => collect_candidates(&table),
            _ => Err("脚本返回类型不支持，应为表或 nil".to_string()),
        }
    }
}

/// 清空表的所有键
fn clear_table(table: &mlua::Table) -> Result<(), mlua::Error> {
    let mut keys = Vec::new();
    for pair in table.pairs::<mlua::Value, mlua::Value>() {
        let (key, _) = pair?;
        keys.push(key);
    }
    for key in keys {
        table.raw_set(key, mlua::Value::Nil)?;
    }
    Ok(())
}

/// 返回全部命中，按脚本顺序；nil 或空表 = 不提醒
pub fn run_script(script: &str, snap: &Snapshot) -> Result<Vec<RemindCandidate>, String> {
    ENGINE.with(|cell| {
        let mut slot = cell.borrow_mut();
        let stale = match slot.as_ref() {
            Some((cached, _)) => cached != script,
            None => true,
        };
        if stale {
            let engine = Engine::new(script)?;
            *slot = Some((script.to_string(), engine));
        }
        slot.as_mut().expect("引擎刚写入").1.run(snap)
    })
}

#[cfg(test)]
fn engine_used_batches() -> u32 {
    ENGINE.with(|cell| cell.borrow().as_ref().map(|(_, e)| e.used.get()).unwrap_or(0))
}

#[cfg(test)]
fn engine_builds() -> u32 {
    ENGINE_BUILDS.with(|c| c.get())
}

fn collect_candidates(table: &mlua::Table) -> Result<Vec<RemindCandidate>, String> {
    if table.contains_key("title").unwrap_or(false) {
        return Ok(vec![candidate_from(table, 0)?]);
    }
    if table.raw_len() == 0 {
        // {} = 不提醒；有键但无 title 按缺 title 报错
        return match table.pairs::<mlua::Value, mlua::Value>().next() {
            None => Ok(Vec::new()),
            Some(Ok(_)) => Err("返回表缺少 title".to_string()),
            Some(Err(e)) => Err(e.to_string()),
        };
    }
    let mut out = Vec::new();
    let mut index = 1i64;
    while out.len() < MAX_CANDIDATES {
        let item: mlua::Value = table.get(index).map_err(|e| e.to_string())?;
        match item {
            mlua::Value::Nil => break,
            mlua::Value::Table(item) => out.push(candidate_from(&item, out.len())?),
            _ => return Err(format!("第 {index} 项不是表")),
        }
        index += 1;
    }
    Ok(out)
}

fn candidate_from(table: &mlua::Table, index: usize) -> Result<RemindCandidate, String> {
    let title: String = table.get("title").map_err(|_| "返回表缺少 title".to_string())?;
    let body: String = table.get("body").unwrap_or_default();
    let id: Option<String> = table.get("id").ok().flatten();
    let cooldown_minutes: i64 = table.get("cooldownMinutes").unwrap_or(60);
    let once_per_day: bool = table.get("oncePerDay").unwrap_or(false);
    Ok(RemindCandidate {
        id: id
            .filter(|s| !s.trim().is_empty())
            .unwrap_or_else(|| index.to_string()),
        title: clamp_chars(&title, TITLE_MAX_CHARS),
        body: clamp_chars(&body, BODY_MAX_CHARS),
        // 下限 1 分钟防通知风暴
        cooldown_minutes: cooldown_minutes.clamp(1, 24 * 60),
        once_per_day,
    })
}

fn clamp_chars(text: &str, max: usize) -> String {
    if text.chars().count() <= max {
        return text.to_string();
    }
    text.chars().take(max).collect()
}

#[cfg(test)]
mod tests {
    use super::*;

    fn snap() -> Snapshot {
        Snapshot {
            continuous_seconds: 3600,
            sleeping: false,
            today_total_seconds: 7200,
            apps: vec![("game.exe".to_string(), 3600), ("code.exe".to_string(), 1800)],
            categories: vec![("娱乐".to_string(), 3600)],
            current_process: "code.exe".to_string(),
            hour: 23,
            weekday: 7,
        }
    }

    fn snap_with(
        continuous_minutes: i64,
        apps: &[(&str, i64)],
        categories: &[(&str, i64)],
        hour: i64,
    ) -> Snapshot {
        Snapshot {
            continuous_seconds: continuous_minutes * 60,
            sleeping: false,
            today_total_seconds: apps.iter().map(|(_, s)| *s).sum(),
            apps: apps
                .iter()
                .map(|(n, s)| ((*n).to_string(), *s))
                .collect(),
            categories: categories
                .iter()
                .map(|(n, s)| ((*n).to_string(), *s))
                .collect(),
            current_process: String::new(),
            hour,
            weekday: 2,
        }
    }

    /// 命中返回标题集合，未命中为空；脚本形态与 rules.ts 编译产物一致
    fn hit_titles(script: &str, snapshot: &Snapshot) -> Vec<String> {
        run_script(script, snapshot)
            .expect("脚本应可执行")
            .into_iter()
            .map(|c| c.title)
            .collect()
    }

    const MIN: i64 = 60;

    #[test]
    fn sample_script_collects_all_matches_in_order() {
        let candidates = run_script(SAMPLE_SCRIPT, &snap()).unwrap();
        let ids: Vec<&str> = candidates.iter().map(|c| c.id.as_str()).collect();
        assert_eq!(ids, ["r1", "r3"]);
        assert_eq!(candidates[0].title, "该休息一下了");
        assert_eq!(candidates[1].title, "夜深了");
    }

    #[test]
    fn single_action_table_still_works() {
        let script = r#"return { title = "x", body = "y", cooldownMinutes = 3 }"#;
        let candidates = run_script(script, &snap()).unwrap();
        assert_eq!(candidates.len(), 1);
        assert_eq!(candidates[0].id, "0");
        assert_eq!(candidates[0].cooldown_minutes, 3);
    }

    #[test]
    fn list_keeps_explicit_ids() {
        let script = r#"
local out = {}
out[#out + 1] = { id = "a", title = "一", cooldownMinutes = 5 }
out[#out + 1] = { title = "二", cooldownMinutes = 5 }
return out
"#;
        let candidates = run_script(script, &snap()).unwrap();
        let ids: Vec<&str> = candidates.iter().map(|c| c.id.as_str()).collect();
        assert_eq!(ids, ["a", "1"]);
    }

    #[test]
    fn empty_results_are_allowed() {
        assert!(run_script("return nil", &snap()).unwrap().is_empty());
        assert!(run_script("return {}", &snap()).unwrap().is_empty());
        assert!(run_script("local out = {} return out", &snap()).unwrap().is_empty());
    }

    #[test]
    fn helper_functions_read_host_data() {
        let script = r#"
if todayAppMinutes("game") == 60 and todayCategoryMinutes("娱乐") == 60 then
  return { title = "ok", body = fmtDuration(ctx.continuousSeconds), cooldownMinutes = 5 }
end
return nil
"#;
        let candidates = run_script(script, &snap()).unwrap();
        assert_eq!(candidates.len(), 1);
        assert_eq!(candidates[0].body, "1 小时");
        assert_eq!(candidates[0].cooldown_minutes, 5);
    }

    #[test]
    fn duration_helpers_format_human_readable() {
        let cases = [
            (0u64, "0 秒"),
            (45, "45 秒"),
            (60, "1 分钟"),
            (45 * 60, "45 分钟"),
            (59 * 60 + 59, "59 分钟"),
            (60 * 60, "1 小时"),
            (90 * 60, "1 小时 30 分钟"),
            (125 * 60, "2 小时 5 分钟"),
            (24 * 3600, "24 小时"),
        ];
        for (secs, want) in cases {
            let script = format!(
                r#"return {{ title = "x", body = fmtDuration({secs}), cooldownMinutes = 1 }}"#
            );
            let got = &run_script(&script, &snap()).unwrap()[0].body;
            assert_eq!(got, want, "fmtDuration({secs})");
        }

        // fmtMinutes 走同一套格式，输入是分钟
        for (mins, want) in [(0i64, "0 秒"), (1, "1 分钟"), (120, "2 小时"), (95, "1 小时 35 分钟")] {
            let script = format!(
                r#"return {{ title = "x", body = fmtMinutes({mins}), cooldownMinutes = 1 }}"#
            );
            let got = &run_script(&script, &snap()).unwrap()[0].body;
            assert_eq!(got, want, "fmtMinutes({mins})");
        }
    }

    #[test]
    fn cooldown_is_clamped() {
        let script = r#"return { title = "x", body = "y", cooldownMinutes = 0 }"#;
        assert_eq!(run_script(script, &snap()).unwrap()[0].cooldown_minutes, 1);

        let script = r#"return { title = "x", body = "y", cooldownMinutes = 99999 }"#;
        assert_eq!(run_script(script, &snap()).unwrap()[0].cooldown_minutes, 24 * 60);
    }

    #[test]
    fn candidates_and_text_are_capped() {
        let script = r#"
local out = {}
for i = 1, 100 do
  out[i] = { title = "t" .. i, cooldownMinutes = 1 }
end
return out
"#;
        assert_eq!(run_script(script, &snap()).unwrap().len(), MAX_CANDIDATES);

        let script = r#"return { title = string.rep("长", 500), body = string.rep("文", 900), cooldownMinutes = 1 }"#;
        let candidate = &run_script(script, &snap()).unwrap()[0];
        assert_eq!(candidate.title.chars().count(), TITLE_MAX_CHARS);
        assert_eq!(candidate.body.chars().count(), BODY_MAX_CHARS);
    }

    #[test]
    fn missing_title_is_reported() {
        let err = run_script(r#"return { body = "y" }"#, &snap()).expect_err("应报缺少 title");
        assert!(err.contains("缺少 title"), "{err}");

        let err = run_script(r#"return { { title = "ok" }, { body = "y" } }"#, &snap())
            .expect_err("列表里某一项缺 title 也应报错");
        assert!(err.contains("缺少 title"), "{err}");
    }

    #[test]
    fn sandbox_blocks_io_and_os() {
        // 断言要用会真正报错的写法；tostring(io) 只会拿到 "nil"
        let io_err = run_script(r#"return { title = tostring(io.open), body = "x" }"#, &snap())
            .expect_err("io 必须不可达");
        assert!(io_err.contains("运行失败") || io_err.contains("编译失败"), "{io_err}");

        let os_err = run_script(r#"return os.time() and { title = "x", body = "y" }"#, &snap())
            .expect_err("os 必须不可达");
        assert!(os_err.contains("运行失败") || os_err.contains("编译失败"), "{os_err}");

        assert!(run_script(r#"return require("os") and { title = "x", body = "y" }"#, &snap()).is_err());
    }

    #[test]
    fn syntax_error_is_reported() {
        let err = run_script("if then", &snap()).expect_err("应报编译失败");
        assert!(err.contains("编译失败"), "{err}");
    }

    /// 与 rules.ts 的编译产物同形，保证前端编译器与引擎的契约不破
    fn compiled_rule(id: &str, cond: &str, cooldown: i64) -> String {
        format!(
            "local out = {{}}\n\
             if {cond} then\n  out[#out + 1] = {{\n    \
             id = \"{id}\",\n    title = \"{id}\",\n    body = \"\",\n    \
             cooldownMinutes = {cooldown},\n  }}\nend\nreturn out"
        )
    }

    #[test]
    fn category_limit_fires_only_at_threshold() {
        let rule = compiled_rule("r1", "todayCategoryMinutes(\"娱乐\") >= 120", 60);
        let cats = &[("娱乐", 120 * MIN)];

        assert_eq!(
            hit_titles(&rule, &snap_with(0, &[], cats, 10)),
            ["r1"],
            "正好 120 分钟应触发"
        );
        assert!(
            hit_titles(&rule, &snap_with(0, &[], &[("娱乐", 119 * MIN)], 10)).is_empty(),
            "119 分钟不应触发"
        );
        assert_eq!(
            hit_titles(&rule, &snap_with(0, &[], &[("娱乐", 8 * 3600)], 10)),
            ["r1"],
            "超出后仍持续触发（配合冷却即「每 N 分钟重复提醒」）"
        );
    }

    #[test]
    fn category_limit_is_exact_name_not_substring() {
        let rule = compiled_rule("r1", "todayCategoryMinutes(\"娱乐\") >= 60", 60);
        assert!(
            hit_titles(&rule, &snap_with(0, &[], &[("娱乐视频", 120 * MIN)], 10)).is_empty(),
            "分类按全名取值，子串不匹配"
        );
    }

    #[test]
    fn several_categories_sum_up() {
        let rule = compiled_rule(
            "r1",
            "(todayCategoryMinutes(\"娱乐\") + todayCategoryMinutes(\"社交\")) >= 120",
            60,
        );
        let cats = &[("娱乐", 70 * MIN), ("社交", 50 * MIN), ("工作", 300 * MIN)];
        assert_eq!(
            hit_titles(&rule, &snap_with(0, &[], cats, 10)),
            ["r1"],
            "70+50=120 应触发，且不计入「工作」"
        );
        assert!(
            hit_titles(&rule, &snap_with(0, &[], &[("娱乐", 70 * MIN), ("社交", 49 * MIN)], 10))
                .is_empty()
        );
    }

    #[test]
    fn unknown_category_counts_as_zero() {
        let script =
            compiled_rule("r1", "todayCategoryMinutes(\"不存在的分类\") >= 1", 60);
        assert!(hit_titles(&script, &snap_with(0, &[], &[("娱乐", 600 * MIN)], 10)).is_empty());
    }

    #[test]
    fn app_limit_sums_matches_and_ignores_case() {
        let rule = compiled_rule(
            "r1",
            "(todayAppMinutes(\"Chromium\") + todayAppMinutes(\"wechat\")) >= 90",
            30,
        );
        let apps = &[("chromium", 60 * MIN), ("WeChat", 30 * MIN), ("Code", 600 * MIN)];
        assert_eq!(
            hit_titles(&rule, &snap_with(0, apps, &[], 10)),
            ["r1"],
            "进程名大小写不敏感，子串匹配，第三方应用不计入"
        );
        assert!(hit_titles(&rule, &snap_with(0, &[("chromium", 89 * MIN)], &[], 10)).is_empty());
    }

    #[test]
    fn app_match_is_substring_of_process_name() {
        let rule = compiled_rule("r1", "todayAppMinutes(\"game\") >= 30", 60);
        assert_eq!(
            hit_titles(&rule, &snap_with(0, &[("my-game-launcher.exe", 30 * MIN)], &[], 10)),
            ["r1"]
        );
    }

    #[test]
    fn continuous_limit_uses_minutes_and_ignores_idle() {
        let rule = compiled_rule("r1", "ctx.continuousMinutes >= 60", 30);
        assert_eq!(hit_titles(&rule, &snap_with(60, &[], &[], 10)), ["r1"]);
        assert!(hit_titles(&rule, &snap_with(59, &[], &[], 10)).is_empty());

        let idle = Snapshot {
            continuous_seconds: 0,
            ..snap_with(60, &[], &[], 10)
        };
        assert!(hit_titles(&rule, &idle).is_empty(), "空闲后连续段归零");
    }

    #[test]
    fn daily_total_limit_uses_today_total() {
        let rule = compiled_rule("r1", "ctx.todayTotalMinutes >= 240", 120);
        assert_eq!(
            hit_titles(&rule, &snap_with(0, &[("a", 240 * MIN)], &[], 10)),
            ["r1"]
        );
        assert!(hit_titles(&rule, &snap_with(0, &[("a", 239 * MIN)], &[], 10)).is_empty());
    }

    #[test]
    fn time_window_guards_hour_boundaries() {
        let night = compiled_rule("r1", "(ctx.hour >= 22 or ctx.hour < 6)", 60);
        for hour in [22, 23, 0, 5] {
            assert_eq!(
                hit_titles(&night, &snap_with(0, &[], &[], hour)),
                ["r1"],
                "hour={hour} 应在深夜窗口内"
            );
        }
        for hour in [6, 12, 21] {
            assert!(
                hit_titles(&night, &snap_with(0, &[], &[], hour)).is_empty(),
                "hour={hour} 不应在深夜窗口内"
            );
        }

        let work = compiled_rule("r1", "ctx.hour >= 9 and ctx.hour < 18", 60);
        assert_eq!(hit_titles(&work, &snap_with(0, &[], &[], 9)), ["r1"]);
        assert!(hit_titles(&work, &snap_with(0, &[], &[], 18)).is_empty());
        assert!(hit_titles(&work, &snap_with(0, &[], &[], 8)).is_empty());
    }

    #[test]
    fn window_and_limit_combine_with_and() {
        let rule = compiled_rule(
            "r1",
            "todayCategoryMinutes(\"娱乐\") >= 120 and ctx.hour >= 18",
            60,
        );
        let cats = &[("娱乐", 200 * MIN)];
        assert!(hit_titles(&rule, &snap_with(0, &[], cats, 17)).is_empty(), "时长够但不在时段内");
        assert!(hit_titles(&rule, &snap_with(0, &[], &[("娱乐", 10 * MIN)], 20)).is_empty(), "时段对但时长不够");
        assert_eq!(hit_titles(&rule, &snap_with(0, &[], cats, 20)), ["r1"]);
    }

    #[test]
    fn once_per_day_flag_is_read_from_script() {
        let script = r#"return { title = "x", body = "y", cooldownMinutes = 5, oncePerDay = true }"#;
        let candidate = &run_script(script, &snap()).unwrap()[0];
        assert!(candidate.once_per_day);

        let script = r#"return { title = "x", body = "y", cooldownMinutes = 5 }"#;
        assert!(!run_script(script, &snap()).unwrap()[0].once_per_day);
    }

    /// 由 rules.ts 的 compileRules 生成，锁住「可视化规则 → Lua → 引擎」这条链路
    const FROM_WEBUI: &str = r#"
local out = {}

if ctx.continuousMinutes >= 60 then
  out[#out + 1] = {
    id = "r1",
    title = "该休息一下了",
    body = "已连续使用 " .. fmtMinutes(ctx.continuousMinutes),
    cooldownMinutes = 30,
  }
end

if (todayCategoryMinutes("娱乐") + todayCategoryMinutes("社交")) >= 120 then
  out[#out + 1] = {
    id = "r2",
    title = "「娱乐, 社交」时间到啦",
    body = "「娱乐/社交」今日已用 " .. fmtMinutes(todayCategoryMinutes("娱乐") + todayCategoryMinutes("社交")),
    cooldownMinutes = 120,
    oncePerDay = true,
  }
end

if (todayAppMinutes("Chromium") + todayAppMinutes("WeChat")) >= 90 and (ctx.hour >= 22 or ctx.hour < 6) then
  out[#out + 1] = {
    id = "r3",
    title = "夜深了",
    body = "Chromium/WeChat 今日已用 " .. fmtMinutes(todayAppMinutes("Chromium") + todayAppMinutes("WeChat")),
    cooldownMinutes = 240,
  }
end

return out
"#;

    #[test]
    fn globals_do_not_leak_between_runs() {
        // 脚本写全局（不带 local）不应跨轮可见：与旧的「每轮新建 VM」语义一致
        let script = r#"
counter = (counter or 0) + 1
return { title = tostring(counter), body = "", cooldownMinutes = 1 }
"#;
        let first = &run_script(script, &snap()).unwrap()[0];
        let second = &run_script(script, &snap()).unwrap()[0];
        assert_eq!(first.title, "1");
        assert_eq!(second.title, "1", "第二轮的 counter 不应继承第一轮");
    }

    #[test]
    fn instruction_budget_resets_each_run() {
        // 每轮触发若干次指令钩子；若预算计数不按轮重置，第二轮结束时会翻倍
        let spin = "local i = 0 while i < 100000 do i = i + 1 end return nil";
        run_script(spin, &snap()).unwrap();
        let first = engine_used_batches();
        assert!(first > 0, "校准失败：脚本应触发指令钩子（used={first}）");

        run_script(spin, &snap()).unwrap();
        let second = engine_used_batches();
        assert!(
            second <= first + 1,
            "指令计数应按轮重置：第一轮 {first} 批，第二轮结束 {second} 批"
        );
    }

    #[test]
    fn same_script_reuses_single_engine() {
        let before = engine_builds();
        for _ in 0..3 {
            run_script(FROM_WEBUI, &snap()).unwrap();
        }
        assert_eq!(
            engine_builds(),
            before + 1,
            "同一脚本连续运行只应构建一次引擎（不再每拍重建 VM/重编译）"
        );

        run_script("return nil", &snap()).unwrap();
        assert_eq!(engine_builds(), before + 2, "脚本变更才应重建引擎");

        run_script("return nil", &snap()).unwrap();
        assert_eq!(engine_builds(), before + 2, "变更后保持不变不应再重建");
    }

    /// 基准（默认忽略）：cargo test --release bench_engine -- --ignored --nocapture
    /// 对比「每拍新建 VM+编译」与「复用缓存」的单次耗时，量化 Lua 编译浪费
    #[test]
    #[ignore = "基准，手动运行"]
    fn bench_engine() {
        use std::time::Instant;

        // 代表性数据量：今日 150 个进程、15 个分类
        let apps: Vec<(String, i64)> = (0..150)
            .map(|i| (format!("app-{i:03}.exe"), (i as i64 + 1) * 37))
            .collect();
        let categories: Vec<(String, i64)> = (0..15)
            .map(|i| (format!("分类{i:02}"), (i as i64 + 1) * 900))
            .collect();
        let snap = Snapshot {
            continuous_seconds: 3725,
            sleeping: false,
            today_total_seconds: apps.iter().map(|(_, s)| *s).sum(),
            apps,
            categories,
            current_process: "app-007.exe".to_string(),
            hour: 22,
            weekday: 5,
        };
        let script = FROM_WEBUI;
        const N: u32 = 1000;

        // 旧路径：每次新建 Lua、重编译、再执行（修复前的 run_script 行为）
        let t = Instant::now();
        for _ in 0..N {
            let (lua, _used) = fresh_lua().unwrap();
            let ctx = build_ctx_table(&lua, &snap).unwrap();
            lua.globals().set("ctx", ctx).unwrap();
            let full = format!("{PRELUDE}\n{script}");
            let chunk = lua.load(&full).into_function().unwrap();
            let result: mlua::Value = chunk.call(()).unwrap();
            let _ = collect_candidates(result.as_table().unwrap()).unwrap();
        }
        let per_old = t.elapsed() / N;

        // 新路径：run_script（内部有编译/状态缓存）
        let t = Instant::now();
        for _ in 0..N {
            run_script(script, &snap).unwrap();
        }
        let per_new = t.elapsed() / N;

        // 只计「新建 Lua + 编译」本身（上下文构建另算）
        let t = Instant::now();
        for _ in 0..N {
            let (lua, _used) = fresh_lua().unwrap();
            let _ = lua.load(&format!("{PRELUDE}\n{script}")).into_function().unwrap();
        }
        let per_bare = t.elapsed() / N;

        println!("每拍新建 VM+编译: {per_old:?}/次");
        println!("run_script 现路径: {per_new:?}/次");
        println!("仅新建+编译裸成本: {per_bare:?}/次");
    }

    #[test]
    fn webui_compiled_script_runs_as_configured() {
        let apps = &[("chromium", 60 * MIN), ("WeChat", 30 * MIN)];
        let cats = &[("娱乐", 70 * MIN), ("社交", 50 * MIN)];
        let late = snap_with(60, apps, cats, 23);
        let candidates = run_script(FROM_WEBUI, &late).unwrap();

        assert_eq!(
            candidates.iter().map(|c| c.id.as_str()).collect::<Vec<_>>(),
            ["r1", "r2", "r3"]
        );
        assert_eq!(
            candidates[1].body,
            "「娱乐/社交」今日已用 2 小时",
            "120 分钟应格式化为「2 小时」而不是「120 分钟」"
        );
        assert!(candidates[1].once_per_day, "规则 2 配的是每天一次");
        assert!(!candidates[0].once_per_day && !candidates[2].once_per_day);
        assert_eq!(
            (candidates[0].cooldown_minutes, candidates[2].cooldown_minutes),
            (30, 240)
        );

        // 白天：应用规则的时段条件不满足
        let daytime = snap_with(60, apps, cats, 14);
        assert_eq!(
            hit_titles(FROM_WEBUI, &daytime),
            ["该休息一下了", "「娱乐, 社交」时间到啦"]
        );

        // 分类求和差 1 分钟就不触发
        let short = snap_with(0, &[], &[("娱乐", 69 * MIN), ("社交", 50 * MIN)], 23);
        assert!(hit_titles(FROM_WEBUI, &short).is_empty());
    }
}
