import { BrowserAPI } from '../browser/interface';
import { ActivePage, BrowseData, TabInfo } from '../types';
import { CONFIG } from './config';
import { Connection } from './connection';
import { Queue } from './queue';
import { State } from './state';

declare const browser: {
  runtime?: {
    onMessage?: {
      addListener: (listener: (msg: unknown, sender: { tab?: { id?: number } }) => boolean) => void;
    };
  };
};

const INTERNAL_PREFIXES = [
  'chrome://', 'chrome-extension://', 'about:',
  'edge://', 'edge-extension://', 'firefox://',
  'file://', 'data:', 'javascript:', 'blob:',
  'brave://', 'opera://', 'vivaldi://', 'safari://',
  'safari-extension://', 'safari-web-extension://',
];

const IGNORE_TITLES = [
  '新标签页', '新标签', 'new tab', 'newtab',
  'about:blank', '空白页', 'blank page',
];

export class Tracker {
  private browser: BrowserAPI;
  private connection: Connection;
  private state: State;
  private queue: Queue;
  private currentWindowId: number = -1;
  private currentTabId: number = -1;
  private urlChangeDebounceTimer: ReturnType<typeof setTimeout> | null = null;
  private isPageVisible: boolean = true;
  private isWindowFocused: boolean = true;
  private isUserIdle: boolean = false;
  private idleEventReceived: boolean = false;
  private lastSavedTime: number = 0;
  private started: boolean = false;

  constructor(browser: BrowserAPI) {
    this.browser = browser;
    this.state = new State(browser);
    this.queue = new Queue();
    this.connection = new Connection({
      onConnected: () => this.handleConnected(),
      onDisconnected: () => this.handleDisconnected(),
      onSleep: () => this.handleSleep(),
      onWake: () => this.handleWake(),
      onMessage: () => {},
    });
  }

  async start(): Promise<void> {
    if (this.started) return;
    this.started = true;

    await this.state.load();
    this.queue.setAll(this.state.notifyFailList);

    // SW 重启后非持久化态重置，等首次事件自纠
    // isWindowFocused/isPageVisible 默认 true，idle 兜底防误算
    this.isPageVisible = true;
    this.isWindowFocused = true;
    this.isUserIdle = false;
    this.idleEventReceived = false;

    try {
      const win = await this.browser.windows.getCurrent();
      if (win) this.currentWindowId = win.id;
    } catch {
      // ignore
    }

    this.connection.connect();

    this.browser.alarms.create('heartbeat', { periodInMinutes: CONFIG.HEARTBEAT_INTERVAL_MIN });
    this.browser.alarms.create('save', { periodInMinutes: CONFIG.SAVE_INTERVAL_MIN });
    this.browser.alarms.create('renotify', { periodInMinutes: CONFIG.RENOTIFY_INTERVAL_MIN });

    this.browser.tabs.onActivated.addListener((info) => this.handleTabActivated(info));
    this.browser.tabs.onUpdated.addListener((tabId, changeInfo, tab) => this.handleTabUpdated(tabId, changeInfo, tab));
    this.browser.windows.onFocusChanged.addListener((windowId) => this.handleWindowFocusChanged(windowId));

    if (this.browser.webNavigation?.onHistoryStateUpdated) {
      this.browser.webNavigation.onHistoryStateUpdated.addListener((details) => this.handleHistoryStateUpdated(details));
    }

    this.browser.runtime.onSuspend.addListener(() => this.handleSuspend());

    this.setupIdleListener();
    this.setupMessageListener();

    // SW 冷启动时主动探测一次焦点和 idle，否则没事件进来 doPeriodicSave 会持续累加
    await this.probeInitialState();
  }

  private async probeInitialState(): Promise<void> {
    // queryState 不会触发 onStateChanged，要主动取一次
    // await 期间若已收到真实事件，idleEventReceived 守卫防 stale 结果覆盖
    if (this.browser.idle?.queryState) {
      try {
        const idleState = await this.browser.idle.queryState(CONFIG.IDLE_DETECTION_SECS);
        if (!this.idleEventReceived) {
          this.isUserIdle = idleState !== 'active';
        }
      } catch {
        // ignore
      }
    }
  }

