import type {
  ApiResponse, AppItem, CategoryItem, PluginDetail, PluginListItem,
} from './types';
import { t } from './i18n.svelte';

// 按状态码区分失败原因：空响应体的 404 是最常见的一种——
// 后台服务在跑，但二进制比 webui 旧、不含该路由（编译产物未更新）
function httpError(status: number): Error {
  if (status === 404) return new Error(t('err.api404', { at: location.host }));
  if (status === 502 || status === 503) return new Error(t('err.busy', { status }));
  return new Error(t('err.http', { status, at: location.host }));
}

async function request<T>(method: string, path: string, body?: unknown): Promise<T> {
  let resp: Response;
  try {
    resp = await fetch(path, {
      method,
      headers: body !== undefined ? { 'Content-Type': 'application/json' } : undefined,
      body: body !== undefined ? JSON.stringify(body) : undefined,
    });
  } catch {
    throw new Error(t('err.connect', { host: location.host }));
  }
  let api: ApiResponse<T>;
  try {
    api = (await resp.json()) as ApiResponse<T>;
  } catch {
    throw httpError(resp.status);
  }
  if (api.code !== 0) throw new Error(api.message || `HTTP ${resp.status}`);
  return api.data as T;
}

export const api = {
  listPlugins: () => request<PluginListItem[]>('GET', '/api/plugins'),
  getPlugin: (id: string) => request<PluginDetail>('GET', `/api/plugins/${id}`),
  saveConfig: (id: string, values: Record<string, unknown>) =>
    request<{ configVersion: number }>('PUT', `/api/plugins/${id}/config`, { values }),
  setEnabled: (id: string, enabled: boolean) =>
    request('POST', `/api/plugins/${id}/enabled`, { enabled }),
  postCommand: (id: string, cmd: string, data?: unknown) =>
    request('POST', `/api/plugins/${id}/commands`, { cmd, data }),
  // 读插件记录：返回该 key 最新一条的 value，无记录返回 null
  getRecord: (id: string, key: string) =>
    request<{ key: string; value: unknown }[]>(
      'GET',
      `/api/plugins/${id}/records?key=${encodeURIComponent(key)}`,
    ).then((rows) => (Array.isArray(rows) && rows.length ? rows[0].value : null)),
  listCategories: () => request<CategoryItem[]>('GET', '/api/category'),
  listApps: () => request<AppItem[]>('GET', '/api/appdata'),
};

/** 插件自带配置 UI 的通用请求入口，getJson/postJson 转发到它 */
export { request as rawRequest };
