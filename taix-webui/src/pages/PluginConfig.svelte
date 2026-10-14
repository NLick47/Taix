<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import { api, rawRequest } from '../lib/api';
  import { setCanvasWide, setLeaveGuard } from '../lib/store.svelte';
  import { theme, type ThemeChoice } from '../lib/theme.svelte';
  import PluginHeader from './PluginHeader.svelte';
  import type { PluginDetail, PluginUiApi, PluginUiTheme } from '../lib/types';

  let { id }: { id: string } = $props();

  let plugin = $state<PluginDetail | null>(null);
  let values = $state<Record<string, unknown>>({});
  let error = $state('');
  let saved = $state(false);
  let saving = $state(false);
  // 载入时的快照：脏检测 + 重置基准
  let baseline = $state('{}');
  // json 类型字段：编辑草稿与解析错误（解析失败时 values 保留上次合法值，保存被拦下）
  let jsonDraft = $state<Record<string, string>>({});
  let jsonError = $state<Record<string, string>>({});

  // 插件自带配置 UI：模块导出 mount(el, api)，编辑经 api.patch 回写宿主草稿
  // 重置改写不了模块内部状态，靠换宿主节点触发重挂载，见下方 {#key uiEpoch}
  let hostEl = $state<HTMLElement | null>(null);
  // 模块加载失败 → 回退通用表单；uiNotice 是模块自己报的错，不回退
  let uiError = $state('');
  let uiNotice = $state('');
  let uiEpoch = $state(0);
  const themeSubs = new Set<(t: PluginUiTheme) => void>();

  const isCustomUi = $derived(!!plugin?.configUi && !uiError);

  $effect(() => {
    load();
  });

  async function load() {
    try {
      plugin = await api.getPlugin(id);
      const v = { ...plugin.config.values };
      for (const f of plugin.configSchema) {
        if (v[f.key] === undefined && f.default !== undefined) v[f.key] = f.default;
      }
      values = v;
      baseline = JSON.stringify(v);
      jsonDraft = {};
      jsonError = {};
      error = '';
    } catch (e) {
      error = String(e);
    }
  }

  // 模块可 patch 任意值：序列化失败（循环引用等）不能把整页渲染带走
  function safeJson(v: unknown): string {
    try {
      return JSON.stringify(v) ?? 'undefined';
    } catch {
      return '«无法序列化»';
    }
  }

  const dirty = $derived(safeJson(values) !== baseline);

  // 改动过的字段数（保存栏要写明改了几处，而不只是「有修改」）
  const dirtyCount = $derived.by(() => {
    const now = values;
    const base = JSON.parse(baseline) as Record<string, unknown>;
    let n = 0;
    const keys = new Set([...Object.keys(now), ...Object.keys(base)]);
    for (const k of keys) {
      if (safeJson(now[k]) !== safeJson(base[k])) n++;
    }
    return n;
  });

  // 离开守卫：dirty 时拦下面包屑与刷新
  $effect(() => {
    setLeaveGuard(() => {
      if (!dirty) return true;
      return confirm('有未保存的修改，确定离开？');
    });
    return () => setLeaveGuard(null);
  });

  // ⌘S / Ctrl+S 保存，beforeunload 兜底
  $effect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === 's') {
        e.preventDefault();
        if (dirty) save();
      }
    };
    const onBeforeUnload = (e: BeforeUnloadEvent) => {
      if (!dirty) return;
      e.preventDefault();
      e.returnValue = '';
    };
    window.addEventListener('keydown', onKey);
    window.addEventListener('beforeunload', onBeforeUnload);
    return () => {
      window.removeEventListener('keydown', onKey);
      window.removeEventListener('beforeunload', onBeforeUnload);
    };
  });

  async function save() {
    if (saving) return;
    if (Object.keys(jsonError).length) {
      error = '存在 JSON 格式错误的字段，修正后才能保存';
      return;
    }
    saving = true;
    saved = false;
    try {
      // 先冻结本次提交的内容：保存期间的编辑不能被这次保存「顺带」标成已保存
      const submitted = safeJson(values);
      await api.saveConfig(id, values);
      baseline = submitted;
      saved = true;
      setTimeout(() => (saved = false), 3000);
    } catch (e) {
      error = String(e);
    }
    saving = false;
  }

  function reset() {
    const base = JSON.parse(baseline) as Record<string, unknown>;
    // 原地恢复：自带 UI 的模块持有挂载时快照，靠 uiEpoch 重挂载拿新值
    for (const k of Object.keys(values)) {
      if (!(k in base)) delete values[k];
    }
    Object.assign(values, base);
    jsonDraft = {};
    jsonError = {};
    uiEpoch += 1;
  }

  // json 字段：草稿即所见，解析成功才写进 values（坏 JSON 不能悄悄毁掉原值）
  function setJson(key: string, raw: string) {
    jsonDraft[key] = raw;
    try {
      values[key] = JSON.parse(raw);
      delete jsonError[key];
    } catch (e) {
      jsonError[key] = String(e);
    }
  }

  // ---- 自带 UI 的宿主侧 ----

  function currentTheme(choice: ThemeChoice): PluginUiTheme {
    const attr = document.documentElement.dataset.theme;
    const resolved =
      attr === 'dark' || (attr !== 'light' && matchMedia('(prefers-color-scheme: dark)').matches)
        ? 'dark'
        : 'light';
    return { choice, resolved };
  }

  function makeUiApi(): PluginUiApi {
    return {
      id,
      version: plugin?.version ?? '',
      schema: plugin?.configSchema ?? [],
      // 快照脱离宿主响应式：模块自管内部状态，写回一律走 patch
      values: $state.snapshot(values) as Record<string, unknown>,
      patch(partial) {
        if (!partial || typeof partial !== 'object') return;
        for (const [k, v] of Object.entries(partial)) values[k] = v;
      },
      get theme() {
        return currentTheme(theme.choice);
      },
      onTheme(cb) {
        themeSubs.add(cb);
        return () => themeSubs.delete(cb);
      },
      reportError(msg) {
        uiNotice = msg ?? '';
      },
      getJson: (path) => rawRequest('GET', path),
      postJson: (path, body) => rawRequest('POST', path, body),
    };
  }

  // 挂载时机：宿主节点就绪且清单声明了 configUi；重置走 {#key} 重建节点后重挂载
  $effect(() => {
    const entry = plugin?.configUi;
    const el = hostEl;
    if (!entry || !el || uiError) return;

    let disposed = false;
    let dispose: (() => void) | undefined;

    (async () => {
      try {
        // 插件资源由 server 静态路由同源提供；vite-ignore 保持运行期解析
        const mod = (await import(
          /* @vite-ignore */ `/api/plugins/${id}/ui/${encodeURIComponent(entry)}`
        )) as {
          mount?: (el: HTMLElement, api: PluginUiApi) => void | (() => void);
        };
        if (disposed) return;
        if (typeof mod.mount !== 'function') throw new Error('模块未导出 mount(el, api)');
        const ret = mod.mount(el, makeUiApi());
        if (typeof ret === 'function') dispose = ret;
      } catch (e) {
        if (!disposed) uiError = String(e);
      }
    })();

    return () => {
      disposed = true;
      themeSubs.clear();
      try {
        dispose?.();
      } catch {
        // 模块卸载异常不应阻断换页
      }
    };
  });

  // 主题变化通知模块；choice 必须在循环外读：首轮 themeSubs 为空，循环里建立不了依赖
  $effect(() => {
    const choice = theme.choice;
    for (const cb of themeSubs) cb(currentTheme(choice));
    const mq = matchMedia('(prefers-color-scheme: dark)');
    const onSys = () => {
      for (const cb of themeSubs) cb(currentTheme(theme.choice));
    };
    mq.addEventListener('change', onSys);
    return () => mq.removeEventListener('change', onSys);
  });

  // 画布宽度：自带 UI 需要整幅画布，通用表单留给窄列
  $effect(() => {
    setCanvasWide(isCustomUi);
    return () => setCanvasWide(false);
  });
