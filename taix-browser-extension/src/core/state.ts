import { BrowserAPI } from '../browser/interface';
import { SavedState, BrowseData, ActivePage } from '../types';

export class State {
  private browser: BrowserAPI;

  activePage: ActivePage | null = null;
  isSleep: boolean = false;
  reconnectFail: number = 0;
  notifyFailList: BrowseData[] = [];

  // save 合并到 microtask，同一个 handler 内多次调用合并为 1 次 storage.set
  private pendingWrite: boolean = false;
  private flushPromise: Promise<void> | null = null;

  constructor(browser: BrowserAPI) {
    this.browser = browser;
  }

  async load(): Promise<void> {
    try {
      const data = await this.browser.storage.get(['activePage', 'isSleep', 'reconnectFail', 'notifyFailList']);
      if (data.activePage) {
        // startTime 跨 SW 生命周期没意义，拉回 now 避免冻结/休眠后灌入巨值
        this.activePage = { ...data.activePage, startTime: Date.now() };
      }
      if (typeof data.isSleep === 'boolean') this.isSleep = data.isSleep;
      if (typeof data.reconnectFail === 'number') this.reconnectFail = data.reconnectFail;
      if (Array.isArray(data.notifyFailList)) this.notifyFailList = data.notifyFailList;
    } catch {
      // ignore
    }
  }

  save(): Promise<void> {
    this.pendingWrite = true;
    if (!this.flushPromise) {
      // microtask 而非 setTimeout，同步代码段结束就 flush，延迟最低
      this.flushPromise = Promise.resolve().then(() => this.flush());
    }
    return this.flushPromise;
  }

  private async flush(): Promise<void> {
    // 循环到 pending 清空，写期间被再次标记的变更不会丢
    while (this.pendingWrite) {
      this.pendingWrite = false;
      const snapshot = {
        activePage: this.activePage,
        isSleep: this.isSleep,
        reconnectFail: this.reconnectFail,
        notifyFailList: this.notifyFailList,
      };
      try {
        await this.browser.storage.set(snapshot);
      } catch {
        // ignore
      }
    }
    this.flushPromise = null;
  }
}
