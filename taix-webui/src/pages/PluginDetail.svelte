<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import { api } from '../lib/api';
  import { subscribe } from '../lib/sse';
  import { t } from '../lib/i18n.svelte';
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

  <div class="detail">
    {#if actions.length}
      <div class="section-label">{t('detail.commands')}</div>
      <div class="actions" style="margin-bottom: 20px">
        {#each actions as a, i (a.cmd)}
          <button class={i === 0 ? 'primary' : ''} onclick={() => run(a.cmd)} disabled={busy !== ''}>
            {a.label || a.cmd}
          </button>
        {/each}
      </div>
    {/if}

    <div class="section-label">{enabled ? t('detail.runtime') : t('detail.status')}</div>
    <div class="card">
      <div class="side-row">
        <span class="side-label">{t('detail.enablePlugin')}</span>
        <span class="switch">
          <input type="checkbox" checked={enabled} onchange={(e) => toggleEnabled((e.target as HTMLInputElement).checked)} />
          <span class="track"></span>
        </span>
      </div>
      {#if kvRows.length}
        {#each kvRows as [k, v] (k)}
          <div class="kv"><span class="k">{k}</span><span class="v">{fmtValue(v)}</span></div>
        {/each}
      {:else}
        <div class="status-line">
          <span class="mt">{enabled ? t('detail.noStatus') : t('detail.enableHint')}</span>
        </div>
      {/if}
    </div>

    <div class="section-label">{t('detail.meta')}</div>
    <div class="card meta-bar">
      <div class="meta-cell">
        <div class="k">{t('detail.type')}</div>
        <div class="v">{plugin.display ? t('kind.overlay') : t('kind.process')}</div>
      </div>
      <div class="meta-cell">
        <div class="k">{t('detail.platforms')}</div>
        <div class="v">{plugin.platforms.length ? plugin.platforms.join(t('detail.listSep')) : t('detail.platformAll')}</div>
      </div>
      <div class="meta-cell">
        <div class="k">{t('detail.configVersion')}</div>
        <div class="v tnum">{plugin.configVersion ?? '—'}</div>
      </div>
      <div class="meta-cell">
        <div class="k">{t('detail.entry')}</div>
        <div class="v mono" title={plugin.entry}>{plugin.entry}</div>
      </div>
    </div>
  </div>
{:else if error}
  <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{error}</div>
  <a href="#/plugins"><button class="flat">{t('detail.back')}</button></a>
{:else}
  <div class="skeleton" style="height: 30px; width: 220px; margin-bottom: 12px"></div>
  <div class="skeleton" style="height: 60px; width: 180px; margin-bottom: 18px"></div>
  <div class="skeleton" style="height: 140px"></div>
{/if}

<style>
  .detail > .section-label:first-child { margin-top: 0; }

  .side-row { display: flex; align-items: center; gap: 10px; padding: 11px 14px; }
  .side-label { flex: 1; font-size: 13px; font-weight: 500; }

  .status-line { padding: 11px 14px; border-top: 1px solid var(--line); }
  .mt { font-size: 12.5px; color: var(--text-2); }

  .meta-bar { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); }
  .meta-cell {
    min-width: 0; padding: 12px 14px;
    border-left: 1px solid var(--line);
  }
  .meta-cell:first-child { border-left: 0; }
  .meta-cell .k { font-size: 11px; color: var(--text-3); }
  .meta-cell .v {
    display: inline-block; max-width: 100%;
    margin-top: 6px; padding: 2px 8px;
    border-radius: 6px;
    background: var(--card-inset);
    box-shadow: inset 0 0 0 1px var(--line);
    font-size: 12.5px; font-weight: 500;
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
    vertical-align: bottom;
  }
  .meta-cell .v.mono { font-size: 11.5px; }
  @media (max-width: 900px) {
    .meta-bar { grid-template-columns: repeat(2, minmax(0, 1fr)); }
    .meta-cell:nth-child(odd) { border-left: 0; }
    .meta-cell:nth-child(n+3) { border-top: 1px solid var(--line); }
  }
  @media (max-width: 560px) {
    .meta-bar { grid-template-columns: minmax(0, 1fr); }
    .meta-cell { border-left: 0; border-top: 1px solid var(--line); }
    .meta-cell:first-child { border-top: 0; }
  }
</style>
