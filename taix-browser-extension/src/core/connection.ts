import { CONFIG } from './config';

export type ConnectionStatus = 'connected' | 'disconnected' | 'sleep';

export interface ConnectionCallbacks {
  onConnected: () => void;
  onDisconnected: () => void;
  onSleep: () => void;
  onWake: () => void;
  onMessage: (data: string) => void;
}

export class Connection {
  private ws: WebSocket | null = null;
  private isConnected: boolean = false;
  private isSleep: boolean = false;
  private callbacks: ConnectionCallbacks;

  constructor(callbacks: ConnectionCallbacks) {
    this.callbacks = callbacks;
  }

  connect(): void {
    // 先清理旧连接，防止旧 WebSocket 的回调干扰新连接
    if (this.ws) {
      try {
        this.ws.onopen = null;
        this.ws.onmessage = null;
        this.ws.onclose = null;
        this.ws.onerror = null;
        this.ws.close();
      } catch {
        // ignore
      }
      this.ws = null;
    }

    // 重置状态
    this.isConnected = false;
    this.isSleep = false;

    this.ws = new WebSocket(CONFIG.WS_URL);

    this.ws.onopen = () => {
      this.isConnected = true;
      console.log('[Taix] WebSocket 已连接');
      this.callbacks.onConnected();
    };

    this.ws.onmessage = (event) => {
      if (event.data === 'sleep') {
        this.isSleep = true;
        console.log('[Taix] 进入睡眠模式');
        this.callbacks.onSleep();
      } else if (event.data === 'wake') {
        this.isSleep = false;
        console.log('[Taix] 唤醒');
        this.callbacks.onWake();
      } else {
        this.callbacks.onMessage(event.data);
      }
    };

    this.ws.onclose = () => {
      const wasConnected = this.isConnected;
      this.isConnected = false;
      if (wasConnected) {
        console.warn('[Taix] WebSocket 连接断开');
        this.callbacks.onDisconnected();
      }
      this.ws = null;
    };

    this.ws.onerror = () => {
      console.warn('[Taix] WebSocket 连接错误');
    };
  }

  disconnect(): void {
    if (this.ws) {
      try {
        this.ws.onopen = null;
        this.ws.onmessage = null;
        this.ws.onclose = null;
        this.ws.onerror = null;
        this.ws.close();
      } catch {
        // ignore
      }
      this.ws = null;
    }
    this.isConnected = false;
  }

  send(data: string): boolean {
    if (!this.isConnected || !this.ws || this.ws.readyState !== WebSocket.OPEN) {
      return false;
    }
    try {
      this.ws.send(data);
      return true;
    } catch {
      return false;
    }
  }

  getStatus(): ConnectionStatus {
    if (this.isSleep) return 'sleep';
    if (this.isConnected) return 'connected';
    return 'disconnected';
  }

  isSleeping(): boolean {
    return this.isSleep;
  }

  setSleep(value: boolean): void {
    this.isSleep = value;
  }
}