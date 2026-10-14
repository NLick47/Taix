use mlua::{Lua, LuaOptions, StdLib};
use serde::Serialize;

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

function fmtHMS(secs)
  local t = math.max(0, math.floor(secs))
  return string.format("%d:%02d:%02d", t // 3600, (t % 3600) // 60, t % 60)
end
"#;

#[cfg(test)]
pub const SAMPLE_SCRIPT: &str = r#"-- 使用时长提醒脚本（Lua）。每 5 秒执行一次：
-- 把本轮命中的规则按顺序放进 out 并返回；插件只提醒第一条不在冷却中的，
-- 其余命中让位 5 分钟。也可直接返回单个 {title=..., body=..., cooldownMinutes=30}，
-- 返回 nil 或空表不提醒。可用数据见「实时数据」面板；辅助函数：todayAppMinutes /
-- todayCategoryMinutes / fmtHMS。修改保存后即时生效。

local out = {}

-- 1) 连续使用满 60 分钟，提醒休息（空闲后自动清零）
if ctx.continuousMinutes >= 60 then
  out[#out + 1] = {
    id = "r1",
    title = "该休息一下了",
    body = "已连续使用 " .. ctx.continuousMinutes .. " 分钟",
    cooldownMinutes = 30,
  }
end

-- 2) 「娱乐」分类今天累计满 2 小时
if todayCategoryMinutes("娱乐") >= 120 then
  out[#out + 1] = {
    id = "r2",
    title = "娱乐时间到头啦",
    body = "今天娱乐分类已用 " .. todayCategoryMinutes("娱乐") .. " 分钟",
    cooldownMinutes = 120,
  }
end

-- 3) 22 点后，含 "game" 的应用今天用了 30 分钟以上（进程名子串匹配）
if ctx.hour >= 22 and todayAppMinutes("game") >= 30 then
  out[#out + 1] = {
    id = "r3",
    title = "夜深了",
    body = "游戏今天已用 " .. todayAppMinutes("game") .. " 分钟",
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
fn fresh_lua() -> Result<Lua, mlua::Error> {
    let libs = StdLib::COROUTINE | StdLib::TABLE | StdLib::MATH | StdLib::STRING | StdLib::UTF8;
    let lua = Lua::new_with(libs, LuaOptions::new())?;
    lua.set_memory_limit(LUA_MEMORY_LIMIT)?;
    let used = std::cell::Cell::new(0u32);
    lua.set_hook(
        mlua::HookTriggers::new().every_nth_instruction(LUA_INSTRUCTION_BATCH),
        move |_lua, _dbg| {
            used.set(used.get() + LUA_INSTRUCTION_BATCH);
            if used.get() >= LUA_INSTRUCTION_BATCH * LUA_INSTRUCTION_BUDGET {
                Err(mlua::Error::RuntimeError(
                    "script exceeded instruction budget".to_string(),
                ))
            } else {
                Ok(mlua::VmState::Continue)
            }
        },
    );
    Ok(lua)
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

/// 返回全部命中，按脚本顺序；nil 或空表 = 不提醒
pub fn run_script(script: &str, snap: &Snapshot) -> Result<Vec<RemindCandidate>, String> {
    let lua = fresh_lua().map_err(|e| format!("Lua 初始化失败: {e}"))?;
    let ctx = build_ctx_table(&lua, snap).map_err(|e| format!("构建上下文失败: {e}"))?;
    lua.globals().set("ctx", ctx).map_err(|e| e.to_string())?;

    let full = format!("{PRELUDE}\n{script}");
    let chunk = lua
        .load(&full)
        .set_name("reminder")
        .into_function()
        .map_err(|e| format!("脚本编译失败: {e}"))?;

    let result: mlua::Value = chunk.call(()).map_err(|e| format!("脚本运行失败: {e}"))?;
    match result {
        mlua::Value::Nil => Ok(Vec::new()),
        mlua::Value::Table(table) => collect_candidates(&table),
        _ => Err("脚本返回类型不支持，应为表或 nil".to_string()),
    }
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
  return { title = "ok", body = fmtHMS(ctx.continuousSeconds), cooldownMinutes = 5 }
end
return nil
"#;
        let candidates = run_script(script, &snap()).unwrap();
        assert_eq!(candidates.len(), 1);
        assert_eq!(candidates[0].body, "1:00:00");
        assert_eq!(candidates[0].cooldown_minutes, 5);
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
    body = "已连续使用 " .. ctx.continuousMinutes .. " 分钟",
    cooldownMinutes = 30,
  }
end

if (todayCategoryMinutes("娱乐") + todayCategoryMinutes("社交")) >= 120 then
  out[#out + 1] = {
    id = "r2",
    title = "「娱乐, 社交」时间到啦",
    body = "「娱乐/社交」今日已用 " .. (todayCategoryMinutes("娱乐") + todayCategoryMinutes("社交")) .. " 分钟",
    cooldownMinutes = 120,
    oncePerDay = true,
  }
end

if (todayAppMinutes("Chromium") + todayAppMinutes("WeChat")) >= 90 and (ctx.hour >= 22 or ctx.hour < 6) then
  out[#out + 1] = {
    id = "r3",
    title = "夜深了",
    body = "Chromium/WeChat 今日已用 " .. (todayAppMinutes("Chromium") + todayAppMinutes("WeChat")) .. " 分钟",
    cooldownMinutes = 240,
  }
end

return out
"#;

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
        assert_eq!(candidates[1].body, "「娱乐/社交」今日已用 120 分钟");
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
