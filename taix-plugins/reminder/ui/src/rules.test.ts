import { test } from 'node:test';
import assert from 'node:assert/strict';

import {
  compileRules, defaultRule, ruleSummary, matchList, fmtMinutesLabel, REPEAT_LABEL,
  type ReminderRule, type RuleType,
} from './rules.ts';
import { durationLabel, summarizeRule } from './i18n.ts';

function rule(patch: Partial<ReminderRule> = {}): ReminderRule {
  return { id: 'r1', type: 'category', minutes: 120, match: '娱乐', window: 'all',
    title: '娱乐到点', cooldown: 60, repeat: 'repeat', ...patch };
}

/** 取编译产物里第一条规则的条件表达式 */
function conditionOf(script: string): string {
  const m = /if (.+) then/.exec(script);
  if (!m) throw new Error(`脚本里应有 if 条件：${script}`);
  return m[1];
}

test('编译产物是可返回候选列表的 Lua', () => {
  const s = compileRules([rule()]);
  assert.match(s, /^local out = \{\}/m);
  assert.match(s.trimEnd(), /return out$/);
  assert.equal(s.match(/if .+ then/g)?.length, 1);
});

test('分类规则：单个分类按全名取时长', () => {
  assert.equal(
    conditionOf(compileRules([rule({ match: '娱乐', minutes: 120 })])),
    '(todayCategoryMinutes("娱乐")) >= 120',
  );
});

test('分类规则：多个分类时长求和', () => {
  assert.equal(
    conditionOf(compileRules([rule({ match: '娱乐, 社交', minutes: 120 })])),
    '(todayCategoryMinutes("娱乐") + todayCategoryMinutes("社交")) >= 120',
  );
  assert.equal(
    conditionOf(compileRules([rule({ match: '娱乐，社交，视频' })])),
    '(todayCategoryMinutes("娱乐") + todayCategoryMinutes("社交") + todayCategoryMinutes("视频")) >= 120',
    '中文逗号同样分隔',
  );
});

test('应用规则：多个匹配词求和，空匹配永不命中', () => {
  assert.equal(
    conditionOf(compileRules([rule({ type: 'app', match: 'Chromium, WeChat', minutes: 90 })])),
    '(todayAppMinutes("Chromium") + todayAppMinutes("WeChat")) >= 90',
  );
  assert.equal(conditionOf(compileRules([rule({ type: 'app', match: '  ' })])), 'false');
  assert.equal(conditionOf(compileRules([rule({ match: '' })])), 'false');
});

test('未选择分类/应用的规则编译成 false，不会因缺数据误报', () => {
  for (const type of ['category', 'app'] as RuleType[]) {
    const s = compileRules([rule({ type, match: '' })]);
    assert.equal(conditionOf(s), 'false');
    assert.match(s, /if false then/);
  }
});

test('连续使用与每日总时长读不同的口径', () => {
  assert.equal(
    conditionOf(compileRules([rule({ type: 'continuous', minutes: 60 })])),
    'ctx.continuousMinutes >= 60',
  );
  assert.equal(
    conditionOf(compileRules([rule({ type: 'daily', minutes: 240 })])),
    'ctx.todayTotalMinutes >= 240',
  );
});

test('时段窗口叠加在时长条件之后', () => {
  assert.equal(
    conditionOf(compileRules([rule({ window: 'work' })])),
    '(todayCategoryMinutes("娱乐")) >= 120 and ctx.hour >= 9 and ctx.hour < 18',
  );
  assert.equal(
    conditionOf(compileRules([rule({ window: 'night' })])),
    '(todayCategoryMinutes("娱乐")) >= 120 and (ctx.hour >= 22 or ctx.hour < 6)',
  );
  assert.equal(
    conditionOf(compileRules([rule({ window: 'evening' })])),
    '(todayCategoryMinutes("娱乐")) >= 120 and ctx.hour >= 18',
  );
  assert.equal(conditionOf(compileRules([rule({ window: 'all' })])), '(todayCategoryMinutes("娱乐")) >= 120');
});

test('阈值与冷却至少 1 分钟', () => {
  const s = compileRules([rule({ minutes: 0.4, cooldown: 0 })]);
  assert.match(s, />= 1\b/);
  assert.match(s, /cooldownMinutes = 1,/);
  const rounded = compileRules([rule({ minutes: 59.6, cooldown: 30.4 })]);
  assert.match(rounded, />= 60\b/);
  assert.match(rounded, /cooldownMinutes = 30,/);
});

test('每天一次编译出 oncePerDay，重复提醒不带该字段', () => {
  const once = compileRules([rule({ repeat: 'once' })]);
  assert.match(once, /oncePerDay = true,/);
  assert.match(once, /每天一次/);

  const repeat = compileRules([rule({ repeat: 'repeat' })]);
  assert.doesNotMatch(repeat, /oncePerDay/);
  assert.doesNotMatch(repeat, /每天一次/);
});

test('规则 id 缺省按序号回退，冷却按 id 记', () => {
  const s = compileRules([rule({ id: undefined }), rule({ id: 'custom' })]);
  assert.match(s, /id = "r1",/);
  assert.match(s, /id = "custom",/);
  const both = compileRules([rule({ id: undefined }), rule({ id: undefined })]);
  assert.match(both, /id = "r2",/);
});

test('标题留空时自动填充', () => {
  assert.match(compileRules([rule({ type: 'continuous', title: '' })]), /title = "该休息一下了",/);
  assert.match(compileRules([rule({ title: '  自定义  ' })]), /title = "自定义",/);
});

