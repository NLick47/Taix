let lastVisibilityState = document.visibilityState;
let hasSentBeforeUnload = false;

declare const browser: {
  runtime?: {
    sendMessage: (msg: object) => Promise<void>;
  };
};

function getRuntime(): { sendMessage: (msg: unknown) => void } | null {
  if (typeof chrome !== 'undefined' && chrome.runtime?.sendMessage) {
    return {
      sendMessage: (msg) => {
        try {
          chrome.runtime.sendMessage(msg as object);
        } catch {
          // ignore
        }
      },
    };
  }
  if (typeof browser !== 'undefined' && browser.runtime !== undefined && browser.runtime.sendMessage !== undefined) {
    return {
      sendMessage: (msg) => {
        try {
          browser.runtime!.sendMessage!(msg as object);
        } catch {
          // ignore
        }
      },
    };
  }
  return null;
}

function notifyBackground(state: 'visible' | 'hidden'): void {
  const runtime = getRuntime();
  if (!runtime) return;

  runtime.sendMessage({ type: 'visibility', state, url: window.location.href });
}

document.addEventListener('visibilitychange', () => {
  const currentState = document.visibilityState;
  if (currentState !== lastVisibilityState) {
    lastVisibilityState = currentState;
    if (currentState === 'hidden') {
      hasSentBeforeUnload = false;
    }
    notifyBackground(currentState as 'visible' | 'hidden');
  }
});

window.addEventListener('pagehide', () => {
  if (!hasSentBeforeUnload && document.visibilityState === 'visible') {
    hasSentBeforeUnload = true;
    notifyBackground('hidden');
  }
});