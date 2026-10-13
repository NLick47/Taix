<script lang="ts">
  import Icon from '../lib/Icon.svelte';
  import AppPicker from '../lib/AppPicker.svelte';
  import { api } from '../lib/api';
  import type { AppOption } from '../lib/types';
  import {
    compileRules, defaultRule, ruleSummary, WINDOW_LABEL, RULE_TYPE_LABEL,
    type ReminderRule, type RuleType,
  } from '../lib/rules';

  // values 由 PluginConfig 持有（$state proxy）。可视化模式编辑 rules，规则
  // 变化时编译进 values.script；Lua 模式直改 values.script。server 只认 script。
  let { values }: { values: Record<string, unknown> } = $props();

  // 分类与应用列表：选择器用它降低手输错误；拿不到就退回文本输入
  let categories = $state<string[]>([]);
  let appOptions = $state<AppOption[]>([]);
  $effect(() => {
    api.listCategories()
      .then((rows) => {
        categories = rows.map((c) => c.name ?? '').filter(Boolean);
      })
      .catch(() => {});
    api.listApps()
      .then((rows) => {
        appOptions = rows.flatMap((a) =>
          a.name ? [{ name: a.name, alias: a.alias, totalTime: a.totalTime ?? 0 }] : [],
        );
      })
      .catch(() => {});
  });

  function catOptions(current: string): { value: string; label: string }[] {
    const options = categories.map((name) => ({ value: name, label: name }));
    if (current && !categories.includes(current)) {
      options.unshift({ value: current, label: `${current}（分类不存在）` });
    }
    return options;
  }

  const mode = $derived(String(values.mode ?? '可视化规则'));
  const isVisual = $derived(mode === '可视化规则');
  const rules = $derived.by(() => {
    if (!Array.isArray(values.rules)) return [];
    return values.rules as unknown as ReminderRule[];
  });

  // 首次载入：无规则时播种默认（server manifest default 已带，这里兜底）
  $effect(() => {
    if (!Array.isArray(values.rules)) {
      values.rules = [
        { id: 'r1', type: 'continuous', minutes: 60, match: '', window: 'all',
          title: '该休息一下了', cooldown: 30 },
      ];
    }
    if (!values.mode) values.mode = '可视化规则';
  });

  const compiled = $derived(compileRules(rules));

  // 载入时的 script：可能是 server 默认脚本，也可能是用户手改过的。
  // 前端无法区分二者，所以只在「用户动过规则」时提示会覆盖，不指责用户改过什么
  let scriptAtLoad = '';
  let captured = false;
  $effect(() => {
    if (captured || Object.keys(values).length === 0) return;
    scriptAtLoad = String(values.script ?? '');
    captured = true;
  });

  // 规则变化 → 编译进 script。载入时不动 script：手动改过的脚本不被静默覆盖
  let lastRulesJson = '';
  let baseRulesJson = '';
  let rulesEdited = $state(false);
  $effect(() => {
    const json = JSON.stringify(rules);
    if (lastRulesJson === '') {
      lastRulesJson = json;
    } else if (json !== lastRulesJson) {
      lastRulesJson = json;
      if (isVisual) values.script = compiled;
    }
    // 播种完成后的规则定格为基线，之后的变化才算用户改过
    if (baseRulesJson === '' && json !== '[]') {
      baseRulesJson = json;
    } else if (baseRulesJson !== '' && json !== baseRulesJson) {
      rulesEdited = true;
    }
  });

  let scriptTouched = $state(false);
  const overwriteRisk = $derived(
    isVisual && rulesEdited && scriptAtLoad.trim() !== '' && scriptAtLoad !== compiled,
  );
  const scriptOverridden = $derived(scriptTouched && String(values.script ?? '') !== compiled);

  // 新规则生成稳定 id：取现有 r<N> 的最大序号 + 1
  function nextRuleId(): string {
    let max = 0;
    rules.forEach((r, i) => {
      const m = /^r(\d+)$/.exec(r.id ?? `r${i + 1}`);
      if (m) max = Math.max(max, Number(m[1]));
    });
    return `r${max + 1}`;
  }
  function addRule(type: RuleType) {
    values.rules = [...rules, { ...defaultRule(type), id: nextRuleId() }];
  }
  function removeRule(i: number) {
    const next = [...rules];
    next.splice(i, 1);
    values.rules = next;
  }
  function updateRule(i: number, patch: Partial<ReminderRule>) {
    const next = [...rules];
    next[i] = { ...next[i], ...patch };
    values.rules = next;
  }

  // ---- Lua 编辑器（脚本模式）：行号 + 水平滚动 + 高亮 overlay ----
  const text = $derived(String(values.script ?? ''));
  const lineCount = $derived(text.split('\n').length);
  const KEYWORDS =
    'if|then|else|elseif|end|return|local|function|for|while|do|nil|true|false|and|or|not|in|break|repeat|until';
  function highlightLua(src: string): string {
    const pattern = new RegExp(
      `(--\\[\\[[\\s\\S]*?\\]\\]|--.*?(?=\n|$))|("(?:[^"\\\\]|\\\\.)*"|'(?:[^'\\\\]|\\\\.)*')|\\b(${KEYWORDS})\\b|\\b(0x[0-9a-fA-F]+|\\d+(?:\\.\\d+)?)\\b|\\b(todayAppMinutes|todayCategoryMinutes|fmtHMS|ctx|string|math|table|pairs|tostring|tonumber)\\b`,
      'g',
    );
    let out = '';
    let last = 0;
    for (const m of src.matchAll(pattern)) {
      const idx = m.index ?? 0;
      out += escapeHtml(src.slice(last, idx));
      const [full, comment, str, kw, num, lib] = m;
      const cls = comment ? 'c' : str ? 's' : kw ? 'k' : num ? 'n' : lib ? 'f' : '';
      out += `<span class="hl-${cls}">${escapeHtml(full)}</span>`;
      last = idx + full.length;
    }
    out += escapeHtml(src.slice(last));
    return out + '\n';
  }
  function escapeHtml(s: string): string {
    return s.replace(/&/g, '&amp;').replace(/</g, '&lt;');
  }
  const highlighted = $derived(highlightLua(text));
  let ta: HTMLTextAreaElement | undefined = $state();
  let pre: HTMLPreElement | undefined = $state();
  let gutter: HTMLDivElement | undefined = $state();
  function syncScroll() {
    if (ta && pre) {
      pre.scrollTop = ta.scrollTop;
      pre.scrollLeft = ta.scrollLeft;
    }
    if (ta && gutter) {
      gutter.scrollTop = ta.scrollTop;
    }
  }

  // ---- 试运行：命令 + 记录往返 ----
  interface DryCandidate { id: string; title: string; body: string; cooldownMinutes: number; cooldownLeftSeconds: number }
  interface DrySnapshot {
    continuousSeconds: number; todayTotalSeconds: number; currentProcess: string;
    hour: number; weekday: number;
    apps: { name: string; seconds: number }[];
    categories: { name: string; seconds: number }[];
  }
  // 应答信封由 SDK 定死：{nonce, ok, error, data}（见 taix-plugin-sdk/src/request.rs）
  interface DryReply {
    nonce: string;
    ok: boolean;
    error: string | null;
    data: { candidates: DryCandidate[]; pickedIndex: number | null; snapshot: DrySnapshot | null } | null;
  }
  let dryResult = $state<{ nonce: string; ok: boolean; error: string | null; candidates: DryCandidate[]; pickedIndex: number | null; snapshot: DrySnapshot | null } | null>(null);
  let dryTimedOut = $state(false);
  let running = $state(false);

  // 生成 nonce 后发 dry-run 命令，再轮询记录直到匹配本次 nonce（区分新旧结果）
  async function dryRun() {
    running = true;
    dryResult = null;
    dryTimedOut = false;
    const nonce = Math.random().toString(36).slice(2);
    try {
      await api.postCommand('reminder', 'dry-run', { script: String(values.script ?? ''), nonce });
      const deadline = Date.now() + 4000;
      while (Date.now() < deadline) {
        const rec = (await api.getRecord('reminder', 'dryRun')) as DryReply | null;
        if (rec && rec.nonce === nonce) {
          dryResult = {
            nonce,
            ok: rec.ok,
            error: rec.error,
            candidates: rec.data?.candidates ?? [],
            pickedIndex: rec.data?.pickedIndex ?? null,
            snapshot: rec.data?.snapshot ?? null,
          };
          running = false;
          return;
        }
        await new Promise((r) => setTimeout(r, 150));
      }
      dryTimedOut = true;
    } catch (e) {
      dryResult = { nonce, ok: false, error: String(e), candidates: [], pickedIndex: null, snapshot: null };
    }
    running = false;
  }

  const dryPicked = $derived(
    dryResult && dryResult.pickedIndex != null
      ? dryResult.candidates[dryResult.pickedIndex] ?? null
      : null,
  );
  function leftLabel(seconds: number): string {
    return seconds > 0 ? `剩余 ${Math.ceil(seconds / 60)} 分钟` : '也可提醒';
  }
  const dryOthers = $derived.by(() => {
    const result = dryResult;
    if (!result) return '';
    return result.candidates
      .map((c, i) => (i === result.pickedIndex ? null : `${c.title} ${leftLabel(c.cooldownLeftSeconds)}`))
      .filter((s): s is string => s !== null)
      .join(' · ');
  });

  const needMatch = $derived((t: RuleType) => t === 'category' || t === 'app');
