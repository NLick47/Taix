import type {
  ApiResponse, AppItem, CategoryItem, PluginDetail, PluginListItem,
} from './types';

// 按状态码区分失败原因：空响应体的 404 是最常见的一种——
// 后台服务在跑，但二进制比 webui 旧、不含该路由（编译产物未更新）
function httpError(status: number): Error {
  const at = location.host;
  if (status === 404) {
    return new Error(
      `本地服务（${at}）没有这个接口，通常是 taix-server 版本较旧。` +
        '请重新编译并重启 taix-server 后再试。',
    );
  }
  if (status === 502 || status === 503) {
    return new Error(`本地服务暂时不可用（HTTP ${status}），可能正在重启，稍后会自动重连。`);
  }
  return new Error(`本地服务返回异常（HTTP ${status}），地址 ${at}`);
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
    throw new Error(`无法连接本地服务（${location.host}），请确认 Taix 后台正在运行`);
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
