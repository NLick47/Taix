// 与 taix-server 的 HTTP 契约对应（见 taix-server/src/routes）

export interface ApiResponse<T> {
  code: number;
  message: string;
  data?: T;
}

export interface ConfigField {
  key: string;
  label: string;
  type: 'string' | 'text' | 'number' | 'bool' | 'select' | 'json';
  min?: number;
  max?: number;
  options?: string[];
  default?: unknown;
}

export interface PluginListItem {
  id: string;
  name: string;
  version: string;
  display: boolean;
  entry: string;
  platforms: string[];
  enabled: boolean;
  state: string | null;
  configVersion: number;
}

export interface PluginDetail extends PluginListItem {
  configSchema: ConfigField[];
  config: { version: number; values: Record<string, unknown> };
  status: { state: string; data?: Record<string, unknown>; ts: string } | null;
  records: { key: string; value: unknown; updatedAt: string }[];
}

export interface SseEvent {
  type: 'command' | 'config' | 'current' | 'plugin-state';
  id?: string;
  [key: string]: unknown;
}

/** GET /api/category（只取选择器需要的字段） */
export interface CategoryItem {
  id: number;
  name: string | null;
}

/** GET /api/appdata（只取选择器需要的字段） */
export interface AppItem {
  id: number;
  name: string | null;
  alias: string | null;
  totalTime: number;
}

/** 应用选择器选项（name = 进程名，即匹配词） */
export interface AppOption {
  name: string;
  alias: string | null;
  totalTime: number;
}
