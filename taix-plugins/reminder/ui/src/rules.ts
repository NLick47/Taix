export type RuleType = 'continuous' | 'daily' | 'category' | 'app';
export type TimeWindow = 'all' | 'work' | 'evening' | 'night';
/** 冷却到期后是否再提醒 */
export type Repeat = 'repeat' | 'once';

export interface ReminderRule {
  /** 稳定 id，插件按它记冷却；缺省按序号回退 */
  id?: string;
  type: RuleType;
  /** 阈值（分钟） */
  minutes: number;
  /** category=分类名；app=进程名子串；均支持逗号分隔多个，时长求和 */
  match: string;
  window: TimeWindow;
  title: string;
  /** 提醒后的静默分钟数 */
  cooldown: number;
  /** once = 当天只提醒一次，跨天自动重置 */
  repeat?: Repeat;
}

export const RULE_TYPE_LABEL: Record<RuleType, string> = {
  continuous: '连续使用',
  daily: '每日总时长',
  category: '按分类',
  app: '按应用',
};

export const REPEAT_LABEL: Record<Repeat, string> = {
  repeat: '重复提醒',
  once: '每天一次',
};

export const WINDOW_LABEL: Record<TimeWindow, string> = {
  all: '全天',
  work: '白天 9-18 点',
  evening: '晚间 18-24 点',
  night: '深夜 22-6 点',
};

/** 逗号分隔的匹配词列表；中英文逗号都认 */
export function matchList(match: string): string[] {
  return match.split(/[,，]/).map((s) => s.trim()).filter(Boolean);
}

export function defaultRule(type: RuleType): ReminderRule {
  const base: ReminderRule = {
    type, minutes: 60, match: '', window: 'all', title: '', cooldown: 60, repeat: 'repeat',
  };
  if (type === 'daily') base.minutes = 240;
  return base;
}

function threshold(minutes: number): number {
  return Math.max(1, Math.round(minutes));
}

function autoTitle(r: ReminderRule): string {
  switch (r.type) {
    case 'continuous': return '该休息一下了';
    case 'daily': return '今日屏幕时间较长';
    case 'category': return r.match ? `「${r.match}」时间到啦` : '时间到啦';
    case 'app': return '该放下了';
  }
}

/** 规则的 Lua 条件表达式，不含 if/end */
function conditionExpr(r: ReminderRule): string {
  const windowGuard = ((): string => {
    switch (r.window) {
      case 'work': return 'ctx.hour >= 9 and ctx.hour < 18';
      case 'evening': return 'ctx.hour >= 18';
      case 'night': return '(ctx.hour >= 22 or ctx.hour < 6)';
      default: return '';
    }
  })();

  const apps = matchList(r.match);
  const limit = threshold(r.minutes);
  const parts: string[] = [];
  switch (r.type) {
    case 'continuous':
      parts.push(`ctx.continuousMinutes >= ${limit}`);
      break;
    case 'daily':
      parts.push(`ctx.todayTotalMinutes >= ${limit}`);
      break;
    case 'category': {
      const sum = matchList(r.match)
        .map((c) => `todayCategoryMinutes(${luaStr(c)})`)
        .join(' + ');
      parts.push(sum ? `(${sum}) >= ${limit}` : 'false');
      break;
    }
    case 'app': {
      const sum = apps.map((a) => `todayAppMinutes(${luaStr(a)})`).join(' + ');
      parts.push(sum ? `(${sum}) >= ${limit}` : 'false');
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
      const cats = matchList(r.match);
      const sum = cats.map((c) => `todayCategoryMinutes(${luaStr(c)})`).join(' + ') || '0';
      const shown = cats.length ? cats.join('/') : '未分类';
      return `${luaStr(`「${shown}」今日已用 `)} .. (${sum}) .. " 分钟"`;
    }
    case 'app': {
      const apps = matchList(r.match);
      const sum = apps.map((a) => `todayAppMinutes(${luaStr(a)})`).join(' + ') || '0';
      return `${luaStr(`${apps.join('/')} 今日已用 `)} .. (${sum}) .. " 分钟"`;
    }
  }
}

function describe(r: ReminderRule): string {
  const limit = `满 ${Math.round(r.minutes)} 分钟`;
  const names = matchList(r.match);
  const target =
    r.type === 'continuous' ? `连续使用${limit}`
    : r.type === 'daily' ? `今日累计${limit}`
    : r.type === 'category' ? (names.length ? `「${names.join(' + ')}」今日${limit}` : '未选择分类')
    : names.length ? `应用(${names.join(' + ')})${limit}` : '未选择应用';
  const scope = r.window === 'all' ? target : `${target} · ${WINDOW_LABEL[r.window]}`;
  return r.repeat === 'once' ? `${scope} · 每天一次` : scope;
}

/** 规则的一句话摘要，规则卡片预览用 */
export function ruleSummary(r: ReminderRule): string {
  return describe(r);
}

/** 规则列表 → Lua 脚本，保存时经 patch 回写 values.script */
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
      // 摘要进 Lua 注释：用户文本里的换行会把注释拆成代码行
      out.push(`-- ${i + 1}. ${describe(r).replace(/[\r\n]+/g, ' ')}`);
      out.push(`if ${conditionExpr(r)} then`);
      out.push('  out[#out + 1] = {');
      out.push(`    id = ${luaStr(r.id)},`);
      out.push(`    title = ${luaStr(r.title)},`);
      out.push(`    body = ${autoBody(r)},`);
      out.push(`    cooldownMinutes = ${threshold(r.cooldown)},`);
      if (r.repeat === 'once') out.push('    oncePerDay = true,');
      out.push('  }');
      out.push('end');
    });
  out.push('');
  out.push('return out');
  return out.join('\n');
}

/** 默认规则，首次进入可视化模式时播种，与 server DEFAULT_SCRIPT 的规则 1 对齐 */
export const SEED_RULES: ReminderRule[] = [
  { id: 'r1', type: 'continuous', minutes: 60, match: '', window: 'all', title: '该休息一下了',
    cooldown: 30, repeat: 'repeat' },
];
