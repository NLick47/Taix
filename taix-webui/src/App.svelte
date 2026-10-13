<script lang="ts">
  import Plugins from './pages/Plugins.svelte';
  import PluginConfig from './pages/PluginConfig.svelte';
  import PluginDetail from './pages/PluginDetail.svelte';
  import Icon from './lib/Icon.svelte';
  import { canLeave, connectPluginStore, pluginStore } from './lib/store.svelte';
  import { cycleTheme, theme, THEME_LABEL } from './lib/theme.svelte';

  let hash = $state(location.hash || '#/plugins');
  window.addEventListener('hashchange', () => (hash = location.hash || '#/plugins'));

  const view = $derived.by(() => {
    const m = hash.match(/^#\/plugins\/([a-z0-9-]+)(\/config)?$/);
    if (m) return m[2] ? { page: 'config', id: m[1] } : { page: 'detail', id: m[1] };
    return { page: 'list', id: '' };
  });

  const activeName = $derived(pluginStore.items.find((p) => p.id === view.id)?.name ?? view.id);

  $effect(() => connectPluginStore());

  $effect(() => {
    if (view.page === 'list') document.title = '插件 · Taix';
    else if (view.page === 'config') document.title = `${activeName} 配置 · Taix`;
    else document.title = `${activeName} · Taix`;
  });

  function onCrumbClick(e: MouseEvent) {
    if (!canLeave()) e.preventDefault();
  }
</script>

<div class="shell">
  <header class="topbar">
    <a class="home" href="#/plugins" onclick={onCrumbClick} title="返回插件列表">
      <span class="mark">T</span>
      <span class="brand">Taix</span>
    </a>
    <span class="spacer"></span>
    <span class="conn" class:bad={pluginStore.conn !== 'live'} title={'本地服务 ' + location.host}>
      <span class="dot"></span>{pluginStore.conn === 'live' ? '已连接' : '重连中…'}
    </span>
    <button
      class="icon-btn"
      onclick={cycleTheme}
      title={'外观：' + THEME_LABEL[theme.choice] + '（点击切换）'}
    >
      <Icon name={theme.choice === 'auto' ? 'contrast' : theme.choice === 'light' ? 'sun' : 'moon'} size={14} />
    </button>
  </header>

  <main class="canvas" class:canvas-narrow={view.page === 'config' && view.id !== 'reminder'}>
    <!-- key 只用 id：概览与配置同属一个插件，切 tab 不该重建整个画布 -->
    {#key view.id}
      {#if view.page === 'list'}
        <Plugins />
      {:else if view.page === 'config'}
        <PluginConfig id={view.id} />
      {:else}
        <PluginDetail id={view.id} />
      {/if}
    {/key}
  </main>
</div>
