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
  private lastSavedTime: number = 0;

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
    await this.state.load();
    this.queue.setAll(this.state.notifyFailList);

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

    this.setupMessageListener();
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

  private saveActivePageDuration(): void {
    if (!this.state.activePage?.url) return;

    const now = Date.now();
    if (now - this.lastSavedTime < 1000) return;

    const duration = Math.floor((now - this.state.activePage.startTime) / 1000);
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
    this.state.save();
  }

  private handleWake(): void {
    this.state.isSleep = false;
    this.validateActivePage();
    this.state.save();
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

    const now = Date.now();
    const duration = Math.floor((now - this.state.activePage.startTime) / 1000);
    if (duration <= 0) return;

    this.lastSavedTime = now;
    const activeTime = Math.floor(this.state.activePage.startTime / 1000);

    this.notifyTai({
      Url: this.state.activePage.url,
      Title: this.state.activePage.title,
      Icon: this.state.activePage.icon,
      Duration: duration,
      ActiveTime: activeTime,
    });

    this.state.activePage.startTime = now;
    this.state.save();
  }

  private doRenotify(): void {
    this.flushQueue();
  }

  private async handleWindowFocusChanged(windowId: number): Promise<void> {
    if (windowId === this.browser.windows.WINDOW_ID_NONE) {
      if (this.currentWindowId !== -1) {
        this.currentWindowId = -1;
        this.currentTabId = -1;
        this.isPageVisible = true;
        this.saveActivePage();
      }
    } else {
      try {
        const win = await this.browser.windows.get(windowId);
        if (win && (win.type === 'normal' || win.type === 'popup')) {
          this.currentWindowId = windowId;
          this.isPageVisible = true;
          this.validateActivePage();
        }
      } catch {
        // ignore
      }
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

    const duration = Math.floor((Date.now() - this.state.activePage.startTime) / 1000);
    if (duration <= 0) return;

    const activeTime = Math.floor(this.state.activePage.startTime / 1000);
    const { url: Url, title: Title, icon: Icon } = this.state.activePage;

    this.state.activePage = null;
    this.lastSavedTime = 0;
    this.state.save();

    this.notifyTai({ Url, Title, Icon, Duration: duration, ActiveTime: activeTime });
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
    const path = state === 'active'
      ? { 16: 'icons/socket-active.svg', 48: 'icons/socket-active.svg', 128: 'icons/socket-active.svg' }
      : { 16: 'icons/socket-inactive.svg', 48: 'icons/socket-inactive.svg', 128: 'icons/socket-inactive.svg' };
    this.browser.action.setIcon({ path }).catch(() => {});
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