  private setupIdleListener(): void {
    if (!this.browser.idle?.onStateChanged) return;

    if (this.browser.idle.setDetectionInterval) {
      try {
        this.browser.idle.setDetectionInterval(CONFIG.IDLE_DETECTION_SECS);
      } catch {
        // ignore
      }
    }

    this.browser.idle.onStateChanged.addListener((state) => {
      this.idleEventReceived = true;
      this.handleIdleStateChanged(state);
    });
  }

  private handleIdleStateChanged(state: 'active' | 'idle' | 'locked'): void {
    if (state === 'active') {
      if (this.isUserIdle) {
        this.isUserIdle = false;
        // 空闲恢复，离开期间不算时长，startTime 拉回 now 重新计
        if (this.state.activePage?.url) {
          this.state.activePage.startTime = Date.now();
          this.lastSavedTime = 0;
          this.state.save();
        }
      }
    } else {
      // idle / locked
      if (!this.isUserIdle) {
        // 进入空闲前先把累计时长落库
        this.saveActivePageDuration();
        this.isUserIdle = true;
      }
    }
  }

  private setupMessageListener(): void {
    const addMessageListener = (listener: (message: { type: string; state?: string; url?: string }, sender: { tab?: { id?: number } }) => void) => {
      if (typeof chrome !== 'undefined' && chrome.runtime?.onMessage) {
        chrome.runtime.onMessage.addListener((msg, sender, sendResponse) => {
          listener(msg as { type: string; state?: string; url?: string }, { tab: sender.tab ? { id: sender.tab.id } : undefined });
          return false;
        });
      } else if (typeof browser !== 'undefined' && browser.runtime?.onMessage) {
        browser.runtime.onMessage.addListener((msg: unknown, sender: { tab?: { id?: number } }) => {
          listener(msg as { type: string; state?: string; url?: string }, sender);
          return false;
        });
      }
    };

    addMessageListener((message, sender) => {
      if (message.type === 'visibility' && message.state) {
        this.handleVisibilityChange(sender.tab?.id, message.state as 'visible' | 'hidden', message.url);
      }
    });
  }

  private handleVisibilityChange(tabId: number | undefined, state: 'visible' | 'hidden', url: string | undefined): void {
    if (tabId === undefined || tabId !== this.currentTabId) return;
    if (!url || !this.state.activePage?.url || !this.urlsMatch(url, this.state.activePage.url)) return;

    if (state === 'hidden' && this.isPageVisible) {
      this.isPageVisible = false;
      this.saveActivePageDuration();
    } else if (state === 'visible' && !this.isPageVisible) {
      this.isPageVisible = true;
      this.resumeActivePage();
    }
  }

  private urlsMatch(url1: string, url2: string): boolean {
    try {
      const u1 = new URL(url1);
      const u2 = new URL(url2);
      return u1.href === u2.href;
    } catch {
      return url1 === url2;
    }
  }

  /** 计算 startTime 到 now 的可信时长，超过 MAX_SAVE_STEP 视为休眠/冻结，整段丢弃 */
  private computeTrustedDuration(startTime: number, now: number): number {
    const raw = Math.floor((now - startTime) / 1000);
    if (raw <= 0) return 0;
    if (raw > CONFIG.MAX_SAVE_STEP_SECS) {
      console.warn('[Taix] 时间跨度异常，丢弃本段', {
        rawSecs: raw,
        cap: CONFIG.MAX_SAVE_STEP_SECS,
      });
      return 0;
    }
    return raw;
  }

  private saveActivePageDuration(): void {
    if (!this.state.activePage?.url) return;

    const now = Date.now();
    // 1s 节流，防 visibility/idle/focus 连环触发
    // 节流时 return 不前推 startTime，未上报时长留到下次一并算
    if (now - this.lastSavedTime < 1000) return;

    const duration = this.computeTrustedDuration(this.state.activePage.startTime, now);
    if (duration > 0) {
      this.lastSavedTime = now;
      const activeTime = Math.floor(this.state.activePage.startTime / 1000);
      this.notifyTai({
        Url: this.state.activePage.url,
        Title: this.state.activePage.title,
        Icon: this.state.activePage.icon,
        Duration: duration,
        ActiveTime: activeTime,
      });
    }

    this.state.activePage.startTime = now;
    this.state.save();
  }

  private resumeActivePage(): void {
    if (this.state.activePage?.url) {
      this.state.activePage.startTime = Date.now();
      this.state.save();
    }
  }

  private handleConnected(): void {
    this.state.reconnectFail = 0;
    this.state.isSleep = false;
    this.setIcon('active');
    this.flushQueue();
  }

