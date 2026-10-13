<script lang="ts">
  import Icon from './Icon.svelte';
  import type { AppOption } from './types';

  // 应用匹配选择器：chips + 搜索下拉。value 是逗号分隔的匹配词串，
  // 匹配词仍是子串语义，所以既可以从列表里挑真实应用，也可以输入自定义词。
  let { apps, value, onchange }: {
    apps: AppOption[];
    value: string;
    onchange: (v: string) => void;
  } = $props();

  function parse(v: string): string[] {
    return v.split(/[,，]/).map((s) => s.trim()).filter(Boolean);
  }

  const tokens = $derived(parse(value));
  const sorted = $derived(
    [...apps].sort((a, b) => b.totalTime - a.totalTime || a.name.localeCompare(b.name)),
  );
  const byName = $derived.by(() => {
    const map = new Map<string, AppOption>();
    for (const app of apps) map.set(app.name.toLowerCase(), app);
    return map;
  });
  const visible = $derived.by(() => {
    const q = search.trim().toLowerCase();
    if (!q) return sorted;
    return sorted.filter(
      (a) => a.name.toLowerCase().includes(q) || (a.alias ?? '').toLowerCase().includes(q),
    );
  });

  let open = $state(false);
  let search = $state('');
  let root: HTMLDivElement | undefined = $state();
  let pop: HTMLDivElement | undefined = $state();
  let coords = $state({ top: 0, left: 0, width: 260 });

  function matchesKnown(t: string): boolean {
    const low = t.toLowerCase();
    return apps.some((a) => a.name.toLowerCase().includes(low));
  }
  function labelOf(t: string): string {
    return byName.get(t.toLowerCase())?.alias?.trim() || t;
  }
  function isPicked(name: string): boolean {
    return tokens.some((t) => t.toLowerCase() === name.toLowerCase());
  }
  function toggle(name: string) {
    onchange(
      isPicked(name)
        ? tokens.filter((t) => t.toLowerCase() !== name.toLowerCase()).join(', ')
        : [...tokens, name].join(', '),
    );
  }
  function remove(t: string) {
    onchange(tokens.filter((x) => x !== t).join(', '));
  }
  function commit() {
    const q = search.trim();
    if (!q) return;
    const hit = apps.find(
      (a) =>
        a.name.toLowerCase() === q.toLowerCase() ||
        (a.alias ?? '').trim().toLowerCase() === q.toLowerCase(),
    );
    if (hit) toggle(hit.name);
    else onchange([...tokens, q].join(', '));
    search = '';
  }
  function onKeydown(e: KeyboardEvent) {
    if (e.key === 'Enter') {
      e.preventDefault();
      commit();
    } else if (e.key === 'Escape') {
      open = false;
      search = '';
    }
  }
  function onWindowClick(e: MouseEvent) {
    if (open && root && !root.contains(e.target as Node)) {
      open = false;
      search = '';
    }
  }

  // 卡片自身 overflow: hidden，弹层用 fixed 挂到视口坐标，滚动/缩放时跟随
  function place() {
    if (!root) return;
    const r = root.getBoundingClientRect();
    const width = Math.max(r.width, 260);
    const height = pop?.offsetHeight ?? 264;
    let top = r.bottom + 4;
    if (top + height > window.innerHeight - 8 && r.top - height - 4 > 8) {
      top = r.top - height - 4;
    }
    const left = Math.max(8, Math.min(r.left, window.innerWidth - width - 8));
    coords = { top, left, width };
  }

  $effect(() => {
    if (!open) return;
    place();
    const onMove = () => place();
    window.addEventListener('scroll', onMove, true);
    window.addEventListener('resize', onMove);
    return () => {
      window.removeEventListener('scroll', onMove, true);
      window.removeEventListener('resize', onMove);
    };
  });
</script>

<svelte:window onclick={onWindowClick} />

