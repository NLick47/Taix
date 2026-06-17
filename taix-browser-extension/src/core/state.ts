import { BrowserAPI } from '../browser/interface';
import { SavedState, BrowseData, ActivePage } from '../types';

export class State {
  private browser: BrowserAPI;

  activePage: ActivePage | null = null;
  isSleep: boolean = false;
  reconnectFail: number = 0;
  notifyFailList: BrowseData[] = [];

  constructor(browser: BrowserAPI) {
    this.browser = browser;
  }

  async load(): Promise<void> {
    try {
      const data = await this.browser.storage.get(['activePage', 'isSleep', 'reconnectFail', 'notifyFailList']);
      if (data.activePage) this.activePage = data.activePage;
      if (typeof data.isSleep === 'boolean') this.isSleep = data.isSleep;
      if (typeof data.reconnectFail === 'number') this.reconnectFail = data.reconnectFail;
      if (Array.isArray(data.notifyFailList)) this.notifyFailList = data.notifyFailList;
    } catch {
      // ignore
    }
  }

  async save(): Promise<void> {
    try {
      await this.browser.storage.set({
        activePage: this.activePage,
        isSleep: this.isSleep,
        reconnectFail: this.reconnectFail,
        notifyFailList: this.notifyFailList,
      });
    } catch {
      // ignore
    }
  }
}