  private flushQueue(): void {
    while (!this.queue.isEmpty() && this.connection.getStatus() === 'connected') {
      const item = this.queue.peek();
      if (!item) break;
      const sent = this.connection.send(JSON.stringify(item));
      if (sent) {
        this.queue.remove();
      } else {
        break;
      }
    }
    this.state.notifyFailList = this.queue.getAll();
    this.state.save();
  }

  private handleDisconnected(): void {
    this.setIcon('inactive');
  }

  private handleSleep(): void {
    this.state.isSleep = true;
    this.saveActivePage();
  }

  private handleWake(): void {
    this.state.isSleep = false;
    this.state.save();
    this.validateActivePage();
  }

  private handleSuspend(): void {
    this.saveActivePage();
    this.connection.disconnect();
  }

  handleAlarm(name: string): Promise<void> {
    switch (name) {
      case 'heartbeat':
        this.doHeartbeat();
        break;
      case 'save':
        this.doPeriodicSave();
        break;
      case 'renotify':
        this.doRenotify();
        break;
    }
    return Promise.resolve();
  }

  private doHeartbeat(): void {
    const status = this.connection.getStatus();
    if (status !== 'connected' && !this.state.isSleep) {
      this.state.reconnectFail++;
      this.connection.connect();
      if (this.state.reconnectFail >= CONFIG.RECONNECT_FAIL_SLEEP && !this.state.isSleep) {
        this.state.isSleep = true;
        this.state.save();
      }
      return;
    }

    this.connection.send('ping');
  }

  private doPeriodicSave(): void {
    if (!this.state.activePage?.url) return;
    if (!this.isPageVisible) return;
    if (!this.isWindowFocused) return;
    if (this.isUserIdle) return;

    const now = Date.now();
    const startTime = this.state.activePage.startTime;
    const duration = this.computeTrustedDuration(startTime, now);
    // 始终前推 startTime，本段被丢弃也别让下一轮再算同样的巨值
    this.state.activePage.startTime = now;
    this.lastSavedTime = now;

    if (duration <= 0) {
      this.state.save();
      return;
    }

    this.notifyTai({
      Url: this.state.activePage.url,
      Title: this.state.activePage.title,
      Icon: this.state.activePage.icon,
      Duration: duration,
      ActiveTime: Math.floor(startTime / 1000),
    });

    this.state.save();
  }

  private doRenotify(): void {
    this.flushQueue();
  }

  private async handleWindowFocusChanged(windowId: number): Promise<void> {
    if (windowId === this.browser.windows.WINDOW_ID_NONE) {
      // 浏览器整体失焦，停止计时并把累计时长落库
      // currentWindowId 不重置，回切时 onFocusChanged 能识别同一窗口
      if (this.isWindowFocused) {
        this.isWindowFocused = false;
        this.saveActivePageDuration();
      }
      return;
    }

    try {
      const win = await this.browser.windows.get(windowId);
      if (!win || (win.type !== 'normal' && win.type !== 'popup')) return;

      const isSwitchingWindow = this.currentWindowId !== windowId;
      const isResumingFocus = !this.isWindowFocused && !isSwitchingWindow;

      if (isSwitchingWindow) {
        // 切到另一个浏览器窗口，先把旧窗口当前页累计时长落库再切
        this.saveActivePage();
      } else if (isResumingFocus) {
        // 失焦后聚焦回同一窗口，离开期间不计，startTime 拉回 now
        if (this.state.activePage?.url) {
          this.state.activePage.startTime = Date.now();
          this.lastSavedTime = 0;
          this.state.save();
        }
      }

      this.currentWindowId = windowId;
      this.isWindowFocused = true;
      // 新窗口 active tab 默认可见，避免上一窗口遗留的 false 卡住计时
      this.isPageVisible = true;
      this.validateActivePage();
    } catch {
      // ignore
    }
  }

  private async handleTabActivated(info: { tabId: number; windowId: number }): Promise<void> {
    if (info.windowId !== this.currentWindowId) return;

    this.isPageVisible = true;

    try {
      const tab = await this.browser.tabs.get(info.tabId);
      if (tab) {
        this.currentTabId = tab.id;
        this.onActivePage(tab);
      }
    } catch {
      // ignore
    }
  }

