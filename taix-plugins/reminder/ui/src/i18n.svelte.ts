import {
  detectLang, durationLabel, repeatLabel, summarizeRule, translate, typeLabel, windowLabel,
  type Lang, type MsgKey, type Params,
} from './i18n.ts';
import type { ReminderRule, Repeat, RuleType, TimeWindow } from './rules.ts';
import type { TaixPluginUiApi } from './bridge.ts';

export { REPEATS, RULE_TYPES, WINDOWS } from './i18n.ts';

const i18n = $state({ lang: 'zh' as Lang });

let off: (() => void) | null = null;

/** 读宿主语言并订阅变化；宿主缺失 locale 时按浏览器语言兜底 */
export function initI18n(api: TaixPluginUiApi) {
  off?.();
  const apply = (value: unknown) => {
    i18n.lang = value === 'en' ? 'en' : 'zh';
  };
  apply(api.locale ?? detectLang(navigator.language));
  off = typeof api.onLocale === 'function' ? api.onLocale(apply) : null;
}

export function t(key: MsgKey, params?: Params): string {
  return translate(i18n.lang, key, params);
}

export function typeText(type: RuleType): string {
  return typeLabel(i18n.lang, type);
}

export function windowText(window: TimeWindow): string {
  return windowLabel(i18n.lang, window);
}

export function repeatText(repeat: Repeat): string {
  return repeatLabel(i18n.lang, repeat);
}

export function ruleSummary(r: ReminderRule): string {
  return summarizeRule(i18n.lang, r);
}

export function durationText(minutes: number): string {
  return durationLabel(i18n.lang, minutes);
}
