// 可视化规则 ⇄ Lua 编译（reminder 插件）
// 单一执行路径：规则在前端编译成 Lua 存入 values.script，server 只认 script。
// 每条规则带稳定 id，插件按 id 记冷却；缺省按序号回退。
// 手动改过脚本后切回可视化会提示覆盖（见 ReminderEditor）。

export type RuleType = 'continuous' | 'daily' | 'category' | 'app';
export type TimeWindow = 'all' | 'work' | 'evening' | 'night';

export interface ReminderRule {
  /** 稳定 id，插件按它记冷却；缺省按序号回退 */
  id?: string;
  type: RuleType;
  /** 阈值（分钟） */
  minutes: number;
  /** category=分类名；app=进程名子串，逗号分隔多个 */
  match: string;
  window: TimeWindow;
  title: string;
  /** 提醒后的静默分钟数 */
  cooldown: number;
}

export const RULE_TYPE_LABEL: Record<RuleType, string> = {
  continuous: '连续使用',
  daily: '每日总时长',
  category: '按分类',
  app: '按应用',
};

export const WINDOW_LABEL: Record<TimeWindow, string> = {
  all: '全天',
  work: '白天 9-18 点',
  evening: '晚间 18-24 点',
  night: '深夜 22-6 点',
};

export function defaultRule(type: RuleType): ReminderRule {
  const base: ReminderRule = { type, minutes: 60, match: '', window: 'all', title: '', cooldown: 60 };
  if (type === 'daily') base.minutes = 240;
  return base;
}

function autoTitle(r: ReminderRule): string {
  switch (r.type) {
    case 'continuous': return '该休息一下了';
    case 'daily': return '今日屏幕时间较长';
    case 'category': return r.match ? `「${r.match}」时间到啦` : '时间到啦';
    case 'app': return '该放下了';
  }
}

/** 规则的 Lua 条件表达式（不含 if/end） */
function conditionExpr(r: ReminderRule): string {
  const windowGuard = ((): string => {
    switch (r.window) {
      case 'work': return 'ctx.hour >= 9 and ctx.hour < 18';
      case 'evening': return 'ctx.hour >= 18';
      case 'night': return '(ctx.hour >= 22 or ctx.hour < 6)';
      default: return '';
    }
  })();

  const apps = r.match.split(/[,，]/).map((s) => s.trim()).filter(Boolean);
  const parts: string[] = [];
  switch (r.type) {
    case 'continuous':
      parts.push(`ctx.continuousMinutes >= ${Math.max(1, Math.round(r.minutes))}`);
      break;
    case 'daily':
      parts.push(`ctx.todayTotalMinutes >= ${Math.max(1, Math.round(r.minutes))}`);
      break;
    case 'category':
      parts.push(
        r.match
          ? `todayCategoryMinutes(${luaStr(r.match)}) >= ${Math.max(1, Math.round(r.minutes))}`
          : 'false',
      );
      break;
    case 'app': {
      const sum = apps.map((a) => `todayAppMinutes(${luaStr(a)})`).join(' + ');
      parts.push(sum ? `(${sum}) >= ${Math.max(1, Math.round(r.minutes))}` : 'false');
      break;
    }
  }
  if (windowGuard) parts.push(windowGuard);
  return parts.join(' and ');
}

function luaStr(s: string): string {
  // 用户文本进 Lua 字符串字面量：反斜杠/引号/换行都必须转义，否则生成非法脚本
  return `"${s
    .replace(/\\/g, '\\\\')
    .replace(/"/g, '\\"')
    .replace(/\r/g, '\\r')
    .replace(/\n/g, '\\n')
    .replace(/\t/g, '\\t')}"`;
}

function autoBody(r: ReminderRule): string {
  switch (r.type) {
    case 'continuous': return `"已连续使用 " .. ctx.continuousMinutes .. " 分钟"`;
    case 'daily': return `"今日累计 " .. fmtHMS(ctx.todayTotalSeconds) .. "，注意休息"`;
    case 'category': {
      const name = r.match || '未分类';
      return `${luaStr(`「${name}」今日已用 `)} .. todayCategoryMinutes(${luaStr(name)}) .. " 分钟"`;
    }
    case 'app': {
      const apps = r.match.split(/[,，]/).map((s) => s.trim()).filter(Boolean);
      const sum = apps.map((a) => `todayAppMinutes(${luaStr(a)})`).join(' + ') || '0';
      return `${luaStr(`${apps.join('/')} 今日已用 `)} .. (${sum}) .. " 分钟"`;
    }
  }
}

function describe(r: ReminderRule): string {
  const m = `${Math.round(r.minutes)} 分钟`;
  const target =
    r.type === 'continuous' ? `连续使用满 ${m}`
    : r.type === 'daily' ? `今日累计满 ${m}`
    : r.type === 'category' ? (r.match ? `「${r.match}」今日满 ${m}` : '未选择分类')
    : r.match.trim() ? `应用(${r.match})今日满 ${m}` : '未选择应用';
  return r.window === 'all' ? target : `${target} · ${WINDOW_LABEL[r.window]}`;
}

/** 规则的一句话摘要（规则卡片预览用） */
export function ruleSummary(r: ReminderRule): string {
  return describe(r);
}

/** 规则列表 → Lua 脚本（webui 保存可视化模式时调用） */
export function compileRules(rules: ReminderRule[]): string {
  const out: string[] = [
    '-- 本脚本由「可视化规则」自动生成；切到 Lua 标签可手动微调，',
    '-- 但手动修改后再次保存可视化规则会被覆盖。',
    '',
    'local out = {}',
  ];
  rules
    .map((r, i) => ({ ...r, id: r.id || `r${i + 1}`, title: r.title.trim() || autoTitle(r) }))
    .forEach((r, i) => {
      out.push('');
      out.push(`-- ${i + 1}. ${describe(r)}`);
      out.push(`if ${conditionExpr(r)} then`);
      out.push('  out[#out + 1] = {');
      out.push(`    id = ${luaStr(r.id)},`);
      out.push(`    title = ${luaStr(r.title)},`);
      out.push(`    body = ${autoBody(r)},`);
      out.push(`    cooldownMinutes = ${Math.max(1, Math.round(r.cooldown))},`);
      out.push('  }');
      out.push('end');
    });
  out.push('');
  out.push('return out');
  return out.join('\n');
}

/** 默认规则（首次进入可视化模式时播种，与 server DEFAULT_SCRIPT 的规则 1 对齐） */
export const SEED_RULES: ReminderRule[] = [
  { id: 'r1', type: 'continuous', minutes: 60, match: '', window: 'all', title: '该休息一下了', cooldown: 30 },
];
