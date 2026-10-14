import { matchList, type ReminderRule, type Repeat, type RuleType, type TimeWindow } from './rules.ts';

export type Lang = 'zh' | 'en';

export const zh = {
  'bar.visual': '可视化',
  'bar.sound': '提示音',
  'bar.running': '运行中…',
  'bar.dryRun': '试运行',

  'warn.overwriteRisk': '已存的 Lua 脚本与当前规则编译结果不同，保存会用规则覆盖该脚本',
  'warn.overwriteSession': 'Lua 脚本有本次会话内的手动修改，保存可视化规则会覆盖这些修改',

  'rules.empty': '还没有提醒规则',
  'rules.emptyHint': '从下方添加第一条规则',
  'rules.delete': '删除规则',

  'rule.category': '分类（可多选，时长合计）',
  'rule.appMatch': '应用匹配',
  'rule.threshold': '时长',
  'rule.window': '时段',
  'rule.cooldown': '间隔',
  'rule.cooldownOnceHint': '每天只提醒一次，无需重复间隔',
  'rule.cooldownHint': '提醒后的静默分钟',
  'rule.repeat': '频率',
  'rule.title': '通知标题',
  'rule.titlePlaceholder': '留空自动生成',
  'rule.add': '添加',

  'type.continuous': '连续使用',
  'type.daily': '每日总时长',
  'type.category': '按分类',
  'type.app': '按应用',

  'window.all': '全天',
  'window.work': '白天 9-18 点',
  'window.evening': '晚间 18-24 点',
  'window.night': '深夜 22-6 点',

  'repeat.repeat': '重复提醒',
  'repeat.once': '每天一次',

  'sum.continuous': '连续使用满 {min} 分钟',
  'sum.daily': '今日累计满 {min} 分钟',
  'sum.category': '「{names}」今日满 {min} 分钟',
  'sum.categoryNone': '未选择分类',
  'sum.app': '应用({names})满 {min} 分钟',
  'sum.appNone': '未选择应用',

  'lua.title': '提醒规则 · Lua',
  'lua.noSync': '手动修改后可视化编辑器不会同步',
  'lua.sandbox': 'Lua 5.4 沙箱（无 io / os）',
  'lua.lines': '{n} 行',
  'lua.every': '每 5 秒执行',

  'dry.noResponse': '无响应',
  'dry.noResponseBody': '提醒插件未响应，确认它已启用并在运行',
  'dry.scriptError': '脚本错误',
  'dry.noTrigger': '不触发',
  'dry.noTriggerBody': '当前数据下没有任何规则命中',
  'dry.wouldNotify': '会提醒',
  'dry.rateWrap': '（{rate}）',
  'dry.bodyJoin': '：{body}',
  'dry.matchedNote': '命中 {n} 条，其余：{others}',
  'dry.heldBack': '暂不提醒',
  'dry.heldBackBody': '命中的 {n} 条都在冷却或今天已提醒过：{others}',
  'dry.leftNotified': '今天已提醒过',
  'dry.leftMinutes': '剩余 {n} 分钟',
  'dry.leftAvailable': '也可提醒',
  'dry.rateOnce': '每天一次',
  'dry.rateCooldown': '冷却 {n} 分钟',

  'picker.categoryPlaceholder': '选择分类，或输入分类名',
  'picker.categoryEmpty': '还没有分类，可直接输入分类名',
  'picker.appPlaceholder': '选择应用，或输入匹配词',
  'picker.appEmpty': '还没有记录到应用',
  'picker.search': '搜索应用，回车添加自定义匹配词',
  'picker.remove': '移除',
  'picker.matched': '匹配词 {word}',
  'picker.unmatched': '{word} 未匹配到任何已记录的应用',
  'picker.addCustom': '添加「{q}」作为匹配词',
};

