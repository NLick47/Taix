// 主题选择（auto 跟随系统 / light / dark），localStorage 持久化。
// 首帧由 index.html 内联脚本先行设置 data-theme，这里只负责切换。
export type ThemeChoice = 'auto' | 'light' | 'dark';

function initial(): ThemeChoice {
  // localStorage 可能被禁用/受限：读失败只回退默认，不能让模块顶层抛错白屏
  try {
    const v = localStorage.getItem('taix-theme');
    return v === 'light' || v === 'dark' ? v : 'auto';
  } catch {
    return 'auto';
  }
}

export const theme = $state({ choice: initial() });

const ORDER: ThemeChoice[] = ['auto', 'light', 'dark'];
export const THEME_LABEL: Record<ThemeChoice, string> = {
  auto: '跟随系统',
  light: '浅色',
  dark: '深色',
};

export function cycleTheme() {
  theme.choice = ORDER[(ORDER.indexOf(theme.choice) + 1) % ORDER.length];
  try {
    localStorage.setItem('taix-theme', theme.choice);
  } catch {
    /* 写失败不影响本次切换 */
  }
  apply();
}

function apply() {
  const root = document.documentElement;
  if (theme.choice === 'auto') delete root.dataset.theme;
  else root.dataset.theme = theme.choice;
}