test('分类名里的引号、反斜杠与换行不会破坏生成的 Lua', () => {
  const s = compileRules([rule({ match: 'a"b\\c\nd' })]);
  assert.match(s, /todayCategoryMinutes\("a\\"b\\\\c\\nd"\)/);

  const codeLines = s.split('\n').filter((l) => l.trim() && !l.trimStart().startsWith('--'));
  assert.equal(codeLines.filter((l) => /\bif /.test(l)).length, 1, '换行不能把注释拆成多行代码');
  assert.match(s, /if \(todayCategoryMinutes\("a\\"b\\\\c\\nd"\)\) >= 120 then/);

  const commentLines = s.split('\n').filter((l) => l.startsWith('--'));
  assert.ok(commentLines.every((l) => !l.includes('\n')));
});

test('matchList 兼容中英文逗号与空白', () => {
  assert.deepEqual(matchList('a, b，c '), ['a', 'b', 'c']);
  assert.deepEqual(matchList(' , '), []);
  assert.deepEqual(matchList('单'), ['单']);
});

test('ruleSummary 说明阈值、时段与频率', () => {
  assert.equal(ruleSummary(rule({ minutes: 120 })), '「娱乐」今日满 2 小时');
  assert.equal(
    ruleSummary(rule({ match: '娱乐,社交', window: 'night', repeat: 'once' })),
    '「娱乐 + 社交」今日满 2 小时 · 深夜 22-6 点 · 每天一次',
  );
  assert.equal(ruleSummary(rule({ match: '' })), '未选择分类');
  assert.equal(ruleSummary(rule({ type: 'app', match: '' })), '未选择应用');
});

test('defaultRule 的默认值可用且带重复提醒', () => {
  for (const type of ['continuous', 'daily', 'category', 'app'] as RuleType[]) {
    const r = defaultRule(type);
    assert.equal(r.repeat, 'repeat');
    assert.ok(r.minutes >= 1 && r.cooldown >= 1);
    assert.equal(r.window, 'all');
  }
  assert.equal(defaultRule('daily').minutes, 240);
  assert.equal(Object.keys(REPEAT_LABEL).length, 2);
});

test('多条规则按序编译，注释带人类可读摘要', () => {
  const s = compileRules([
    rule({ id: 'r1', type: 'continuous', minutes: 60 }),
    rule({ id: 'r2', type: 'category', minutes: 120 }),
  ]);
  const first = s.indexOf('id = "r1"');
  const second = s.indexOf('id = "r2"');
  assert.ok(first > -1 && second > first, '规则顺序应保持');
  assert.match(s, /-- 1\. 连续使用满 1 小时/);
  assert.match(s, /-- 2\. 「娱乐」今日满 2 小时/);
});

test('durationLabel 中英双语都带单位且口径一致', () => {
  assert.equal(durationLabel('zh', 30), '30 分钟');
  assert.equal(durationLabel('zh', 60), '1 小时');
  assert.equal(durationLabel('zh', 95), '1 小时 35 分钟');
  assert.equal(durationLabel('zh', 120), '2 小时');

  assert.equal(durationLabel('en', 30), '30 min');
  assert.equal(durationLabel('en', 60), '1 hr');
  assert.equal(durationLabel('en', 95), '1 hr 35 min');
  assert.equal(durationLabel('en', 120), '2 hr');
});

test('summarizeRule 的阈值按语言格式化', () => {
  const r = rule({ minutes: 120 });
  assert.equal(summarizeRule('zh', r), '「娱乐」今日满 2 小时');
  assert.equal(summarizeRule('en', r), '「娱乐」 over 2 hr today');

  assert.equal(summarizeRule('zh', rule({ minutes: 45 })), '「娱乐」今日满 45 分钟');
  assert.equal(summarizeRule('en', rule({ minutes: 45 })), '「娱乐」 over 45 min today');
});

test('fmtMinutesLabel 按中文时长口径格式化', () => {
  assert.equal(fmtMinutesLabel(0), '0 分钟');
  assert.equal(fmtMinutesLabel(30), '30 分钟');
  assert.equal(fmtMinutesLabel(59), '59 分钟');
  assert.equal(fmtMinutesLabel(60), '1 小时');
  assert.equal(fmtMinutesLabel(95), '1 小时 35 分钟');
  assert.equal(fmtMinutesLabel(120), '2 小时');
});

test('编译出的正文用友好时长，不再拼「xxx 分钟」', () => {
  const continuous = compileRules([rule({ id: 'r1', type: 'continuous', minutes: 60 })]);
  assert.match(continuous, /body = "已连续使用 " \.\. fmtMinutes\(ctx\.continuousMinutes\)/);

  const daily = compileRules([rule({ id: 'r2', type: 'daily', minutes: 240 })]);
  assert.match(daily, /fmtDuration\(ctx\.todayTotalSeconds\)/);

  const category = compileRules([rule({ id: 'r3', type: 'category', match: '娱乐,社交' })]);
  assert.match(
    category,
    /body = "「娱乐\/社交」今日已用 " \.\. fmtMinutes\(todayCategoryMinutes\("娱乐"\) \+ todayCategoryMinutes\("社交"\)\)/,
  );

  const app = compileRules([rule({ id: 'r4', type: 'app', match: 'Chromium,WeChat' })]);
  assert.match(app, /fmtMinutes\(todayAppMinutes\("Chromium"\) \+ todayAppMinutes\("WeChat"\)\)/);

  assert.doesNotMatch(continuous + daily + category + app, /\.\. " 分钟"/);
});
