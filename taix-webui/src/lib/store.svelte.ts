import { api } from './api';
import type { PluginListItem } from './types';
import { onConnChange, subscribe, type ConnState } from './sse';

// 应用级共享状态（Svelte 5 runes 模块）：多个页面共用同一份插件列表
export const pluginStore = $state({
  items: [] as PluginListItem[],
  loaded: false,
  error: '',
  conn: 'reconnecting' as ConnState,
});

// 配置页注册的离开守卫：返回 true 表示允许离开
let guard: (() => boolean) | null = null;

export function setLeaveGuard(fn: (() => boolean) | null) {
  guard = fn;
}

export function canLeave() {
  return guard ? guard() : true;
}

// 配置页画布宽度信号，默认窄；插件自带 UI 时由页面置宽
// 宽度上限落在画布元素上，见 App.svelte
export const layout = $state({ canvasWide: false });

export function setCanvasWide(wide: boolean) {
  layout.canvasWide = wide;
}

export async function refreshPlugins() {
  try {
    pluginStore.items = await api.listPlugins();
    pluginStore.error = '';
  } catch (e) {
    pluginStore.error = String(e);
  }
  pluginStore.loaded = true;
}

/// 挂载一次：初始加载 + SSE 状态增量刷新
export function connectPluginStore() {
  refreshPlugins();
  const offConn = onConnChange((s) => (pluginStore.conn = s));
  const offSub = subscribe('webui', (e) => {
    if (e.type === 'plugin-state' && typeof e.id === 'string') {
      const p = pluginStore.items.find((x) => x.id === e.id);
      if (!p) return;
      if (typeof e.state === 'string') p.state = e.state;
      // 启停事件带 data.enabled：另一个标签页切开关时同步本地状态
      const enabled = (e.data as { enabled?: unknown } | undefined)?.enabled;
      if (typeof enabled === 'boolean') p.enabled = enabled;
    }
  });
  return () => {
    offConn();
    offSub();
  };
}

export type Status = 'running' | 'paused' | 'idle' | 'stopped';

export function statusOf(p: { state: string | null; enabled: boolean }): Status {
  if (p.state === 'running' || p.state === 'paused') return p.state;
  return p.enabled ? 'idle' : 'stopped';
}

export const statusLabel: Record<Status, string> = {
  running: '运行中',
  paused: '已暂停',
  idle: '已启用',
  stopped: '已停用',
};
