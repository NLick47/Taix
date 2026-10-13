<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import { api } from '../lib/api';
  import { subscribe } from '../lib/sse';
  import PluginHeader from './PluginHeader.svelte';
  import type { PluginDetail } from '../lib/types';

  let { id }: { id: string } = $props();

  let plugin = $state<PluginDetail | null>(null);
  let statusData = $state<Record<string, unknown> | null>(null);
  let busy = $state('');
  let enabled = $state(false);
  let error = $state('');

  $effect(() => {
    load();
    return subscribe('webui', (e) => {
      if (e.type === 'plugin-state' && e.id === id) {
        const data = e.data as Record<string, unknown> | undefined;
        if (!data) return;
        // 启停广播只带 { enabled }：同步开关，不覆盖状态面板
        const keys = Object.keys(data);
        if (keys.length === 1 && typeof data.enabled === 'boolean') {
          enabled = data.enabled;
        } else {
          statusData = data;
        }
      }
    });
  });

  async function load() {
    try {
      plugin = await api.getPlugin(id);
      statusData = plugin.status?.data ?? null;
      enabled = plugin.enabled;
      error = '';
    } catch (e) {
      error = String(e);
    }
  }

  // 动态指令按钮：actions 由插件状态上报
  const actions = $derived.by(() => {
    const raw = statusData?.['actions'];
    if (!Array.isArray(raw)) return [];
    return raw.filter(
      (a): a is { cmd: string; label: string } =>
        !!a && typeof a === 'object' && typeof (a as any).cmd === 'string',
    );
  });

  const kvRows = $derived(Object.entries(statusData ?? {}).filter(([k]) => k !== 'actions'));

  async function run(cmd: string) {
    busy = cmd;
    try {
      await api.postCommand(id, cmd);
    } catch (e) {
      error = String(e);
    }
    busy = '';
  }

  async function toggleEnabled(next: boolean) {
    enabled = next; // 乐观更新
    try {
      await api.setEnabled(id, next);
    } catch (e) {
      enabled = !next;
      error = String(e);
    }
  }

  function fmtValue(v: unknown): string {
    if (typeof v === 'object') return JSON.stringify(v);
    return String(v);
  }
</script>

{#if plugin}
  <PluginHeader {plugin} tab="overview" onRefresh={load} busy={busy !== ''} />

  {#if error}
    <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{error}</div>
  {/if}

  <div class="split">
    <div>
      {#if actions.length}
        <div class="section-label">插件指令</div>
        <div class="actions" style="margin-bottom: 18px">
          {#each actions as a, i (a.cmd)}
            <button class={i === 0 ? 'primary' : ''} onclick={() => run(a.cmd)} disabled={busy !== ''}>
              {a.label || a.cmd}
            </button>
          {/each}
        </div>
      {/if}

      <div class="section-label">{enabled ? '运行状态' : '状态'}</div>
      {#if kvRows.length}
        <div class="card">
          {#each kvRows as [k, v] (k)}
            <div class="kv"><span class="k">{k}</span><span class="v">{fmtValue(v)}</span></div>
          {/each}
        </div>
      {:else}
        <div class="card muted-block">
          <div class="mt">{enabled ? '尚未上报状态，稍候自动刷新' : '插件已停用'}</div>
          <div class="ms">{enabled ? '插件每 5 秒上报一次' : '启用后开始上报运行数据'}</div>
        </div>
      {/if}
    </div>

    <div>
      <div class="section-label">启用</div>
      <div class="card side-card">
        <div class="side-row">
          <div class="side-text">
            <div class="st1">{enabled ? '已启用' : '已停用'}</div>
          </div>
          <span class="switch">
            <input type="checkbox" checked={enabled} onchange={(e) => toggleEnabled((e.target as HTMLInputElement).checked)} />
            <span class="track"></span>
          </span>
        </div>
      </div>

      <div class="section-label" style="margin-top: 18px">元信息</div>
      <div class="card">
        <div class="kv"><span class="k">类型</span><span class="v">{plugin.display ? '悬浮窗' : '独立进程'}</span></div>
        <div class="kv"><span class="k">平台</span><span class="v">{plugin.platforms.length ? plugin.platforms.join('、') : '不限'}</span></div>
        <div class="kv"><span class="k">配置版本</span><span class="v tnum">{plugin.configVersion ?? '—'}</span></div>
        <div class="kv"><span class="k">入口</span><span class="v">{plugin.entry}</span></div>
      </div>
    </div>
  </div>
{:else if error}
  <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{error}</div>
  <a href="#/plugins"><button class="flat">返回列表</button></a>
{:else}
  <div class="skeleton" style="height: 30px; width: 220px; margin-bottom: 12px"></div>
  <div class="skeleton" style="height: 60px; width: 180px; margin-bottom: 18px"></div>
  <div class="skeleton" style="height: 140px"></div>
{/if}

<style>
  .split {
    display: grid;
    grid-template-columns: minmax(0, 1fr) 208px;
    gap: 20px;
    align-items: start;
  }
  @media (max-width: 900px) {
    .split { grid-template-columns: minmax(0, 1fr); }
  }

  .muted-block { padding: 15px 17px; width: fit-content; min-width: 260px; }
  .mt { font-size: 12.5px; color: var(--text-2); }
  .ms { font-size: 11.5px; color: var(--text-3); margin-top: 3px; }

  .side-card { padding: 12px 14px; }
  .side-row { display: flex; align-items: center; gap: 10px; }
  .side-text { flex: 1; min-width: 0; }
  .st1 { font-size: 12.5px; font-weight: 500; }
</style>
