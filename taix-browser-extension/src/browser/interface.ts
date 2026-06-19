import { TabInfo, TabActiveInfo, Alarm, SavedState } from '../types';

export interface BrowserAPI {
  storage: {
    get: (keys?: string | string[] | null) => Promise<SavedState>;
    set: (items: Record<string, unknown>) => Promise<void>;
  };

  tabs: {
    get: (tabId: number) => Promise<TabInfo>;
    query: (queryInfo: { active?: boolean; currentWindow?: boolean }) => Promise<TabInfo[]>;
    onActivated: { addListener: (callback: (info: TabActiveInfo) => void) => void };
    onUpdated: { addListener: (callback: (tabId: number, changeInfo: { status?: string; url?: string }, tab: TabInfo) => void) => void };
  };

  windows: {
    getCurrent: () => Promise<{ id: number; type: string }>;
    get: (windowId: number) => Promise<{ id: number; type: string }>;
    // 最近焦点窗口，focused 表示浏览器是否真在前台
    // SW 重启后据此校准 isWindowFocused，防止后台时持续累加时长
    getLastFocused: () => Promise<{ id: number; type: string; focused: boolean }>;
    onFocusChanged: { addListener: (callback: (windowId: number) => void) => void };
    WINDOW_ID_NONE: number;
  };

  webNavigation: {
    onHistoryStateUpdated: { addListener: (callback: (details: { tabId: number; frameId: number; url: string }) => void) => void } | null;
  };

  action: {
    setIcon: (details: { path: Record<string, string> }) => Promise<void>;
  };

  alarms: {
    create: (name: string, alarmInfo: { periodInMinutes?: number }) => void;
    onAlarm: { addListener: (callback: (alarm: Alarm) => void) => void };
  };

  idle: {
    setDetectionInterval?: (intervalSeconds: number) => void;
    queryState?: (detectionIntervalSeconds: number) => Promise<'active' | 'idle' | 'locked'>;
    onStateChanged: { addListener: (callback: (state: 'active' | 'idle' | 'locked') => void) => void } | null;
  };

  runtime: {
    onSuspend: { addListener: (callback: () => void) => void };
  };
}