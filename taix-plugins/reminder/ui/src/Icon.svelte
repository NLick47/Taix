<script lang="ts">
  // 最小内联图标集，16×16 网格，stroke 继承 currentColor，零依赖
  // 只收录本 UI 用到的图形，新增图标保持 1.5px 圆头笔画风格
  let { name, size = 16 }: { name: string; size?: number } = $props();

  const paths: Record<string, string> = {
    plus: 'M8 3v10M3 8h10',
    bell: 'M8 2.5c-2 0-3.2 1.4-3.2 3.4 0 2.8-1 3.4-1.3 4.1h9c-.3-.7-1.3-1.3-1.3-4.1 0-2-1.2-3.4-3.2-3.4zM6.8 12.5a1.3 1.3 0 0 0 2.4 0',
    terminal: 'M3 4.5L6.5 8 3 11.5M8 11.5h5',
    check: 'M3 8.5l3.2 3L13 4.5',
    alert: 'M8 2.8L14 13H2zM8 6.8v2.9M8 11.4v.4',
    play: 'M5.5 3.5l7 4.5-7 4.5z',
    x: 'M4 4l8 8M12 4l-8 8',
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
  {#if name === 'play'}
    <path d={paths[name] ?? ''} fill="currentColor" stroke="none" />
  {:else}
    <path d={paths[name] ?? ''} />
  {/if}
</svg>

<style>
  svg { display: block; flex: none; }
</style>