</script>

{#if plugin}
  <PluginHeader {plugin} tab="config" />

  {#if error}
    <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{error}</div>
  {/if}
  {#if uiError}
    <div class="notice err">
      <span class="ico"><Icon name="alert" size={14} /></span>
      自带配置界面加载失败，已回退通用表单：{uiError}
    </div>
  {/if}
  {#if uiNotice}
    <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{uiNotice}</div>
  {/if}

  {#if isCustomUi}
    {#key uiEpoch}
      <div class="ui-host" bind:this={hostEl}></div>
    {/key}
  {:else if plugin.configSchema.length}
    <div class="hint-line">保存后即时生效，无需重启</div>
    <div class="card">
      {#each plugin.configSchema as f (f.key)}
        {#if f.type === 'bool'}
          <div class="field-row">
            <div style="min-width: 0">
              <div class="fl">{f.label}</div>
            </div>
            <span class="switch">
              <input
                type="checkbox"
                checked={!!values[f.key]}
                oninput={(e) => (values[f.key] = (e.target as HTMLInputElement).checked)}
              />
              <span class="track"></span>
            </span>
          </div>
        {:else if f.type === 'json'}
          <div class="text-field">
            <div class="fl">{f.label}</div>
            <textarea
              rows={6}
              class="mono"
              value={jsonDraft[f.key] ?? JSON.stringify(values[f.key] ?? null, null, 2)}
              oninput={(e) => setJson(f.key, (e.target as HTMLTextAreaElement).value)}
            ></textarea>
            {#if jsonError[f.key]}
              <div class="json-err">JSON 解析失败：{jsonError[f.key]}（已保留上次合法值，修正后才能保存）</div>
            {/if}
          </div>
        {:else}
          <div class="text-field">
            <div class="fl">{f.label}</div>
            {#if f.type === 'select'}
              <select
                value={String(values[f.key] ?? '')}
                oninput={(e) => (values[f.key] = (e.target as HTMLSelectElement).value)}
              >
                {#each f.options ?? [] as opt}
                  <option value={opt}>{opt}</option>
                {/each}
              </select>
            {:else if f.type === 'number'}
              <input
                style="max-width: 220px"
                type="number" min={f.min} max={f.max}
                value={Number(values[f.key] ?? 0)}
                oninput={(e) => (values[f.key] = Number((e.target as HTMLInputElement).value))}
              />
            {:else}
              <input
                type="text"
                value={String(values[f.key] ?? '')}
                oninput={(e) => (values[f.key] = (e.target as HTMLInputElement).value)}
              />
            {/if}
          </div>
        {/if}
      {/each}
    </div>
  {:else}
    <div class="empty" style="padding: 30px 20px">
      <div class="eh">该插件没有可配置项</div>
    </div>
  {/if}

  {#if dirty || saving || saved}
    <div class="savebar">
      {#if saved}
        <span class="state ok"><Icon name="check" size={14} />已保存 · 即时生效</span>
      {:else if dirty}
        <span class="state"><Icon name="alert" size={14} />有 {dirtyCount} 处未保存</span>
      {/if}
      <span class="spacer"></span>
      {#if dirty && !saving}
        <button class="flat" onclick={reset}>放弃</button>
      {/if}
      <span class="hint">⌘S</span>
      <button class="primary" onclick={save} disabled={saving || !dirty}>
        {saving ? '保存中…' : '保存配置'}
      </button>
    </div>
  {/if}
{:else if error}
  <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{error}</div>
{:else}
  <div class="skeleton" style="height: 28px; width: 160px; margin-bottom: 12px"></div>
  <div class="skeleton" style="height: 170px"></div>
{/if}

<style>
  .hint-line {
    font-size: 12px; color: var(--text-3);
    margin: -6px 2px 10px;
  }
  .json-err {
    margin-top: 6px;
    font-size: 12px;
    color: var(--err);
  }
  .ui-host { display: block; }
</style>
