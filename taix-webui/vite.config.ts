import { defineConfig } from 'vite';
import { svelte } from '@sveltejs/vite-plugin-svelte';

export default defineConfig({
  plugins: [svelte()],
  base: '/ui/',
  build: { target: 'es2020' },
  server: {
    proxy: {
      // dev 下浏览器与 /ui 同源（:5173），但代理会把 Origin 原样转发，
      // 而 server 的写接口只放行 127.0.0.1/localhost:<端口>（origin_check）→ 去掉它
      '/api': {
        target: 'http://127.0.0.1:37091',
        configure: (proxy) => {
          proxy.on('proxyReq', (proxyReq) => proxyReq.removeHeader('origin'));
        },
      },
    },
  },
});
