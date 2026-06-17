import { browser } from './browser';
import { Tracker } from './core/tracker';

const tracker = new Tracker(browser);

(async function init() {
  browser.alarms.onAlarm.addListener((alarm) => {
    tracker.handleAlarm(alarm.name);
  });
  await tracker.start();
})();