'use strict';

const CONFIG = {
  WS_URL: 'ws://127.0.0.1:8908/TaiWebSentry',
  HEARTBEAT_INTERVAL_MIN: 0.5,
  SAVE_INTERVAL_MIN: 0.5,
  RENOTIFY_INTERVAL_MIN: 0.5,
  RECONNECT_FAIL_SLEEP: 5,
  URL_CHANGE_DEBOUNCE_MS: 300,
  MAX_FAIL_QUEUE_SIZE: 100,
};

let ws = null;
let isConnected = false;
let isSleep = false;
let reconnectFail = 0;
let currentWindowId = -1;
let activePage = null;
let notifyFailList = [];
let urlChangeDebounceTimer = null;

(async function init() {
  await restoreState();

  const win = await chrome.windows.getCurrent().catch(() => null);
  if (win) currentWindowId = win.id;

  connect();

  chrome.alarms.create('heartbeat', { periodInMinutes: CONFIG.HEARTBEAT_INTERVAL_MIN });
  chrome.alarms.create('save', { periodInMinutes: CONFIG.SAVE_INTERVAL_MIN });
  chrome.alarms.create('renotify', { periodInMinutes: CONFIG.RENOTIFY_INTERVAL_MIN });
})();

chrome.alarms.onAlarm.addListener(handleAlarm);
chrome.windows.onFocusChanged.addListener(handleWindowFocusChanged);
chrome.tabs.onActivated.addListener((info) => handleTabActivated(info).catch(() => {}));
chrome.tabs.onUpdated.addListener((tabId, changeInfo, tab) => handleTabUpdated(tabId, changeInfo, tab).catch(() => {}));

if (chrome.webNavigation?.onHistoryStateUpdated) {
  chrome.webNavigation.onHistoryStateUpdated.addListener((details) => handleHistoryStateUpdated(details).catch(() => {}));
}

chrome.runtime.onSuspend.addListener(handleSuspend);

function handleAlarm(alarm) {
  switch (alarm.name) {
    case 'heartbeat':
      doHeartbeat();
      break;
    case 'save':
      doPeriodicSave();
      break;
    case 'renotify':
      doRenotify();
      break;
  }
}

function connect() {
  if (ws) {
    try { ws.close(); } catch (_) {}
    ws = null;
  }

  ws = new WebSocket(CONFIG.WS_URL);

  ws.onopen = () => {
    isConnected = true;
    reconnectFail = 0;
    setIcon('active');
    console.log('[Tai] 已连接');
  };

  ws.onmessage = (event) => {
    if (event.data === 'sleep') {
      isSleep = true;
      saveActivePage();
      saveState();
      console.log('[Tai] 睡眠');
    } else if (event.data === 'wake') {
      isSleep = false;
      validateActivePage();
      saveState();
      console.log('[Tai] 唤醒');
    }
  };

  ws.onclose = () => {
    isConnected = false;
    setIcon('inactive');
    ws = null;
    console.warn('[Tai] 连接断开');
  };

  ws.onerror = (err) => {
    console.warn('[Tai] WebSocket 错误', err);
  };
}

function doHeartbeat() {
  if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) {
    if (!isConnected && !isSleep) {
      reconnectFail++;
      connect();
      if (reconnectFail >= CONFIG.RECONNECT_FAIL_SLEEP && !isSleep) {
        isSleep = true;
        saveState();
      }
    }
    return;
  }

  try {
    ws.send('ping');
  } catch (e) {
    console.warn('[Tai] 心跳发送失败', e);
    isConnected = false;
  }
}

function saveState() {
  return chrome.storage.local.set({
    activePage,
    isSleep,
    reconnectFail,
    notifyFailList,
  }).catch(() => {});
}

async function restoreState() {
  try {
    const data = await chrome.storage.local.get(['activePage', 'isSleep', 'reconnectFail', 'notifyFailList']);
    if (data.activePage) activePage = data.activePage;
    if (typeof data.isSleep === 'boolean') isSleep = data.isSleep;
    if (typeof data.reconnectFail === 'number') reconnectFail = data.reconnectFail;
    if (Array.isArray(data.notifyFailList)) notifyFailList = data.notifyFailList;
  } catch (_) {}
}

async function handleWindowFocusChanged(windowId) {
  if (windowId === chrome.windows.WINDOW_ID_NONE) {
    if (currentWindowId !== -1) {
      currentWindowId = -1;
      saveActivePage();
    }
  } else {
    try {
      const win = await chrome.windows.get(windowId);
      if (win.type === 'normal' || win.type === 'popup') {
        currentWindowId = windowId;
        validateActivePage();
      }
    } catch (_) {}
  }
}

async function handleTabActivated(activeInfo) {
  if (activeInfo.windowId !== currentWindowId) return;

  const tab = await chrome.tabs.get(activeInfo.tabId).catch(() => null);
  if (!tab) return;

  onActivePage(tab);
}

function handleTabUpdated(tabId, changeInfo, tab) {
  if (tab.windowId !== currentWindowId) return;
  if (!tab.active) return;

  if (changeInfo.url || changeInfo.status === 'complete') {
    onActivePage(tab);
  }
}

