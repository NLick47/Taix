import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  // css: 'injected' 让组件样式随 JS 注入，宿主只加载一个模块文件
  plugins: [svelte({ compilerOptions: { css: 'injected' } })],
  build: {
    // 单文件 ESM 产出，宿主对 /api/plugins/reminder/ui/config.js 做动态 import
    lib: {
      entry: 'src/main.ts',
      formats: ['es'],
      fileName: () => 'config.js',
    },
    target: 'es2020',
    outDir: 'dist',
    emptyOutDir: true,
  },
});
