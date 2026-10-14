/** 宿主传给 mount(el, api) 的主题信息 */
export interface PluginUiTheme {
  choice: 'auto' | 'light' | 'dark';
  resolved: 'light' | 'dark';
}

/** 清单 configSchema 的字段描述，本 UI 只做类型参考 */
export interface ConfigField {
  key: string;
  label: string;
  type: 'string' | 'text' | 'number' | 'bool' | 'select' | 'json';
  min?: number;
  max?: number;
  options?: string[];
  default?: unknown;
}

/** 宿主 API，配置页在挂载时将实例传入 */
export interface TaixPluginUiApi {
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

/** 应用选择项，来自 GET /api/appdata */
export interface AppOption {
  name: string;
  alias: string | null;
  totalTime: number;
}

export function postCommand(api: TaixPluginUiApi, cmd: string, data?: unknown): Promise<unknown> {
  return api.postJson(`/api/plugins/${api.id}/commands`, { cmd, data });
}

/** 读插件记录：返回该 key 最新一条的 value，无记录返回 null */
export async function getRecord<T>(api: TaixPluginUiApi, key: string): Promise<T | null> {
  const rows = (await api.getJson(
    `/api/plugins/${api.id}/records?key=${encodeURIComponent(key)}`,
  )) as { value: T }[] | null;
  return Array.isArray(rows) && rows.length ? rows[0].value : null;
}

/** 分类名列表，来自 GET /api/category */
export async function listCategories(api: TaixPluginUiApi): Promise<string[]> {
  const rows = (await api.getJson('/api/category')) as { name: string | null }[];
  return rows.map((c) => c.name ?? '').filter(Boolean);
}

/** 应用列表，用于匹配词选择器 */
export async function listApps(api: TaixPluginUiApi): Promise<AppOption[]> {
  const rows = (await api.getJson('/api/appdata')) as {
    name: string | null;
    alias: string | null;
    totalTime?: number;
  }[];
  return rows.flatMap((a) =>
    a.name ? [{ name: a.name, alias: a.alias, totalTime: a.totalTime ?? 0 }] : [],
  );
}
