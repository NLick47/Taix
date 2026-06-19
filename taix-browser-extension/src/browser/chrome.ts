import { BrowserAPI } from './interface';
import { TabInfo, SavedState } from '../types';

export const ChromeBrowser: BrowserAPI = {
  storage: {
    get: (keys) => chrome.storage.local.get(keys || null) as Promise<SavedState>,
    set: (items) => chrome.storage.local.set(items) as Promise<void>,
  },

  tabs: {
    get: (tabId) => new Promise((resolve, reject) => {
      chrome.tabs.get(tabId, (tab) => {
        if (chrome.runtime.lastError) reject(chrome.runtime.lastError);
        else resolve({
          id: tab!.id!,
          windowId: tab!.windowId,
          url: tab!.url,
          title: tab!.title,
          favIconUrl: tab!.favIconUrl,
          active: tab!.active,
        });
      });
    }),
    query: (queryInfo) => new Promise((resolve) => {
      chrome.tabs.query(queryInfo, (tabs) => {
        resolve(tabs.map((tab) => ({
          id: tab.id!,
          windowId: tab.windowId,
          url: tab.url,
          title: tab.title,
          favIconUrl: tab.favIconUrl,
          active: tab.active,
        })));
      });
    }),
    onActivated: {
      addListener: (cb) => chrome.tabs.onActivated.addListener(cb),
    },
    onUpdated: {
      addListener: (cb) => chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => {
        cb(tabId, changeInfo, {
          id: tab.id!,
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
    getCurrent: () => new Promise((resolve) => {
      chrome.windows.getCurrent((win) => {
        resolve({ id: win!.id!, type: win!.type! });
      });
    }),
    get: (windowId) => new Promise((resolve) => {
      chrome.windows.get(windowId, (win) => {
        resolve({ id: win!.id!, type: win!.type! });
      });
    }),
    getLastFocused: () => new Promise((resolve) => {
      chrome.windows.getLastFocused((win) => {
        resolve({ id: win!.id!, type: win!.type!, focused: !!win!.focused });
      });
    }),
    onFocusChanged: {
      addListener: (cb) => chrome.windows.onFocusChanged.addListener(cb),
    },
    WINDOW_ID_NONE: chrome.windows.WINDOW_ID_NONE,
  },

  webNavigation: {
    onHistoryStateUpdated: chrome.webNavigation?.onHistoryStateUpdated
      ? {
          addListener: (cb) => chrome.webNavigation!.onHistoryStateUpdated!.addListener((details) => {
            cb({ tabId: details.tabId, frameId: details.frameId, url: details.url });
          }),
        }
      : null,
  },

  action: {
    setIcon: (details) => new Promise((resolve, reject) => {
      chrome.action.setIcon(details, () => {
        if (chrome.runtime.lastError) reject(chrome.runtime.lastError);
        else resolve();
      });
    }),
  },

  alarms: {
    create: (name, alarmInfo) => chrome.alarms.create(name, alarmInfo),
    onAlarm: {
      addListener: (cb) => chrome.alarms.onAlarm.addListener(cb),
    },
  },

  idle: {
    setDetectionInterval: chrome.idle?.setDetectionInterval
      ? (s) => chrome.idle.setDetectionInterval(s)
      : undefined,
    queryState: chrome.idle?.queryState
      ? (s) => new Promise((resolve) => chrome.idle.queryState(s, (state) => resolve(state)))
      : undefined,
    onStateChanged: chrome.idle?.onStateChanged
      ? {
          addListener: (cb) => chrome.idle.onStateChanged.addListener((state) => cb(state as 'active' | 'idle' | 'locked')),
        }
      : null,
  },

  runtime: {
    onSuspend: {
      addListener: (cb) => chrome.runtime.onSuspend.addListener(cb),
    },
  },
};