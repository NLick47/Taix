import { BrowseData } from '../types';
import { CONFIG } from './config';

export class Queue {
  private items: BrowseData[] = [];
  private max: number = CONFIG.MAX_FAIL_QUEUE_SIZE;

  add(data: BrowseData): void {
    const last = this.items[this.items.length - 1];
    if (last && last.Url === data.Url) {
      const gap = data.ActiveTime - (last.ActiveTime + last.Duration);
      if (gap >= -5 && gap <= 60) {
        last.Duration += data.Duration;
        if (data.ActiveTime < last.ActiveTime) {
          last.ActiveTime = data.ActiveTime;
        }
        return;
      }
    }

    this.items.push({ ...data });

    if (this.items.length > this.max) {
      this.items.shift();
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