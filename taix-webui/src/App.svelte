<script lang="ts">
  import Plugins from './pages/Plugins.svelte';
  import PluginConfig from './pages/PluginConfig.svelte';
  import PluginDetail from './pages/PluginDetail.svelte';
  import Icon from './lib/Icon.svelte';
  import { canLeave, connectPluginStore, layout, pluginStore } from './lib/store.svelte';
  import { cycleTheme, theme } from './lib/theme.svelte';
  import { cycleLocale, locale, localeLabel, localeShort, t, themeLabel } from './lib/i18n.svelte';

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
    if (view.page === 'list') document.title = t('app.title.list');
    else if (view.page === 'config') document.title = t('app.title.config', { name: activeName });
    else document.title = t('app.title.detail', { name: activeName });
  });

  function onCrumbClick(e: MouseEvent) {
    if (!canLeave()) e.preventDefault();
  }
</script>

<div class="shell">
  <header class="topbar">
    <a class="home" href="#/plugins" onclick={onCrumbClick} title={t('topbar.home')}>
      <span class="brand">Taix</span>
    </a>
    <span class="spacer"></span>
    <span class="conn" class:bad={pluginStore.conn !== 'live'} title={t('topbar.conn', { host: location.host })}>
      <span class="dot"></span>{pluginStore.conn === 'live' ? t('topbar.connected') : t('topbar.reconnecting')}
    </span>
    <button class="icon-btn" onclick={cycleLocale} title={t('topbar.locale', { label: localeLabel(locale.choice) })}>
      {#if locale.choice === 'auto'}
        <Icon name="globe" size={14} />
      {:else}
        <span class="loc-t">{localeShort[locale.resolved]}</span>
      {/if}
    </button>
    <button
      class="icon-btn"
      onclick={cycleTheme}
      title={t('topbar.theme', { label: themeLabel(theme.choice) })}
    >
      <Icon name={theme.choice === 'auto' ? 'contrast' : theme.choice === 'light' ? 'sun' : 'moon'} size={14} />
    </button>
  </header>

  <!-- 配置页默认窄画布，插件自带 UI 时由页面把 layout.canvasWide 置宽 -->
  <main class="canvas" class:canvas-narrow={view.page === 'config' && !layout.canvasWide}>
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

<style>
  .loc-t { font-size: 11px; font-weight: 600; letter-spacing: 0.02em; }
</style>