<div class="ap" bind:this={root}>
  <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
  <div
    class="ap-box"
    role="button"
    tabindex="0"
    onclick={() => (open = !open)}
    onkeydown={(e) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        open = !open;
      }
    }}
  >
    {#if tokens.length === 0}
      <span class="ap-ph">选择应用，或输入匹配词</span>
    {:else}
      {#each tokens as t (t)}
        <span
          class="ap-chip"
          class:unknown={!matchesKnown(t)}
          title={matchesKnown(t) ? `匹配词 ${t}` : `${t} 未匹配到任何已记录的应用`}
        >
          <span class="ap-chip-t">{labelOf(t)}</span>
          <button
            class="ap-x"
            type="button"
            aria-label="移除"
            onclick={(e) => {
              e.stopPropagation();
              remove(t);
            }}
          >
            <Icon name="x" size={10} />
          </button>
        </span>
      {/each}
    {/if}
  </div>

  {#if open}
    <div
      class="ap-pop"
      bind:this={pop}
      style="top: {coords.top}px; left: {coords.left}px; width: {coords.width}px"
    >
      <input
        class="ap-search"
        placeholder="搜索应用，回车添加自定义匹配词"
        bind:value={search}
        onkeydown={onKeydown}
      />
      <div class="ap-list">
        {#each visible as a (a.name)}
          <button type="button" class="ap-item" class:sel={isPicked(a.name)} onclick={() => toggle(a.name)}>
            <span class="ap-i-name">{a.alias?.trim() || a.name}</span>
            {#if a.alias?.trim()}
              <span class="ap-i-sub">{a.name}</span>
            {/if}
            <span class="spacer"></span>
            {#if isPicked(a.name)}
              <Icon name="check" size={12} />
            {/if}
          </button>
        {/each}
        {#if visible.length === 0 && search.trim()}
          <button type="button" class="ap-item custom" onclick={commit}>
            添加「{search.trim()}」作为匹配词
          </button>
        {:else if visible.length === 0}
          <div class="ap-empty">还没有记录到应用</div>
        {/if}
      </div>
    </div>
  {/if}
</div>

<style>
  .ap { position: relative; }

  .ap-box {
    display: flex; flex-wrap: wrap; align-items: center; gap: 4px;
    min-height: 29px; padding: 3px 8px;
    border: 0.5px solid var(--line-strong);
    border-radius: var(--r-control);
    background: var(--card);
    cursor: pointer;
    transition: border-color 0.12s, box-shadow 0.12s;
  }
  .ap-box:focus-visible { outline: none; border-color: var(--accent); box-shadow: var(--ring); }
  .ap-ph { font-size: 12.5px; color: var(--text-4); }

  .ap-chip {
    display: inline-flex; align-items: center; gap: 3px;
    height: 21px; padding: 0 3px 0 7px; max-width: 100%;
    border-radius: 6px;
    background: var(--accent-quiet); color: var(--accent);
    font-size: 11.5px;
  }
  .ap-chip.unknown { background: var(--warn-quiet); color: var(--warn); }
  .ap-chip-t { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; max-width: 180px; }

  .ap-x {
    display: inline-flex; align-items: center; justify-content: center;
    width: 15px; height: 15px; padding: 0; flex: none;
    border: 0; border-radius: 4px; background: transparent;
    box-shadow: none; color: inherit; cursor: pointer;
  }
  .ap-x:hover { background: color-mix(in srgb, currentColor 16%, transparent); }

  .ap-pop {
    position: fixed; z-index: 40;
    display: flex; flex-direction: column;
    background: var(--card);
    border: 0.5px solid var(--line-strong);
    border-radius: 10px;
    box-shadow: var(--sh-pop);
    overflow: hidden;
  }
  .ap-search {
    width: 100%; height: 32px; flex: none;
    border: 0; border-bottom: 0.5px solid var(--line);
    border-radius: 0; background: transparent;
    padding: 0 11px; font-size: 12.5px;
  }
  .ap-search:focus { border-color: var(--line); box-shadow: none; }

  .ap-list { max-height: 216px; overflow: auto; padding: 4px; }

  .ap-item {
    display: flex; align-items: center; gap: 7px; width: 100%;
    height: auto; padding: 6px 8px; border-radius: 7px;
    border: 0; background: transparent; box-shadow: none;
    font-size: 12.5px; font-weight: 400; text-align: left;
    color: var(--text); cursor: pointer;
  }
  .ap-item:hover { background: var(--hover); }
  .ap-item.sel { color: var(--accent); }
  .ap-item.custom { color: var(--accent); }
  .ap-i-name { min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .ap-i-sub { min-width: 0; font-size: 11px; color: var(--text-3); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }

  .ap-empty { padding: 12px; font-size: 12px; color: var(--text-3); text-align: center; }
</style>
