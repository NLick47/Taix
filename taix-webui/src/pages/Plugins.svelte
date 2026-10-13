<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import { api } from '../lib/api';
  import { pluginStore, refreshPlugins, statusLabel, statusOf, type Status } from '../lib/store.svelte';

  async function toggle(id: string, enabled: boolean) {
    const p = pluginStore.items.find((x) => x.id === id);
    if (p) p.enabled = enabled; // 乐观更新
    try {
      await api.setEnabled(id, enabled);
    } catch {
      if (p) p.enabled = !enabled;
      refreshPlugins();
    }
  }

  const total = $derived(pluginStore.items.length);
  const running = $derived(pluginStore.items.filter((p) => p.enabled).length);
  const stopped = $derived(total - running);

  function tagClass(s: Status): string {
    return s === 'running' || s === 'idle' ? 'ok' : s === 'paused' ? 'warn' : '';
  }

  function kindLabel(p: { display: boolean }): string {
    return p.display ? '悬浮窗' : '独立进程';
  }
</script>

<div class="page-head">
  <div class="h-main">
    <h1>插件</h1>
    <div class="sub">插件常驻后台运行，客户端关闭不受影响</div>
  </div>
  <div class="acts">
    <button onclick={refreshPlugins} title="重新读取插件列表">
      <Icon name="refresh" size={13} />刷新
    </button>
  </div>
</div>

{#if pluginStore.error}
  <div class="notice err"><span class="ico"><Icon name="alert" size={14} /></span>{pluginStore.error}</div>
{/if}

{#if !pluginStore.loaded}
  <div class="tiles">
    <div class="skeleton" style="height: 62px"></div>
    <div class="skeleton" style="height: 62px"></div>
    <div class="skeleton" style="height: 62px"></div>
  </div>
  <div class="skeleton" style="height: 160px"></div>
{:else if total === 0 && pluginStore.error}
  <!-- 服务不可达与「确实没有插件」必须分开：否则「服务没起」被读成「没有插件」 -->
  <div class="empty">
    <div class="glyph"><Icon name="alert" size={22} /></div>
    <div class="et">无法读取插件列表</div>
    <div class="eh">本地服务可能未在运行：确认 taix-shell 已启动后重新刷新</div>
  </div>
{:else if total === 0}
  <div class="empty">
    <div class="glyph"><Icon name="puzzle" size={22} /></div>
    <div class="et">尚未安装任何插件</div>
    <div class="eh">把插件目录放入安装路径的 plugins/&lt;id&gt;/ 即自动出现</div>
  </div>
{:else}
  <div class="tiles">
    <div class="tile">
      <span class="ico c-accent"><Icon name="puzzle" size={17} /></span>
      <div>
        <div class="n">{total}</div>
        <div class="l">插件总数</div>
      </div>
    </div>
    <div class="tile">
      <!-- 数量为 0 时用中性色，避免「已启用 0」配绿色图标造成误读 -->
      <span class="ico" class:c-ok={running > 0} class:c-idle={running === 0}>
        <Icon name="activity" size={17} />
      </span>
      <div>
        <div class="n">{running}</div>
        <div class="l">已启用</div>
      </div>
    </div>
    <div class="tile">
      <span class="ico c-idle"><Icon name="play" size={15} /></span>
      <div>
        <div class="n">{stopped}</div>
        <div class="l">已停用</div>
      </div>
    </div>
  </div>

  <div class="section-label">已安装</div>
  <div class="card">
    {#each pluginStore.items as p (p.id)}
      {@const s = statusOf(p)}
      <div
        class="row"
        class:off={s === 'stopped'}
        onclick={() => (location.hash = '#/plugins/' + p.id)}
        role="link"
        tabindex="0"
        onkeydown={(e) => e.key === 'Enter' && (location.hash = '#/plugins/' + p.id)}
      >
        <span class="ico">
          <Icon name="puzzle" size={16} />
          <span class="badge-dot" style:background={s === 'paused' ? 'var(--warn)' : s === 'running' ? 'var(--ok)' : 'var(--idle)'}></span>
        </span>
        <span class="name">
          <span class="n1">{p.name}<span class="chip">{kindLabel(p)}</span></span>
          <span class="n2">{p.id} · v{p.version}</span>        </span>
        <span class="tag {tagClass(s)}"><span class="tdot"></span>{statusLabel[s]}</span>
        <!-- 开关本体是 label 内的 checkbox（可交互），label 仅拦截行点击冒泡 -->
        <!-- svelte-ignore a11y_no_noninteractive_element_interactions, a11y_click_events_have_key_events -->
        <label class="switch" onclick={(e) => e.stopPropagation()}>
          <input
            type="checkbox"
            checked={p.enabled}
            onchange={(e) => toggle(p.id, (e.target as HTMLInputElement).checked)}
          />
          <span class="track"></span>
        </label>
        <span class="go"><Icon name="chevron-right" size={13} /></span>
      </div>
    {/each}
  </div>
{/if}