  private handleTabUpdated(_tabId: number, changeInfo: { status?: string; url?: string }, tab: TabInfo): void {
    if (tab.windowId !== this.currentWindowId) return;
    if (!tab.active) return;

    if (changeInfo.status === 'loading' && changeInfo.url) {
      this.saveActivePage();
      this.setActive(tab);
    } else if (changeInfo.status === 'complete') {
      this.onActivePage(tab);
    }
  }

  private handleHistoryStateUpdated(details: { tabId: number; frameId: number; url: string }): void {
    if (details.frameId !== 0) return;
    if (!details.url) return;

    const tabId = details.tabId;

    if (this.urlChangeDebounceTimer) clearTimeout(this.urlChangeDebounceTimer);
    this.urlChangeDebounceTimer = setTimeout(async () => {
      this.urlChangeDebounceTimer = null;
      try {
        const tab = await this.browser.tabs.get(tabId);
        if (!tab) return;
        if (tab.windowId !== this.currentWindowId) return;
        if (!tab.active) return;
        this.saveActivePage();
        this.onActivePage(tab);
      } catch {
        // ignore
      }
    }, CONFIG.URL_CHANGE_DEBOUNCE_MS);
  }

  private onActivePage(tab: TabInfo): void {
    if (this.state.isSleep) return;
    if (!tab?.url) return;

    this.currentTabId = tab.id;

    if (this.isInternalPage(tab.url, tab.title)) {
      if (this.state.activePage && !this.isInternalPage(this.state.activePage.url, this.state.activePage.title)) {
        this.saveActivePage();
      }
      return;
    }

    if (this.state.activePage?.url) {
      if (this.state.activePage.url !== tab.url) {
        this.saveActivePage();
        this.setActive(tab);
      } else {
        if (this.state.activePage.title !== (tab.title || '')) this.state.activePage.title = tab.title || '';
        if (this.state.activePage.icon !== (tab.favIconUrl || '')) this.state.activePage.icon = tab.favIconUrl || '';
        this.state.save();
      }
    } else {
      this.setActive(tab);
    }
  }

  private setActive(tab: TabInfo): void {
    if (!tab?.url) {
      this.state.activePage = null;
      this.state.save();
      return;
    }

    this.state.activePage = {
      url: tab.url,
      title: tab.title || '',
      icon: tab.favIconUrl || '',
      startTime: Date.now(),
    };

    this.isPageVisible = true;
    this.lastSavedTime = 0;
    this.state.save();
  }

  private saveActivePage(): void {
    if (!this.state.activePage?.url) return;

    const now = Date.now();
    const startTime = this.state.activePage.startTime;
    const duration = this.computeTrustedDuration(startTime, now);

    const { url: Url, title: Title, icon: Icon } = this.state.activePage;
    this.state.activePage = null;
    this.lastSavedTime = 0;
    this.state.save();

    if (duration <= 0) return;

    this.notifyTai({ Url, Title, Icon, Duration: duration, ActiveTime: Math.floor(startTime / 1000) });
  }

  private notifyTai(data: BrowseData): void {
    console.log('[Taix] 发送浏览数据:', data);
    const sent = this.connection.send(JSON.stringify(data));
    if (!sent) {
      console.warn('[Taix] 发送失败，加入队列');
      this.queue.add(data);
      this.state.notifyFailList = this.queue.getAll();
      this.state.save();
    }
  }

  private async validateActivePage(): Promise<void> {
    try {
      const tabs = await this.browser.tabs.query({ active: true, currentWindow: true });
      const tab = tabs[0];
      if (!tab) return;

      this.currentTabId = tab.id;

      if (this.state.activePage && this.state.activePage.url !== tab.url) {
        this.saveActivePage();
      }

      if (!this.state.activePage) {
        this.onActivePage(tab);
      }
    } catch {
      // ignore
    }
  }

  private setIcon(state: 'active' | 'inactive'): void {
    const file = state === 'active' ? 'icons/socket-active.png' : 'icons/socket-inactive.png';
    this.browser.action.setIcon({ path: { 16: file, 48: file, 128: file } }).catch(() => {});
  }

  private isInternalPage(url: string, title?: string): boolean {
    if (!url) return true;
    const lower = url.toLowerCase();

    if (INTERNAL_PREFIXES.some((p) => lower.startsWith(p))) return true;

    if (title) {
      const t = title.trim().toLowerCase();
      if (IGNORE_TITLES.includes(t)) return true;
    }

    return false;
  }
}
