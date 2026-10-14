<script lang="ts">
  // 最小内联图标集（16×16 viewBox 网格，stroke 继承 currentColor，零依赖）
  // 只收录 UI 实际用到的图形；新增图标保持同一笔画风格（1.5px 圆头）
  let { name, size = 16 }: { name: string; size?: number } = $props();

  const paths: Record<string, string> = {
    grid: 'M2.5 2.5h4.5v4.5h-4.5zM9.5 2.5h4v4.5h-4zM2.5 9.5h4.5v4h-4.5zM9.5 9.5h4v4h-4z',
    puzzle:
      'M6 3.5a1.8 1.8 0 0 1 3.6 0H11.5v2a1.8 1.8 0 0 1 0 3.6v2.4h-2.3a1.8 1.8 0 0 0-3.4 0H3.5v-2.3a1.8 1.8 0 0 0 0-3.4v-2.3z',
    gear: 'M8 5.6A2.4 2.4 0 1 0 8 10.4 2.4 2.4 0 0 0 8 5.6zM8 1.8l.6 1.7 1.8-.4 1 1.6-1.2 1.4 1.2 1.4-1 1.6-1.8-.4L8 10.4l-.6-1.7-1.8.4-1-1.6 1.2-1.4-1.2-1.4 1-1.6 1.8.4z',
    'chevron-right': 'M6 3.5L10.5 8 6 12.5',
    'chevron-left': 'M10 3.5L5.5 8 10 12.5',
    plus: 'M8 3v10M3 8h10',
    bell: 'M8 2.5c-2 0-3.2 1.4-3.2 3.4 0 2.8-1 3.4-1.3 4.1h9c-.3-.7-1.3-1.3-1.3-4.1 0-2-1.2-3.4-3.2-3.4zM6.8 12.5a1.3 1.3 0 0 0 2.4 0',
    sliders: 'M3 5h6M11.5 5H13M3 11h1.5M7 11h6M9 3.2v3.6M5.2 9.2v3.6',
    terminal: 'M3 4.5L6.5 8 3 11.5M8 11.5h5',
    activity: 'M2 8.5h2.8L6.5 4l3 8L11 8.5h3',
    check: 'M3 8.5l3.2 3L13 4.5',
    alert: 'M8 2.8L14 13H2zM8 6.8v2.9M8 11.4v.4',
    book: 'M3 3.5h4a2 2 0 0 1 2 2v7a1.6 1.6 0 0 0-1.6-1.6H3zM13 3.5H9.8A1.8 1.8 0 0 0 8 5.3v7.2a1.6 1.6 0 0 1 1.6-1.6H13z',
    play: 'M5.5 3.5l7 4.5-7 4.5z',
    x: 'M4 4l8 8M12 4l-8 8',
    refresh: 'M13 8a5 5 0 1 1-1.5-3.5M13 2.5V5h-2.5',
    sun: 'M8 5.8A2.2 2.2 0 1 0 8 10.2 2.2 2.2 0 0 0 8 5.8zM8 1.8v1.4M8 12.8v1.4M2.8 8h1.4M11.8 8h1.4M4.4 4.4l1 1M10.6 10.6l1 1M11.6 4.4l-1 1M5.4 10.6l-1 1',
    moon: 'M13 9.5A5.5 5.5 0 0 1 6.5 3 5.5 5.5 0 1 0 13 9.5z',
    contrast: 'M8 2.5a5.5 5.5 0 1 0 0 11zM8 2.5a5.5 5.5 0 0 1 0 11z',
    globe: 'M8 2.6a5.4 5.4 0 1 0 0 10.8A5.4 5.4 0 0 0 8 2.6zM2.7 8h10.6M8 2.6c-1.5 1.5-2.3 3.3-2.3 5.4s.8 3.9 2.3 5.4c1.5-1.5 2.3-3.3 2.3-5.4s-.8-3.9-2.3-5.4z',
  };
</script>

<svg
  width={size}
  height={size}
  viewBox="0 0 16 16"
  fill="none"
  stroke="currentColor"
  stroke-width="1.5"
  stroke-linecap="round"
  stroke-linejoin="round"
  aria-hidden="true"
>
  {#if name === 'puzzle'}
    <path d={paths[name] ?? ''} fill="currentColor" stroke="none" />
  {:else if name === 'play'}
    <path d={paths[name] ?? ''} fill="currentColor" stroke="none" />
  {:else}
    <path d={paths[name] ?? ''} />
  {/if}
</svg>

<style>
  svg { display: block; flex: none; }
</style>