export const en: Record<keyof typeof zh, string> = {
  'bar.visual': 'Visual',
  'bar.sound': 'Sound',
  'bar.running': 'Running…',
  'bar.dryRun': 'Dry run',

  'warn.overwriteRisk': 'The saved Lua script differs from the current rules; saving overwrites it with the rules',
  'warn.overwriteSession': 'The Lua script has manual edits from this session; saving visual rules overwrites them',

  'rules.empty': 'No reminder rules yet',
  'rules.emptyHint': 'Add your first rule below',
  'rules.delete': 'Delete rule',

  'rule.category': 'Category · multi-select, minutes summed',
  'rule.appMatch': 'App match',
  'rule.threshold': 'Minutes',
  'rule.window': 'Window',
  'rule.cooldown': 'Cooldown',
  'rule.cooldownOnceHint': 'Notified once a day, no cooldown needed',
  'rule.cooldownHint': 'Silent minutes after a reminder',
  'rule.repeat': 'Repeat',
  'rule.title': 'Notification title',
  'rule.titlePlaceholder': 'Leave empty to auto-generate',
  'rule.add': 'Add',

  'type.continuous': 'Continuous use',
  'type.daily': 'Daily total',
  'type.category': 'By category',
  'type.app': 'By app',

  'window.all': 'All day',
  'window.work': 'Day 9-18',
  'window.evening': 'Evening 18-24',
  'window.night': 'Night 22-6',

  'repeat.repeat': 'Repeat',
  'repeat.once': 'Once a day',

  'sum.continuous': 'Continuous use over {min} min',
  'sum.daily': 'Today total over {min} min',
  'sum.category': '「{names}」 over {min} min today',
  'sum.categoryNone': 'No category selected',
  'sum.app': 'App ({names}) over {min} min',
  'sum.appNone': 'No app selected',

  'lua.title': 'Reminder rules · Lua',
  'lua.noSync': 'The visual editor will not sync manual edits',
  'lua.sandbox': 'Lua 5.4 sandbox · no io / os',
  'lua.lines': '{n} lines',
  'lua.every': 'Runs every 5 s',

  'dry.noResponse': 'No response',
  'dry.noResponseBody': 'The reminder plugin did not respond; make sure it is enabled and running',
  'dry.scriptError': 'Script error',
  'dry.noTrigger': 'No trigger',
  'dry.noTriggerBody': 'No rule matches the current data',
  'dry.wouldNotify': 'Would notify',
  'dry.rateWrap': '({rate})',
  'dry.bodyJoin': ': {body}',
  'dry.matchedNote': '{n} matched, others: {others}',
  'dry.heldBack': 'Held back',
  'dry.heldBackBody': 'All {n} matches are cooling down or already notified today: {others}',
  'dry.leftNotified': 'already notified today',
  'dry.leftMinutes': '{n} min left',
  'dry.leftAvailable': 'can also notify',
  'dry.rateOnce': 'once a day',
  'dry.rateCooldown': 'cooldown {n} min',

  'picker.categoryPlaceholder': 'Pick categories or type a name',
  'picker.categoryEmpty': 'No categories yet, type a name directly',
  'picker.appPlaceholder': 'Pick apps or type a match word',
  'picker.appEmpty': 'No apps recorded yet',
  'picker.search': 'Search apps, Enter adds a custom match word',
  'picker.remove': 'Remove',
  'picker.matched': 'Match word: {word}',
  'picker.unmatched': '{word} matches no recorded app',
  'picker.addCustom': 'Add “{q}” as a match word',
};

export type MsgKey = keyof typeof zh;
export type Params = Record<string, string | number>;

export function format(tpl: string, params?: Params): string {
  if (!params) return tpl;
  return tpl.replace(/\{(\w+)\}/g, (whole, key: string) => (key in params ? String(params[key]) : whole));
}

export function detectLang(navLang: string | null | undefined): Lang {
  return (navLang || '').toLowerCase().startsWith('zh') ? 'zh' : 'en';
}

export function translate(lang: Lang, key: MsgKey, params?: Params): string {
  const dict = lang === 'en' ? en : zh;
  return format(dict[key] ?? zh[key] ?? key, params);
}

export const RULE_TYPES: RuleType[] = ['continuous', 'daily', 'category', 'app'];
export const WINDOWS: TimeWindow[] = ['all', 'work', 'evening', 'night'];
export const REPEATS: Repeat[] = ['repeat', 'once'];

const TYPE_KEY: Record<RuleType, MsgKey> = {
  continuous: 'type.continuous',
  daily: 'type.daily',
  category: 'type.category',
  app: 'type.app',
};

const WINDOW_KEY: Record<TimeWindow, MsgKey> = {
  all: 'window.all',
  work: 'window.work',
  evening: 'window.evening',
  night: 'window.night',
};

const REPEAT_KEY: Record<Repeat, MsgKey> = {
  repeat: 'repeat.repeat',
  once: 'repeat.once',
};

export function typeLabel(lang: Lang, type: RuleType): string {
  return translate(lang, TYPE_KEY[type]);
}

export function windowLabel(lang: Lang, window: TimeWindow): string {
  return translate(lang, WINDOW_KEY[window]);
}

export function repeatLabel(lang: Lang, repeat: Repeat): string {
  return translate(lang, REPEAT_KEY[repeat]);
}

/** 规则卡片的一句话摘要，与 compileRules 写进 Lua 注释的口径一致 */
export function summarizeRule(lang: Lang, r: ReminderRule): string {
  const min = Math.round(r.minutes);
  const names = matchList(r.match);
  let target: string;
  switch (r.type) {
    case 'continuous':
      target = translate(lang, 'sum.continuous', { min });
      break;
    case 'daily':
      target = translate(lang, 'sum.daily', { min });
      break;
    case 'category':
      target = names.length
        ? translate(lang, 'sum.category', { names: names.join(' + '), min })
        : translate(lang, 'sum.categoryNone');
      break;
    case 'app':
      target = names.length
        ? translate(lang, 'sum.app', { names: names.join(' + '), min })
        : translate(lang, 'sum.appNone');
      break;
  }
  const scope = r.window === 'all' ? target : `${target} · ${windowLabel(lang, r.window)}`;
  return r.repeat === 'once' ? `${scope} · ${repeatLabel(lang, 'once')}` : scope;
}
