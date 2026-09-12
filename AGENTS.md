# AGENTS.md

## What this is

Taix is a software/website usage time tracker (fork of [Tai](https://github.com/Planshit/Tai)). Polyglot monorepo — no shared workspace config across languages.

## Architecture

Three long-running processes managed by taix-shell (watchdog/tray):

- **taix-shell** — tray icon, spawns & restarts server + monitor. Never start server/monitor manually.
- **taix-server** — Rust HTTP+WebSocket API, writes to SQLite at `127.0.0.1:37091`.
- **taix-monitor** — tracks focus/sleep, talks to server via named pipes.

Client (Avalonia UI) connects to server. Browser extension connects via WebSocket on port `8908`.

Override server port: env var `TAIX_SERVER` (e.g. `http://127.0.0.1:37100`).

## Languages & toolchains

| Component | Language | Build |
| --- | --- | --- |
| Taix.Client | C# / .NET 10 / Avalonia | `dotnet publish` with AOT |
| Taix.PageState.Generator | C# / netstandard2.0 | Roslyn source generator (auto-referenced) |
| taix-server, taix-shell, taix-logging | Rust | `cargo build` per crate |
| taix-monitor-windows | Rust | `cargo build` |
| taix-monitor-macos | Swift 5.9 / SPM | `swift build` |
| taix-installer | Rust + shell scripts | `cargo build` + `build-installer.sh` / `build-dmg.sh` |
| taix-installer-pack-payload | Rust | `cargo build` (standalone, no UAC manifest) |
| taix-browser-extension | TypeScript / esbuild | `npm ci && npm run build` |

## Key gotchas

- **No Cargo workspace.** Each Rust crate has its own `Cargo.lock` and is built independently from its directory.
- **Never start taix-server or taix-monitor directly.** taix-shell is the watchdog — manual processes miss `--sleep-watch` params and aren't supervised.
- **AOT publish for client.** The release dotnet publish command is long with many flags — see `.github/workflows/release.yml` for the exact invocation. Key flags: `-p:PublishAot=true` plus many trimming/disable flags.
- **Static linking (win-x64 AOT).** SkiaSharp, ANGLE, and HarfBuzz are statically linked with `DirectPInvoke` and native linker args in `Taix.Client.csproj:39-53`. This is win-x64 only.
- **Source generator.** `Taix.PageState.Generator` is a Roslyn analyzer referenced by `Taix.Client.csproj` — don't delete or rename without updating the project reference.
- **Browser extension builds per-browser.** `npm run build` chains chrome, firefox, safari. Use `npm run build:chrome` for a single target.
- **Extension manifest versions** are synced from `package.json` during CI — manifests are not the source of truth for version numbers.

## Build commands

```bash
# .NET client (AOT, from repo root)
dotnet publish Taix.Client/Taix.Client.csproj -c Release -r win-x64 --self-contained true \
  -p:PublishAot=true -p:DebugType=None -p:DebugSymbols=false \
  -p:IlcGenerateStackTraceData=false -p:DebuggerSupport=false -p:StartupHookSupport=false \
  -p:StackTraceSupport=false -p:HttpActivityPropagationSupport=false \
  -p:MetadataUpdaterSupport=false -p:EnableUnsafeUTF7Encoding=false \
  -p:EventSourceSupport=false -p:MetricsSupport=false -p:UseSystemResourceKeys=true \
  -p:Http3Support=false -p:AssemblyTitle=Taix -p:Product=Taix \
  -o ./output/x64/dotnet

# Rust crates (from crate directory)
cd taix-server && cargo build --release
cd taix-shell && cargo build --release
cd taix-monitor-windows && cargo build --release
cd taix-installer-pack-payload && cargo build --release

# Swift macOS monitor
cd taix-monitor-macos && swift build -c release --arch arm64

# Browser extension
cd taix-browser-extension && npm ci && npm run build
```

## CI / release

Triggered by `v*` tags (`.github/workflows/release.yml`). Builds:

- Windows x64: all Rust crates + .NET AOT client + installer + ZIP
- macOS arm64: taix-server + taix-shell (Rust) + taix-monitor (Swift) + .NET AOT client + DMG
- Browser extension: chrome only (Ubuntu runner)
- Release notes from `release_notes.txt` with `{version}` / `{ext_version}` placeholders.

## Code style

- `.editorconfig` enforced: UTF-8, LF line endings, trailing whitespace trimmed.
- C# files: 4-space indent, `using` directives outside namespace.
- XAML/AXAML, csproj, JSON, YAML: 2-space indent.
- Rust: standard `cargo fmt`.
- TypeScript: strict mode, ES2020 target.

## Runtime defaults

- Server: `127.0.0.1:37091` (HTTP), `8908` (WebSocket for extension)
- SQLite DB in platform-specific app data dir (gitignored as `*.db`)
- Logs: `taix-server.YYYY-MM-DD.log` — Windows: `<install>/Logs/`, macOS: `/Applications/TaixTools/Logs/`
