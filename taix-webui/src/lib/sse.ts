import type { SseEvent } from './types';

// 页面关闭/隐藏即断开（02-webui.md：关闭标签页零连接、无周期轮询）。
// 每个订阅方独立 EventSource + 独立 handler（旧实现是单一全局 handler，
// 第二个订阅方会覆盖前者、卸载时还会把 handler 置空——侧边栏状态点因此失联）。

export type ConnState = 'live' | 'reconnecting';

const connListeners = new Set<(s: ConnState) => void>();

export function onConnChange(fn: (s: ConnState) => void): () => void {
  connListeners.add(fn);
  return () => connListeners.delete(fn);
}

function emitConn(s: ConnState) {
  for (const fn of connListeners) fn(s);
}

export function subscribe(sub: string, onEvent: (e: SseEvent) => void): () => void {
  let stopped = false;
  let source: EventSource | null = null;
  // 每个订阅方独立退避：模块级共享会让多订阅方互相干扰，且只有真正连上才重置
  let backoff = 1000;

  const open = () => {
    if (stopped || source) return;
    const s = new EventSource(`/api/events?sub=${encodeURIComponent(sub)}`);
    source = s;
    s.onopen = () => {
      backoff = 1000;
      emitConn('live');
    };
    s.onmessage = (msg) => {
      try {
        onEvent(JSON.parse(msg.data) as SseEvent);
      } catch {
        /* 忽略坏帧 */
      }
    };
    s.onerror = () => {
      s.close();
      source = null;
      if (stopped) return;
      emitConn('reconnecting');
      setTimeout(open, backoff);
      backoff = Math.min(backoff * 2, 30_000);
    };
  };

  const onVisibility = () => {
    if (document.hidden) {
      source?.close();
      source = null;
    } else {
      open();
    }
  };
  document.addEventListener('visibilitychange', onVisibility);
  open();

  return () => {
    stopped = true;
    document.removeEventListener('visibilitychange', onVisibility);
    source?.close();
    source = null;
  };
}
