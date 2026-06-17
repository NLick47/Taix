import { BrowserAPI } from './interface';
import { TabInfo, SavedState } from '../types';

declare const browser: {
  storage: { local: { get: (keys: string | string[] | null) => Promise<SavedState>; set: (items: Record<string, unknown>) => Promise<void> } };
  tabs: {
    get: (tabId: number) => Promise<{ id: number; windowId: number; url?: string; title?: string; favIconUrl?: string; active: boolean }>;
    query: (queryInfo: { active?: boolean; currentWindow?: boolean }) => Promise<{ id: number; windowId: number; url?: string; title?: string; favIconUrl?: string; active: boolean }[]>;
    onActivated: { addListener: (cb: (arg: { tabId: number; windowId: number }) => void) => void };
    onUpdated: { addListener: (cb: (tabId: number, changeInfo: { status?: string; url?: string }, tab: { id: number; windowId: number; url?: string; title?: string; favIconUrl?: string; active: boolean }) => void) => void };
  };
  windows: {
    getCurrent: () => Promise<{ id: number; type: string }>;
    get: (windowId: number) => Promise<{ id: number; type: string }>;
    onFocusChanged: { addListener: (cb: (windowId: number) => void) => void };
    WINDOW_ID_NONE: number;
  };
  webNavigation: {
    onHistoryStateUpdated: { addListener: (cb: (details: { tabId: number; frameId: number; url: string }) => void) => void } | null;
  } | null;
  action: {
    setIcon: (details: { path: Record<string, string> }) => Promise<void>;
  };
  alarms: {
    create: (name: string, alarmInfo: { periodInMinutes?: number }) => void;
    onAlarm: { addListener: (cb: (alarm: { name: string }) => void) => void };
  };
  runtime: {
    onSuspend: { addListener: (cb: () => void) => void };
  };
};

export const SafariBrowser: BrowserAPI = {
  storage: {
    get: (keys) => browser.storage.local.get(keys || null),
    set: (items) => browser.storage.local.set(items),
  },

  tabs: {
    get: (tabId) => browser.tabs.get(tabId).then((tab) => ({
      id: tab.id,
      windowId: tab.windowId,
      url: tab.url,
      title: tab.title,
      favIconUrl: tab.favIconUrl,
      active: tab.active,
    })),
    query: (queryInfo) => browser.tabs.query(queryInfo).then((tabs) =>
      tabs.map((tab) => ({
        id: tab.id,
        windowId: tab.windowId,
        url: tab.url,
        title: tab.title,
        favIconUrl: tab.favIconUrl,
        active: tab.active,
      }))
    ),
    onActivated: {
      addListener: (cb) => browser.tabs.onActivated.addListener(cb),
    },
    onUpdated: {
      addListener: (cb) => browser.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
        cb(tabId, changeInfo, {
          id: tab.id,
          windowId: tab.windowId,
          url: tab.url,
          title: tab.title,
          favIconUrl: tab.favIconUrl,
          active: tab.active,
        });
      }),
    },
  },

  windows: {
    getCurrent: () => browser.windows.getCurrent(),
    get: (windowId) => browser.windows.get(windowId),
    onFocusChanged: {
      addListener: (cb) => browser.windows.onFocusChanged.addListener(cb),
    },
    WINDOW_ID_NONE: browser.windows.WINDOW_ID_NONE,
  },

  webNavigation: {
    onHistoryStateUpdated: null,
  },

  action: {
    setIcon: (details) => browser.action.setIcon(details),
  },

  alarms: {
    create: (name, alarmInfo) => browser.alarms.create(name, alarmInfo),
    onAlarm: {
      addListener: (cb) => browser.alarms.onAlarm.addListener(cb),
    },
  },

  runtime: {
    onSuspend: {
      addListener: (cb) => browser.runtime.onSuspend.addListener(cb),
    },
  },
};