async function handleHistoryStateUpdated(details) {
  if (details.frameId !== 0) return;
  if (!details.url) return;

  const tab = await chrome.tabs.get(details.tabId).catch(() => null);
  if (!tab) return;
  if (tab.windowId !== currentWindowId) return;
  if (!tab.active) return;

  if (urlChangeDebounceTimer) clearTimeout(urlChangeDebounceTimer);
  urlChangeDebounceTimer = setTimeout(() => {
    urlChangeDebounceTimer = null;
    onActivePage(tab);
  }, CONFIG.URL_CHANGE_DEBOUNCE_MS);
}

function onActivePage(tab) {
  if (isSleep) return;
  if (!tab || !tab.url) return;

  if (isInternalPage(tab.url, tab.title)) {
    if (activePage && !isInternalPage(activePage.url, activePage.title)) {
      saveActivePage();
    }
    return;
  }

  if (activePage && activePage.url) {
    if (activePage.url !== tab.url) {
      saveActivePage();
      setActive(tab);
    } else {
      if (activePage.title !== (tab.title || '')) activePage.title = tab.title || '';
      if (activePage.icon !== (tab.favIconUrl || '')) activePage.icon = tab.favIconUrl || '';
      saveState();
    }
  } else {
    setActive(tab);
  }
}

function setActive(tab) {
  if (!tab || !tab.url) {
    activePage = null;
    saveState();
    return;
  }

  activePage = {
    url: tab.url,
    title: tab.title || '',
    icon: tab.favIconUrl || '',
    startTime: Date.now(),
  };

  saveState();
}

function saveActivePage() {
  if (!activePage || !activePage.url) return;

  const duration = Math.floor((Date.now() - activePage.startTime) / 1000);
  if (duration <= 0) return;

  const activeTime = Math.floor(activePage.startTime / 1000);
  const { url: Url, title: Title, icon: Icon } = activePage;

  activePage = null;
  saveState();

  notifyTai({ Url, Title, Icon, Duration: duration, ActiveTime: activeTime });
}

function doPeriodicSave() {
  if (!activePage || !activePage.url) return;

  const now = Date.now();
  const duration = Math.floor((now - activePage.startTime) / 1000);
  if (duration <= 0) return;

  const activeTime = Math.floor(activePage.startTime / 1000);

  notifyTai({
    Url: activePage.url,
    Title: activePage.title,
    Icon: activePage.icon,
    Duration: duration,
    ActiveTime: activeTime,
  });

  // 重置 startTime，不清空 activePage
  activePage.startTime = now;
  saveState();
}

function notifyTai(data) {
  console.log('[Tai] notify', data);

  if (isConnected && ws && ws.readyState === WebSocket.OPEN) {
    try {
      ws.send(JSON.stringify(data));
      return;
    } catch (e) {
      console.warn('发送失败', e);
    }
  }

  addToFailList(data);
}

function addToFailList(data) {
  const last = notifyFailList[notifyFailList.length - 1];
  if (last && last.Url === data.Url) {
    // 检查时间是否连续
    const gap = data.ActiveTime - (last.ActiveTime + last.Duration);
    if (gap >= -5 && gap <= 60) {
      last.Duration += data.Duration;
      if (data.ActiveTime < last.ActiveTime) {
        last.ActiveTime = data.ActiveTime;
      }
      saveState();
      return;
    }
  }

  notifyFailList.push({ ...data });

  if (notifyFailList.length > CONFIG.MAX_FAIL_QUEUE_SIZE) {
    notifyFailList.shift();
  }

  saveState();
}

function doRenotify() {
  if (!isConnected || !ws || ws.readyState !== WebSocket.OPEN) return;
  if (notifyFailList.length === 0) return;

  const item = notifyFailList[0];
  try {
    ws.send(JSON.stringify(item));
    notifyFailList.shift();
    saveState();
  } catch (e) {
    console.warn('重发失败', e);
  }
}

async function validateActivePage() {
  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    const tab = tabs[0];
    if (!tab) return;

    if (activePage && activePage.url !== tab.url) {
      saveActivePage();
    }

    if (!activePage) {
      onActivePage(tab);
    }
  } catch (_) {}
}

function handleSuspend() {
  saveActivePage();
  if (ws) {
    try { ws.close(); } catch (_) {}
  }
}


function setIcon(state) {
  const path = state === 'active'
    ? { 16: 'icons/socket-active.svg', 48: 'icons/socket-active.svg', 128: 'icons/socket-active.svg' }
    : { 16: 'icons/socket-inactive.svg', 48: 'icons/socket-inactive.svg', 128: 'icons/socket-inactive.svg' };
  chrome.action.setIcon({ path });
}

function isInternalPage(url, title) {
  if (!url) return true;
  const lower = url.toLowerCase();

  const prefixes = [
    'chrome://', 'chrome-extension://', 'about:',
    'edge://', 'edge-extension://', 'firefox://',
    'file://', 'data:', 'javascript:', 'blob:',
    'brave://', 'opera://', 'vivaldi://',
  ];

  if (prefixes.some(p => lower.startsWith(p))) return true;

  const ignoreTitles = [
    '新标签页', '新标签', 'new tab', 'newtab',
    'about:blank', '空白页', 'blank page',
  ];

  if (title) {
    const t = title.trim().toLowerCase();
    if (ignoreTitles.includes(t)) return true;
  }

  return false;
}
