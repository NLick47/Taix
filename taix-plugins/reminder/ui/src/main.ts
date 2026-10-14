import { mount as svelteMount, unmount } from 'svelte';
import App from './App.svelte';
import type { TaixPluginUiApi } from './bridge';

/** 配置 UI 入口：宿主动态 import 本模块后调用，返回卸载函数 */
export function mount(el: HTMLElement, api: TaixPluginUiApi): () => void {
  const app = svelteMount(App, { target: el, props: { api } });
  return () => void unmount(app);
}
