<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import { canLeave, statusLabel, statusOf } from '../lib/store.svelte';
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

<div class="page-head">
  <div class="h-main">
    <h1>
      <span class="glyph"><Icon name="puzzle" size={16} /></span>
      {plugin.name}
    </h1>
    <div class="sub">
      <span class="tag {s === 'running' || s === 'idle' ? 'ok' : s === 'paused' ? 'warn' : ''}">
        <span class="tdot"></span>{statusLabel[s]}
      </span>
      <span class="pid">{plugin.id}</span>
      <span>·</span>
      <span>v{plugin.version}</span>
      <span>·</span>
      <span>{plugin.display ? '悬浮窗 · 独立进程' : '独立进程'}</span>
    </div>
  </div>
</div>

<div class="view-bar">
  <div class="seg">
    <a href={'#/plugins/' + plugin.id} class="seg-a" class:active={tab === 'overview'} onclick={guardClick}>概览</a>
    <a href={'#/plugins/' + plugin.id + '/config'} class="seg-a" class:active={tab === 'config'} onclick={guardClick}>配置</a>
  </div>
  <span class="spacer"></span>
  {#if onRefresh}
    <button onclick={onRefresh} disabled={busy}>
      <Icon name="refresh" size={13} />刷新
    </button>
  {/if}
</div>

<style>
  .pid { color: var(--text-3); }

  .view-bar {
    display: flex; align-items: center; gap: 9px;
    margin-bottom: 18px;
  }

  /* 分段控件内是链接，不能直接用 .seg button 的规则 */
  .seg .seg-a {
    display: inline-flex; align-items: center;
    height: 26px; padding: 0 14px;
    border-radius: 7px;
    font-size: 12.5px; color: var(--text-2);
    transition: background 0.12s, color 0.12s;
  }
  .seg .seg-a:hover { color: var(--text); }
  .seg .seg-a.active {
    background: var(--seg-active); color: var(--text); font-weight: 500;
    box-shadow: var(--sh-pill);
  }
</style>
