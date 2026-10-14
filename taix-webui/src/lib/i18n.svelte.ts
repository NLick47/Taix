export type Locale = 'zh' | 'en';
export type LocaleChoice = 'auto' | Locale;

const zh = {
  'app.title.list': '插件 · Taix',
  'app.title.config': '{name} 配置 · Taix',
  'app.title.detail': '{name} · Taix',

  'topbar.home': '返回插件列表',
  'topbar.conn': '本地服务 {host}',
  'topbar.connected': '已连接',
  'topbar.reconnecting': '重连中…',
  'topbar.theme': '外观：{label}（点击切换）',
  'topbar.locale': '语言：{label}（点击切换）',

  'theme.auto': '跟随系统',
  'theme.light': '浅色',
  'theme.dark': '深色',
  'locale.auto': '跟随浏览器',
  'locale.zh': '简体中文',
  'locale.en': 'English',

  'status.running': '运行中',
  'status.paused': '已暂停',
  'status.idle': '已启用',
  'status.stopped': '已停用',

  'kind.overlay': '悬浮窗',
  'kind.process': '独立进程',

  'plugins.title': '插件',
  'plugins.sub': '插件常驻后台运行，客户端关闭不受影响',
  'plugins.refresh': '刷新',
  'plugins.refreshTitle': '重新读取插件列表',
  'plugins.loadError': '无法读取插件列表',
  'plugins.loadErrorHint': '本地服务可能未在运行：确认 taix-shell 已启动后重新刷新',
  'plugins.empty': '尚未安装任何插件',
  'plugins.emptyHint': '把插件目录放入安装路径的 plugins/<id>/ 即自动出现',
  'plugins.statTotal': '插件总数',
  'plugins.statEnabled': '已启用',
  'plugins.statDisabled': '已停用',
  'plugins.installed': '已安装',

  'detail.commands': '插件指令',
  'detail.runtime': '运行状态',
  'detail.status': '状态',
  'detail.noStatus': '尚未上报状态，稍候自动刷新',
  'detail.reportEvery': '插件每 5 秒上报一次',
  'detail.disabled': '插件已停用',
  'detail.enableHint': '启用后开始上报运行数据',
  'detail.enable': '启用',
  'detail.meta': '元信息',
  'detail.type': '类型',
  'detail.platforms': '平台',
  'detail.platformAll': '不限',
  'detail.listSep': '、',
  'detail.configVersion': '配置版本',
  'detail.entry': '入口',
  'detail.back': '返回列表',

  'header.overview': '概览',
  'header.config': '配置',

  'config.jsonInvalid': '存在 JSON 格式错误的字段，修正后才能保存',
  'config.leaveConfirm': '有未保存的修改，确定离开？',
  'config.uiFallback': '自带配置界面加载失败，已回退通用表单：{err}',
  'config.uiNoMount': '模块未导出 mount(el, api)',
  'config.instantHint': '保存后即时生效，无需重启',
  'config.jsonParseError': 'JSON 解析失败：{err}（已保留上次合法值，修正后才能保存）',
  'config.saved': '已保存 · 即时生效',
  'config.unsaved': '有 {n} 处未保存',
  'config.discard': '放弃',
  'config.save': '保存配置',
  'config.saving': '保存中…',
  'config.nothing': '该插件没有可配置项',

  'err.api404': '本地服务（{at}）没有这个接口，通常是 taix-server 版本较旧。请重新编译并重启 taix-server 后再试。',
  'err.busy': '本地服务暂时不可用（HTTP {status}），可能正在重启，稍后会自动重连。',
  'err.http': '本地服务返回异常（HTTP {status}），地址 {at}',
  'err.connect': '无法连接本地服务（{host}），请确认 Taix 后台正在运行',
};

