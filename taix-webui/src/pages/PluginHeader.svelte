<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import { canLeave, statusOf } from '../lib/store.svelte';
  import { statusLabel, t } from '../lib/i18n.svelte';
  import type { PluginDetail } from '../lib/types';

  // 详情 / 配置两个视图共用同一页头，保证 tab 位置与身份信息完全一致
  let {
    plugin,
    tab,
    onRefresh = null,
    busy = false,
  }: {
    plugin: PluginDetail;
    tab: 'overview' | 'config';
    onRefresh?: (() => void) | null;
    busy?: boolean;
  } = $props();

  const s = $derived(statusOf(plugin));

  // 切视图会重建组件，配置页的未保存改动由守卫拦截
  function guardClick(e: MouseEvent) {
    if (!canLeave()) e.preventDefault();
  }
</script>

<!-- 返回入口：顶栏 logo 太隐蔽，这里给一条显式面包屑 -->
<div class="crumb">
  <a href="#/plugins" onclick={guardClick}>
    <Icon name="chevron-left" size={13} />
    <span>{t('detail.back')}</span>
  </a>
</div>

<div class="page-head">
  <div class="h-main">
    <h1>{plugin.name}</h1>
    <div class="sub">
      <span class="tag {s === 'running' || s === 'idle' ? 'ok' : s === 'paused' ? 'warn' : ''}">
        <span class="tdot"></span>{statusLabel(s)}
      </span>
      <span class="pid">{plugin.id}</span>
      <span>·</span>
      <span>v{plugin.version}</span>
      <span>·</span>
      <span>{plugin.display ? `${t('kind.overlay')} · ${t('kind.process')}` : t('kind.process')}</span>
    </div>
  </div>
</div>

<div class="view-bar">
  <div class="seg">
    <a href={'#/plugins/' + plugin.id} class="seg-a" class:active={tab === 'overview'} onclick={guardClick}>{t('header.overview')}</a>
    <a href={'#/plugins/' + plugin.id + '/config'} class="seg-a" class:active={tab === 'config'} onclick={guardClick}>{t('header.config')}</a>
  </div>
  {#if onRefresh}
    <button class="icon-only" onclick={onRefresh} disabled={busy} title={t('plugins.refreshTitle')} aria-label={t('plugins.refresh')}>
      <span class:spin={busy}><Icon name="refresh" size={14} /></span>
    </button>
  {/if}
</div>

<style>
  .crumb a {
    display: inline-flex; align-items: center; gap: 4px;
    margin: 0 0 10px -6px; padding: 4px 7px 4px 5px;
    border-radius: 6px;
    font-size: 12.5px; color: var(--text-2);
    transition: background 0.16s ease, color 0.16s ease;
  }
  .crumb a:hover { background: var(--hover); color: var(--text); }

  .pid { color: var(--text-3); }

  .view-bar {
    display: flex; align-items: center; gap: 9px;
    margin-bottom: 14px;
  }

  .icon-only { width: 32px; padding: 0; }

  /* 分段控件内是链接，不能直接用 .seg button 的规则 */
  .seg .seg-a {
    display: inline-flex; align-items: center;
    height: 26px; padding: 0 12px;
    border: 1px solid transparent; border-radius: 6px;
    font-size: 12.5px; color: var(--text-2);
    transition: background 0.16s ease, color 0.16s ease, box-shadow 0.16s ease;
  }
  .seg .seg-a:hover { background: color-mix(in srgb, var(--text) 5%, transparent); color: var(--text); }
  .seg .seg-a.active {
    background: var(--seg-active); color: var(--text); font-weight: 500;
    border-color: var(--line-strong);
    box-shadow: 0 1px 2px rgb(16 24 40 / 9%);
  }
</style>
