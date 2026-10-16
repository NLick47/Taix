<script lang="ts">
  import Icon from './Icon.svelte';
  import AppPicker from './AppPicker.svelte';
  import {
    getRecord, listApps, listCategories, postCommand,
    type AppOption, type TaixPluginUiApi,
  } from './bridge';
  import {
    compileRules, defaultRule,
    type ReminderRule, type RuleType, type Repeat,
  } from './rules';
  import {
    initI18n, durationText, repeatText, ruleSummary, t, typeText, windowText,
    REPEATS, RULE_TYPES, WINDOWS,
  } from './i18n.svelte';

  // 宿主契约：api.values 是挂载时快照，编辑经 api.patch 回写宿主草稿
  // 可视化模式编辑 rules 并编译进 script，Lua 模式直改 script，server 只认 script
  let { api }: { api: TaixPluginUiApi } = $props();

  // 挂载一次的初始草稿：api 是宿主为本实例创建的稳定句柄，读初值不需要响应式绑定
  function initFromApi() {
    const values = api.values;
    // 与旧版编辑器相同的宽松兜底：非字符串一律 String 化，布尔取真值
    const mode = String(values.mode ?? '可视化规则') || '可视化规则';
    // 未配置即空：不预置规则，交给空态引导用户自己添加第一条
    const rules: ReminderRule[] = Array.isArray(values.rules)
      ? (values.rules as ReminderRule[])
      : [];
    // 缺字段时回写空值，与「空 rules = 不提醒」的服务端口径保持一致
    if (!Array.isArray(values.rules)) api.patch({ rules: [] });
    if (typeof values.mode !== 'string' || !values.mode) api.patch({ mode });
    return {
      initialMode: mode,
      initialRules: rules,
      initialSound: !!values.sound,
      initialScript: values.script == null ? '' : String(values.script),
    };
  }
  const { initialMode, initialRules, initialSound, initialScript } = initFromApi();

  // 宿主语言：挂载时读初值并订阅变化，宿主缺失 onLocale 时读一次即止
  $effect(() => {
    initI18n(api);
  });

  let mode = $state(initialMode);
  let sound = $state(initialSound);
  let script = $state(initialScript);
  let rules = $state<ReminderRule[]>(initialRules);

  function setMode(m: '可视化规则' | 'Lua 脚本') {
    mode = m;
    api.patch({ mode: m });
  }
  function setSound(v: boolean) {
    sound = v;
    api.patch({ sound: v });
  }
  function setRules(next: ReminderRule[]) {
    rules = next;
    api.patch({ rules: $state.snapshot(next) });
  }
  function setScript(next: string) {
    script = next;
    api.patch({ script: next });
  }

  // 分类与应用列表：选择器用它降低手输错误；拿不到就退回文本输入
  let categories = $state<string[]>([]);
  let appOptions = $state<AppOption[]>([]);
  $effect(() => {
    listCategories(api)
      .then((names) => {
        categories = names;
      })
      .catch(() => {});
    listApps(api)
      .then((rows) => {
        appOptions = rows;
      })
      .catch(() => {});
  });

  const categoryOptions = $derived<AppOption[]>(
    categories.map((name) => ({ name, alias: null, totalTime: 0 })),
  );

  const isVisual = $derived(mode === '可视化规则');

  const compiled = $derived(compileRules(rules));

  // 载入时的 script：可能是 server 默认脚本，也可能是用户手改过的。
  // 前端无法区分二者，所以只在「用户动过规则」时提示会覆盖，不指责用户改过什么
  const scriptAtLoad = initialScript;

  // 规则变化 → 编译进 script。载入时不动 script：手动改过的脚本不被静默覆盖
  // baseRules 用 null 区分「未初始化」与「空规则」：空数组的 JSON 是 "[]" 而非空串，
  // 拿空串当哨兵会让「加一条再删空」被误判成用户改过
  let baseRules: string | null = null;
  let rulesEdited = $state(false);
  $effect(() => {
    const json = JSON.stringify(rules);
    if (baseRules === null) {
      baseRules = json;
      return;
    }
    if (json === baseRules) return;
    if (isVisual) setScript(compiled);
    rulesEdited = true;
  });

  let scriptTouched = $state(false);
  const overwriteRisk = $derived(
    isVisual && rulesEdited && scriptAtLoad.trim() !== '' && scriptAtLoad !== compiled,
  );
  const scriptOverridden = $derived(scriptTouched && script !== compiled);

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
    setRules([...rules, { ...defaultRule(type), id: nextRuleId() }]);
  }
  function removeRule(i: number) {
    const next = [...rules];
    next.splice(i, 1);
    setRules(next);
  }
  function updateRule(i: number, patch: Partial<ReminderRule>) {
    const next = [...rules];
    next[i] = { ...next[i], ...patch };
    setRules(next);
  }

  // ---- Lua 编辑器（脚本模式）：行号 + 水平滚动 + 高亮 overlay ----
  const text = $derived(script);
  const lineCount = $derived(text.split('\n').length);
  const KEYWORDS =
    'if|then|else|elseif|end|return|local|function|for|while|do|nil|true|false|and|or|not|in|break|repeat|until';
  function highlightLua(src: string): string {
    const pattern = new RegExp(
      `(--\\[\\[[\\s\\S]*?\\]\\]|--.*?(?=\n|$))|("(?:[^"\\\\]|\\\\.)*"|'(?:[^'\\\\]|\\\\.)*')|\\b(${KEYWORDS})\\b|\\b(0x[0-9a-fA-F]+|\\d+(?:\\.\\d+)?)\\b|\\b(todayAppMinutes|todayCategoryMinutes|fmtDuration|fmtMinutes|ctx|string|math|table|pairs|tostring|tonumber)\\b`,
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
  interface DryCandidate {
    id: string; title: string; body: string; cooldownMinutes: number;
    cooldownLeftSeconds: number; oncePerDay: boolean; firedToday: boolean;
  }
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
      await postCommand(api, 'dry-run', { script, nonce });
      const deadline = Date.now() + 4000;
      while (Date.now() < deadline) {
        const rec = await getRecord<DryReply>(api, 'dryRun');
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
  function leftLabel(c: DryCandidate): string {
    if (c.oncePerDay && c.firedToday) return t('dry.leftNotified');
    return c.cooldownLeftSeconds > 0
      ? t('dry.leftTime', { dur: durationText(c.cooldownLeftSeconds / 60) })
      : t('dry.leftAvailable');
  }
  function rateLabel(c: DryCandidate): string {
    return c.oncePerDay
      ? t('dry.rateOnce')
      : t('dry.rateCooldown', { dur: durationText(c.cooldownMinutes) });
  }
  const dryOthers = $derived.by(() => {
    const result = dryResult;
    if (!result) return '';
    return result.candidates
      .map((c, i) => (i === result.pickedIndex ? null : `${c.title} ${leftLabel(c)}`))
      .filter((s): s is string => s !== null)
      .join(' · ');
  });

  const needMatch = $derived((t: RuleType) => t === 'category' || t === 'app');
</script>

<div class="re-grid">
  <div>
    <div class="re-bar">
      <div class="seg">
        <button class:active={isVisual} onclick={() => setMode('可视化规则')}>{t('bar.visual')}</button>
        <button class:active={!isVisual} onclick={() => setMode('Lua 脚本')}>Lua</button>
      </div>
      <label class="re-sound">
        {t('bar.sound')}
        <!-- svelte-ignore a11y_no_noninteractive_element_interactions -->
        <span class="switch">
          <input
            type="checkbox"
            checked={sound}
            oninput={(e) => setSound((e.target as HTMLInputElement).checked)}
          />
          <span class="track"></span>
        </span>
      </label>
      <span class="spacer"></span>
      <button onclick={dryRun} disabled={running}>
        <Icon name="play" size={11} />{running ? t('bar.running') : t('bar.dryRun')}
      </button>
    </div>

    {#if isVisual}
      <div class="card">
        {#if overwriteRisk || scriptOverridden}
          <div class="strip warn">
            <Icon name="alert" size={14} />
            {overwriteRisk ? t('warn.overwriteRisk') : t('warn.overwriteSession')}
          </div>
        {/if}
        {#if rules.length === 0}
          <div class="empty" style="box-shadow: none; border-radius: 0; padding: 34px 20px">
            <div class="glyph"><Icon name="bell" size={22} /></div>
            <div class="et">{t('rules.empty')}</div>
            <div class="eh">{t('rules.emptyHint')}</div>
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
                {#each RULE_TYPES as tp (tp)}
                  <option value={tp}>{typeText(tp)}</option>
                {/each}
              </select>
              <span class="rule-summary">{ruleSummary(r)}</span>
              <span class="spacer"></span>
              <button class="icon" onclick={() => removeRule(i)} title={t('rules.delete')}>
                <Icon name="x" size={12} />
              </button>
            </div>
            <div class="rule-grid" class:with-match={needMatch(r.type)}>
              {#if needMatch(r.type)}
                <div class="rg span2">
                  {#if r.type === 'category'}
                    <span class="rg-label">{t('rule.category')}</span>
                    <AppPicker
                      apps={categoryOptions}
                      value={r.match}
                      placeholder={t('picker.categoryPlaceholder')}
                      emptyHint={t('picker.categoryEmpty')}
                      onchange={(v) => updateRule(i, { match: v })}
                    />
                  {:else}
                    <span class="rg-label">{t('rule.appMatch')}</span>
                    <AppPicker
                      apps={appOptions}
                      value={r.match}
                      placeholder={t('picker.appPlaceholder')}
                      emptyHint={t('picker.appEmpty')}
                      onchange={(v) => updateRule(i, { match: v })}
                    />
                  {/if}
                </div>
              {/if}
              <div class="rg">
                <span class="rg-label">{t('rule.threshold')}</span>
                <input
                  type="number" min="1" value={r.minutes}
                  oninput={(e) => updateRule(i, { minutes: Number((e.target as HTMLInputElement).value) })}
                />
              </div>
              <div class="rg">
                <span class="rg-label">{t('rule.window')}</span>
                <select value={r.window} onchange={(e) => updateRule(i, { window: (e.target as HTMLSelectElement).value as never })}>
                  {#each WINDOWS as w (w)}
                    <option value={w}>{windowText(w)}</option>
                  {/each}
                </select>
              </div>
              <div class="rg">
                <span class="rg-label">{t('rule.cooldown')}</span>
                <input
                  type="number" min="1" value={r.cooldown}
                  disabled={r.repeat === 'once'}
                  title={r.repeat === 'once' ? t('rule.cooldownOnceHint') : t('rule.cooldownHint')}
                  oninput={(e) => updateRule(i, { cooldown: Number((e.target as HTMLInputElement).value) })}
                />
              </div>
              <div class="rg">
                <span class="rg-label">{t('rule.repeat')}</span>
                <select
                  value={r.repeat ?? 'repeat'}
                  onchange={(e) => updateRule(i, { repeat: (e.target as HTMLSelectElement).value as Repeat })}
                >
                  {#each REPEATS as rp (rp)}
                    <option value={rp}>{repeatText(rp)}</option>
                  {/each}
                </select>
              </div>
              <div class="rg" class:span2={needMatch(r.type)}>
                <span class="rg-label">{t('rule.title')}</span>
                <input
                  type="text" value={r.title} placeholder={t('rule.titlePlaceholder')}
                  oninput={(e) => updateRule(i, { title: (e.target as HTMLInputElement).value })}
                />
              </div>
            </div>
          </div>
        {/each}
        <div class="rule-add">
          <span class="faint">{t('rule.add')}</span>
          {#each RULE_TYPES as tp (tp)}
            <button class="small" onclick={() => addRule(tp)}>
              <Icon name="plus" size={11} />{typeText(tp)}
            </button>
          {/each}
        </div>
      </div>
    {:else}
      <div class="card">
        <div class="strip head">
          <Icon name="terminal" size={14} />
          <span>{t('lua.title')}</span>
          <span class="spacer"></span>
          <span class="faint">{t('lua.noSync')}</span>
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
                setScript((e.target as HTMLTextAreaElement).value);
                scriptTouched = true;
                syncScroll();
              }}
              onscroll={syncScroll}
              spellcheck="false"
            ></textarea>
          </div>
        </div>
        <div class="rule-foot">
          <span>{t('lua.sandbox')}</span>
          <span class="spacer"></span>
          <span class="tnum">{t('lua.lines', { n: lineCount })}</span>
          <span>{t('lua.every')}</span>
        </div>
      </div>
    {/if}

    {#if dryTimedOut}
      <div class="card dry-out err">
        <span class="dry-tag err">{t('dry.noResponse')}</span>
        <span class="dry-body">{t('dry.noResponseBody')}</span>
      </div>
    {:else if dryResult}
      <div class="card dry-out" class:err={!dryResult.ok}>
        {#if !dryResult.ok}
          <span class="dry-tag err">{t('dry.scriptError')}</span>
          <span class="dry-body">{dryResult.error}</span>
        {:else if dryResult.candidates.length === 0}
          <span class="dry-tag">{t('dry.noTrigger')}</span>
          <span class="dry-body faint">{t('dry.noTriggerBody')}</span>
        {:else if dryPicked}
          <span class="dry-tag ok">{t('dry.wouldNotify')}</span>
          <span class="dry-body">
            {dryPicked.title}{dryPicked.body ? t('dry.bodyJoin', { body: dryPicked.body }) : ''}
            <span class="faint">{t('dry.rateWrap', { rate: rateLabel(dryPicked) })}</span>
            {#if dryOthers}
              <div class="faint dry-note">{t('dry.matchedNote', { n: dryResult.candidates.length, others: dryOthers })}</div>
            {/if}
          </span>
        {:else}
          <span class="dry-tag">{t('dry.heldBack')}</span>
          <span class="dry-body">{t('dry.heldBackBody', { n: dryResult.candidates.length, others: dryOthers })}</span>
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
    grid-template-columns: repeat(5, minmax(0, 1fr));
    gap: 9px;
    padding: 10px 14px 13px;
  }
  .rule-grid.with-match { grid-template-columns: repeat(6, minmax(0, 1fr)); }
  .rule-grid .span2 { grid-column: span 2; }
  .rule-grid input:disabled { opacity: 0.45; cursor: not-allowed; }
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