const en: Record<keyof typeof zh, string> = {
  'app.title.list': 'Plugins · Taix',
  'app.title.config': '{name} Config · Taix',
  'app.title.detail': '{name} · Taix',

  'topbar.home': 'Back to plugin list',
  'topbar.conn': 'Local service {host}',
  'topbar.connected': 'Connected',
  'topbar.reconnecting': 'Reconnecting…',
  'topbar.theme': 'Appearance: {label} · click to switch',
  'topbar.locale': 'Language: {label} · click to switch',

  'theme.auto': 'Follow system',
  'theme.light': 'Light',
  'theme.dark': 'Dark',
  'locale.auto': 'Follow browser',
  'locale.zh': '简体中文',
  'locale.en': 'English',

  'status.running': 'Running',
  'status.paused': 'Paused',
  'status.idle': 'Enabled',
  'status.stopped': 'Disabled',

  'kind.overlay': 'Overlay window',
  'kind.process': 'Background process',

  'plugins.title': 'Plugins',
  'plugins.sub': 'Plugins run in the background and survive closing the client',
  'plugins.refresh': 'Refresh',
  'plugins.refreshTitle': 'Reload the plugin list',
  'plugins.loadError': 'Could not load the plugin list',
  'plugins.loadErrorHint': 'The local service may not be running: start taix-shell, then refresh',
  'plugins.empty': 'No plugins installed yet',
  'plugins.emptyHint': 'Drop a plugin folder into plugins/<id>/ under the install path and it shows up here',
  'plugins.statTotal': 'Plugins',
  'plugins.statEnabled': 'Enabled',
  'plugins.statDisabled': 'Disabled',
  'plugins.installed': 'Installed',

  'detail.commands': 'Plugin commands',
  'detail.runtime': 'Runtime status',
  'detail.status': 'Status',
  'detail.noStatus': 'No status reported yet, it refreshes automatically',
  'detail.reportEvery': 'The plugin reports every 5 seconds',
  'detail.disabled': 'Plugin is disabled',
  'detail.enableHint': 'Enable it to receive runtime data',
  'detail.enable': 'Enable',
  'detail.meta': 'Metadata',
  'detail.type': 'Type',
  'detail.platforms': 'Platforms',
  'detail.platformAll': 'Any',
  'detail.listSep': ', ',
  'detail.configVersion': 'Config version',
  'detail.entry': 'Entry',
  'detail.back': 'Back to list',

  'header.overview': 'Overview',
  'header.config': 'Config',

  'config.jsonInvalid': 'Some fields contain invalid JSON, fix them before saving',
  'config.leaveConfirm': 'You have unsaved changes. Leave anyway?',
  'config.uiFallback': 'Failed to load the plugin-provided UI, fell back to the generic form: {err}',
  'config.uiNoMount': 'Module does not export mount(el, api)',
  'config.instantHint': 'Takes effect immediately after saving',
  'config.jsonParseError': 'Invalid JSON: {err} · the previous valid value is kept, fix it before saving',
  'config.saved': 'Saved · applied',
  'config.unsaved': 'Unsaved changes ({n})',
  'config.discard': 'Discard',
  'config.save': 'Save',
  'config.saving': 'Saving…',
  'config.nothing': 'This plugin has nothing to configure',

  'err.api404': 'The local service ({at}) does not expose this API, usually because taix-server is outdated. Rebuild and restart taix-server, then retry.',
  'err.busy': 'Local service temporarily unavailable (HTTP {status}), it may be restarting and will reconnect automatically.',
  'err.http': 'Local service returned an error (HTTP {status}) at {at}',
  'err.connect': 'Cannot reach the local service ({host}). Make sure Taix is running.',
};

export type MsgKey = keyof typeof zh;
type Params = Record<string, string | number>;

const CATALOG: Record<Locale, Record<string, string>> = { zh, en };

export function format(tpl: string, params?: Params): string {
  if (!params) return tpl;
  return tpl.replace(/\{(\w+)\}/g, (whole, key: string) => (key in params ? String(params[key]) : whole));
}

function detect(): Locale {
  return (navigator.language || '').toLowerCase().startsWith('zh') ? 'zh' : 'en';
}

function initialChoice(): LocaleChoice {
  try {
    const v = localStorage.getItem('taix-locale');
    return v === 'zh' || v === 'en' ? v : 'auto';
  } catch {
    return 'auto';
  }
}

export const locale = $state({ choice: initialChoice() as LocaleChoice, resolved: 'zh' as Locale });

function apply() {
  locale.resolved = locale.choice === 'auto' ? detect() : locale.choice;
  document.documentElement.lang = locale.resolved === 'zh' ? 'zh-CN' : 'en';
  document.title = t('app.title.list');
}

apply();

const ORDER: LocaleChoice[] = ['auto', 'zh', 'en'];

export function cycleLocale() {
  locale.choice = ORDER[(ORDER.indexOf(locale.choice) + 1) % ORDER.length];
  try {
    localStorage.setItem('taix-locale', locale.choice);
  } catch {
    /* 写失败不影响本次切换 */
  }
  apply();
}

// 系统语言变化只影响 auto 模式
window.addEventListener('languagechange', () => {
  if (locale.choice === 'auto') apply();
});

export function t(key: MsgKey, params?: Params): string {
  return format(CATALOG[locale.resolved][key] ?? zh[key] ?? key, params);
}

export function themeLabel(choice: 'auto' | 'light' | 'dark'): string {
  return t(choice === 'auto' ? 'theme.auto' : choice === 'light' ? 'theme.light' : 'theme.dark');
}

export function localeLabel(choice: LocaleChoice): string {
  return t(choice === 'auto' ? 'locale.auto' : choice === 'zh' ? 'locale.zh' : 'locale.en');
}

const STATUS_KEY = {
  running: 'status.running',
  paused: 'status.paused',
  idle: 'status.idle',
  stopped: 'status.stopped',
} as const satisfies Record<string, MsgKey>;

export function statusLabel(s: keyof typeof STATUS_KEY): string {
  return t(STATUS_KEY[s]);
}

export const localeShort: Record<Locale, string> = { zh: '中', en: 'EN' };