</script>

<div class="re-grid">
  <div>
    <div class="re-bar">
      <div class="seg">
        <button class:active={isVisual} onclick={() => (values.mode = '可视化规则')}>可视化</button>
        <button class:active={!isVisual} onclick={() => (values.mode = 'Lua 脚本')}>Lua</button>
      </div>
      <label class="re-sound">
        提示音
        <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
        <span class="switch">
          <input
            type="checkbox"
            checked={!!values.sound}
            oninput={(e) => (values.sound = (e.target as HTMLInputElement).checked)}
          />
          <span class="track"></span>
        </span>
      </label>
      <span class="spacer"></span>
      <button onclick={dryRun} disabled={running}>
        <Icon name="play" size={11} />{running ? '运行中…' : '试运行'}
      </button>
    </div>

    {#if isVisual}
      <div class="card">
        {#if overwriteRisk || scriptOverridden}
          <div class="strip warn">
            <Icon name="alert" size={14} />
            {overwriteRisk
              ? '已存的 Lua 脚本与当前规则编译结果不同，保存会用规则覆盖该脚本'
              : 'Lua 脚本有本次会话内的手动修改，保存可视化规则会覆盖这些修改'}
          </div>
        {/if}
        {#if rules.length === 0}
          <div class="empty" style="box-shadow: none; border-radius: 0; padding: 34px 20px">
            <div class="glyph"><Icon name="bell" size={22} /></div>
            <div class="et">还没有提醒规则</div>
            <div class="eh">从下方添加第一条规则</div>
          </div>
        {/if}
        {#each rules as r, i (i)}
          <div class="rule">
            <div class="rule-head">
              <span class="rule-idx">{i + 1}</span>
              <select
                style="width: auto; height: 27px"
                value={r.type}
                onchange={(e) => updateRule(i, { type: (e.target as HTMLSelectElement).value as RuleType })}
              >
                {#each Object.entries(RULE_TYPE_LABEL) as [t, label] (t)}
                  <option value={t}>{label}</option>
                {/each}
              </select>
              <span class="rule-summary">{ruleSummary(r)}</span>
              <span class="spacer"></span>
              <button class="icon" onclick={() => removeRule(i)} title="删除规则">
                <Icon name="x" size={12} />
              </button>
            </div>
            <div class="rule-grid" class:with-match={needMatch(r.type)}>
              {#if needMatch(r.type)}
                <div class="rg span2">
                  {#if r.type === 'category'}
                    <span class="rg-label">分类</span>
                    {#if categories.length}
                      <select
                        value={r.match}
                        onchange={(e) => updateRule(i, { match: (e.target as HTMLSelectElement).value })}
                      >
                        <option value="">选择分类</option>
                        {#each catOptions(r.match) as c (c.value)}
                          <option value={c.value}>{c.label}</option>
                        {/each}
                      </select>
                    {:else}
                      <input
                        type="text"
                        value={r.match}
                        placeholder="暂无分类数据，可直接输入分类名"
                        oninput={(e) => updateRule(i, { match: (e.target as HTMLInputElement).value })}
                      />
                    {/if}
                  {:else}
                    <span class="rg-label">应用匹配</span>
                    <AppPicker
                      apps={appOptions}
                      value={r.match}
                      onchange={(v) => updateRule(i, { match: v })}
                    />
                  {/if}
                </div>
              {/if}
              <div class="rg">
                <span class="rg-label">时长</span>
                <input
                  type="number" min="1" value={r.minutes}
                  oninput={(e) => updateRule(i, { minutes: Number((e.target as HTMLInputElement).value) })}
                />
              </div>
              <div class="rg">
                <span class="rg-label">时段</span>
                <select value={r.window} onchange={(e) => updateRule(i, { window: (e.target as HTMLSelectElement).value as never })}>
                  {#each Object.entries(WINDOW_LABEL) as [w, label] (w)}
                    <option value={w}>{label}</option>
                  {/each}
                </select>
              </div>
              <div class="rg">
                <span class="rg-label">冷却</span>
                <input
                  type="number" min="1" value={r.cooldown}
                  oninput={(e) => updateRule(i, { cooldown: Number((e.target as HTMLInputElement).value) })}
                />
              </div>
              <div class="rg" class:span2={needMatch(r.type)}>
                <span class="rg-label">通知标题</span>
                <input
                  type="text" value={r.title} placeholder="留空自动生成"
                  oninput={(e) => updateRule(i, { title: (e.target as HTMLInputElement).value })}
                />
              </div>
            </div>
          </div>
        {/each}
        <div class="rule-add">
          <span class="faint">添加</span>
          {#each Object.entries(RULE_TYPE_LABEL) as [t, label] (t)}
            <button class="small" onclick={() => addRule(t as RuleType)}>
              <Icon name="plus" size={11} />{label}
            </button>
          {/each}
        </div>
      </div>
    {:else}
      <div class="card">
        <div class="strip head">
          <Icon name="terminal" size={14} />
          <span>提醒规则 · Lua</span>
          <span class="spacer"></span>
          <span class="faint">手动修改后可视化编辑器不会同步</span>
        </div>
        <div class="code-editor">
          <div class="gutter mono" bind:this={gutter} aria-hidden="true">
            {#each Array.from({ length: lineCount }) as _, i (i)}<span>{i + 1}</span>{/each}
          </div>
          <div class="code-area">
            <pre bind:this={pre} aria-hidden="true">{@html highlighted}</pre>
            <textarea
              bind:this={ta}
              value={text}
              oninput={(e) => {
                values.script = (e.target as HTMLTextAreaElement).value;
                scriptTouched = true;
                syncScroll();
              }}
              onscroll={syncScroll}
              spellcheck="false"
            ></textarea>
          </div>
        </div>
        <div class="rule-foot">
          <span>Lua 5.4 沙箱（无 io / os）</span>
          <span class="spacer"></span>
          <span class="tnum">{lineCount} 行</span>
          <span>每 5 秒执行</span>
        </div>
      </div>
    {/if}

    {#if dryTimedOut}
      <div class="card dry-out err">
        <span class="dry-tag err">无响应</span>
        <span class="dry-body">提醒插件未响应，确认它已启用并在运行</span>
      </div>
    {:else if dryResult}
      <div class="card dry-out" class:err={!dryResult.ok}>
        {#if !dryResult.ok}
          <span class="dry-tag err">脚本错误</span>
          <span class="dry-body">{dryResult.error}</span>
        {:else if dryResult.candidates.length === 0}
          <span class="dry-tag">不触发</span>
          <span class="dry-body faint">当前数据下没有任何规则命中</span>
        {:else if dryPicked}
          <span class="dry-tag ok">会提醒</span>
          <span class="dry-body">
            {dryPicked.title}{dryPicked.body ? `：${dryPicked.body}` : ''}
            <span class="faint">（冷却 {dryPicked.cooldownMinutes} 分钟）</span>
            {#if dryOthers}
              <div class="faint dry-note">命中 {dryResult.candidates.length} 条，其余：{dryOthers}</div>
            {/if}
          </span>
        {:else}
          <span class="dry-tag">冷却中</span>
          <span class="dry-body">命中的 {dryResult.candidates.length} 条都在冷却：{dryOthers}</span>
        {/if}
      </div>
    {/if}
  </div>
</div>

<style>
  .re-grid {
    display: block;
  }

  .re-bar {
    display: flex; align-items: center; gap: 9px;
    margin-bottom: 11px;
  }
  /* 「可视化 / Lua」是规则编辑方式，属次级控件：
     缩小尺寸与上方「概览 / 配置」页面级切换拉开权重 */
  .re-bar .seg { padding: 1px; border-radius: 8px; }
  .re-bar .seg button {
    height: 22px; padding: 0 10px;
    font-size: 11.5px; font-weight: 400; border-radius: 6px;
  }
  .re-sound {
    display: inline-flex; align-items: center; gap: 7px;
    height: 26px; padding: 0 10px; border-radius: 8px;
    background: var(--card); box-shadow: var(--sh-1);
    font-size: 11.5px; color: var(--text-2); cursor: pointer;
  }

  .strip {
    display: flex; align-items: center; gap: 8px;
    padding: 9px 14px; font-size: 12.5px;
    border-bottom: 0.5px solid var(--line);
  }
  .strip.warn { background: var(--warn-quiet); color: var(--warn); }
  .strip.head { background: transparent; color: var(--text-2); }

  .rule { border-top: 0.5px solid var(--line); }
  .rule:first-of-type { border-top: 0; }
  .rule-head { display: flex; align-items: center; gap: 9px; padding: 11px 14px 0; }
  .rule-idx {
    width: 20px; height: 20px; border-radius: 6px;
    display: grid; place-items: center; flex: none;
    background: var(--accent-quiet); color: var(--accent);
    font-size: 11px; font-weight: 500;
  }
  .rule-summary {
    font-size: 12px; color: var(--text-3);
    overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
  }

  /* 均等四列：需要大空间的「应用匹配」跨两列，不再把窄列分给它 */
  .rule-grid {
    display: grid;
    grid-template-columns: repeat(4, minmax(0, 1fr));
    gap: 9px;
    padding: 10px 14px 13px;
  }
  .rule-grid.with-match { grid-template-columns: repeat(6, minmax(0, 1fr)); }
  .rule-grid .span2 { grid-column: span 2; }
  @media (max-width: 1180px) {
    .rule-grid, .rule-grid.with-match { grid-template-columns: repeat(4, minmax(0, 1fr)); }
  }
  @media (max-width: 680px) {
    .rule-grid, .rule-grid.with-match { grid-template-columns: repeat(2, minmax(0, 1fr)); }
  }
  .rg-label { display: block; font-size: 11px; color: var(--text-3); margin-bottom: 4px; }

  .rule-add {
    display: flex; align-items: center; gap: 7px; flex-wrap: wrap;
    padding: 11px 14px;
    border-top: 0.5px solid var(--line);
  }

  .rule-foot {
    display: flex; align-items: center; gap: 13px;
    padding: 8px 14px;
    border-top: 0.5px solid var(--line);
    font-size: 11px; color: var(--text-3);
  }

  .dry-out {
    margin-top: 11px; padding: 10px 14px;
    display: flex; align-items: baseline; gap: 10px;
    font-size: 12.5px;
  }
  .dry-tag {
    flex: none; font-size: 11px; font-weight: 500;
    padding: 2px 8px; border-radius: 6px;
    background: var(--card-inset); color: var(--text-2);
  }
  .dry-tag.ok { background: var(--ok-quiet); color: var(--ok-deep); }
  .dry-tag.err { background: var(--err-quiet); color: var(--err); }
  .dry-body { min-width: 0; overflow-wrap: anywhere; }
  .dry-note { margin-top: 4px; }

  /* 代码编辑器：行号槽 + 水平滚动 */
  .code-editor {
    display: flex;
    background: var(--code-bg);
    font-family: ui-monospace, 'SF Mono', Menlo, Consolas, monospace;
    font-size: 12px; line-height: 1.7;
  }
  .gutter {
    flex: none; width: 38px; text-align: right;
    padding: 12px 0;
    color: var(--text-4); user-select: none;
    overflow: hidden;
    border-right: 0.5px solid var(--line);
    max-height: 420px;
  }
  .gutter span { display: block; padding-right: 9px; }
  .code-area { position: relative; flex: 1; min-width: 0; }
  .code-area pre,
  .code-area textarea {
    margin: 0; padding: 12px 14px;
    font: inherit; line-height: inherit;
    white-space: pre; tab-size: 2;
  }
  .code-area pre {
    position: absolute; inset: 0;
    overflow: hidden; pointer-events: none; color: var(--text);
  }
  .code-area textarea {
    position: relative; display: block; width: 100%;
    height: auto;
    border: 0; background: transparent; color: transparent;
    caret-color: var(--accent);
    resize: vertical; min-height: 360px; max-height: 420px;
    overflow: auto; outline: none;
  }
  .code-area textarea:focus { box-shadow: inset 0 0 0 1.5px var(--accent); }
</style>
