import { BrowseData } from '../types';
import { CONFIG } from './config';

export class Queue {
  private items: BrowseData[] = [];
  private max: number = CONFIG.MAX_FAIL_QUEUE_SIZE;

  add(data: BrowseData): void {
    const last = this.items[this.items.length - 1];
    // 合并相邻时段：同 URL、时间连续或紧挨（gap 在 0-60s）
    if (last && last.Url === data.Url) {
      const lastEndTime = last.ActiveTime + last.Duration;
      const gap = data.ActiveTime - lastEndTime;
      // 只允许正向时间连续，gap >= 0 且 <= 60s
      if (gap >= 0 && gap <= 60) {
        // 合并时段：扩展 Duration，ActiveTime 保持最早
        last.Duration += data.Duration + gap;
        return;
      }
    }

    this.items.push({ ...data });

    if (this.items.length > this.max) {
      const dropped = this.items.shift();
      if (dropped) {
        console.warn('[Taix] 队列溢出，丢弃最早数据:', dropped);
      }
    }
  }

  peek(): BrowseData | null {
    return this.items[0] || null;
  }

  remove(): BrowseData | null {
    return this.items.shift() || null;
  }

  length(): number {
    return this.items.length;
  }

  isEmpty(): boolean {
    return this.items.length === 0;
  }

  getAll(): BrowseData[] {
    return [...this.items];
  }

  setAll(items: BrowseData[]): void {
    this.items = [...items];
  }
}