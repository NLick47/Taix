import { BrowserAPI } from './interface';
import { ChromeBrowser } from './chrome';
import { FirefoxBrowser } from './firefox';
import { SafariBrowser } from './safari';
import { BrowserType } from '../types';

export { ChromeBrowser } from './chrome';
export { FirefoxBrowser } from './firefox';
export { SafariBrowser } from './safari';

export function detectBrowser(): BrowserType {
  const ua = navigator.userAgent;

  if (ua.includes('Safari') && !ua.includes('Chrome') && !ua.includes('Chromium')) {
    return 'safari';
  }

  if (typeof browser !== 'undefined' && browser.runtime) {
    return 'firefox';
  }

  if (typeof chrome !== 'undefined' && chrome.runtime) {
    return 'chrome';
  }

  return 'chrome';
}

export function getBrowserAPI(): BrowserAPI {
  const type = detectBrowser();
  switch (type) {
    case 'firefox':
      return FirefoxBrowser;
    case 'safari':
      return SafariBrowser;
    default:
      return ChromeBrowser;
  }
}

export const browser = getBrowserAPI();