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
  /** 插件自带配置 UI 的入口模块，ui/ 下的文件名；null = 走通用表单 */
  configUi: string | null;
  config: { version: number; values: Record<string, unknown> };
  status: { state: string; data?: Record<string, unknown>; ts: string } | null;
  records: { key: string; value: unknown; updatedAt: string }[];
}

export interface SseEvent {
  type: 'command' | 'config' | 'current' | 'plugin-state';
  id?: string;
  [key: string]: unknown;
}

/** 插件自带配置 UI 的主题信息 */
export interface PluginUiTheme {
  choice: 'auto' | 'light' | 'dark';
  resolved: 'light' | 'dark';
}

/**
 * 插件自带配置 UI 的宿主 API，即 mount(el, api) 的第二个参数
 * 契约见 docs/plugin-dev-guide.md，插件侧参考实现见 taix-plugins/reminder/ui/src/bridge.ts
 */
export interface PluginUiApi {
  /** 当前插件 id / 版本 / manifest 的 configSchema */
  id: string;
  version: string;
  schema: ConfigField[];
  /** 挂载时的配置快照，非响应式；写回一律走 patch */
  values: Record<string, unknown>;
  /** 合并改动进宿主草稿，脏检测与保存栏基于它 */
  patch(partial: Record<string, unknown>): void;
  theme: PluginUiTheme;
  onTheme(cb: (t: PluginUiTheme) => void): () => void;
  /** 非致命错误：显示在宿主错误条，不触发回退 */
  reportError(msg: string | null): void;
  /** 同源 REST 便捷封装：自动解 {code,message,data}，失败抛 Error */
  getJson(path: string): Promise<unknown>;
  postJson(path: string, body?: unknown): Promise<unknown>;
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
