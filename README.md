# Token HUD

Token HUD is a local-first desktop companion that keeps AI context load and token flow visible without repeatedly opening status commands. The current prototype supports Codex on Windows and presents the six most recently active chats in a compact HUD, with deeper inspection in an expanded view.

Token HUD is being developed under the Fluent Studio umbrella. Its long-term direction includes multiple AI applications, macOS support, deeper customization, and potential commercial distribution.

## What the Codex prototype reads

- Metadata-only columns from `%USERPROFILE%\.codex\state_*.sqlite`.
- `token_count` events from the recent tail of relevant Codex rollout JSONL files.
- The local session index for current user-facing chat names.

The catalog query deliberately excludes `first_user_message` and `preview`. Rollout lines are filtered by event type; message records are never deserialized, stored, displayed, or logged. Historical inactive subagents contribute their catalog token total without reopening their rollout. Open subagents become actively monitored when their metadata has changed within 30 minutes and resume automatically on their next update.

## Metric semantics

- **Context Load:** latest call's `total_tokens - reasoning_output_tokens`, matching Codex's context-window accounting.
- **Account Quota:** remaining five-hour and weekly windows, classified by the duration Codex reports rather than by primary/secondary field position.
- **Context Velocity:** median of the eight most recent positive context changes, plus an observed tokens-per-minute rate.
- **Token Spike:** the latest increase is at least 8,000 tokens and at least 2.5 times the recent median.
- **Token Flow:** exact latest-exchange input and output accounting. Input distinguishes cached, cache-write, and new tokens; output distinguishes reasoning and visible output.
- **Context Origin:** a broad, explicitly estimated distinction between instruction-like input and working context. Codex does not expose exact source attribution.

The default context budget is 150,000 tokens. Healthy is below 75%, Caution is 75–89.999%, Critical is 90–99.999%, and Over Budget is 100% or higher.

## Architecture

- `TokenHud.Core`: privacy-bounded catalog adapter, rollout reader, cache, budget analysis, tree aggregation, settings, and diagnostics.
- `TokenHud.App`: WPF HUD, Windows theme integration, tray lifecycle, native app notifications with a tray fallback, startup registration, and accessibility behavior.
- `TokenHud.SelfTest`: dependency-light parser, boundary, signal, live-catalog, and live-snapshot checks.

The data-source adapter is isolated because Codex's local storage is an implementation detail and future providers will require different integrations. A source-health status is always visible in Expanded View.

## Product documentation

- `PRODUCT.md` defines the product purpose, users, principles, and longer horizon.
- `DESIGN.md` records interaction and visual decisions.
- `CONTEXT.md` defines the product's canonical language.
- `docs/screenshots/` preserves visual references from the prototype.

## Build and verify

```powershell
dotnet restore .\TokenHud.slnx
dotnet build .\TokenHud.slnx --no-restore
dotnet .\tests\TokenHud.SelfTest\bin\Debug\net10.0\TokenHud.SelfTest.dll
dotnet publish .\src\TokenHud.App\TokenHud.App.csproj -c Release -r win-x64 --self-contained true
```

## Current boundary

The current Codex integration is local-only and read-only. Codex exposes `thread/compact/start` through its app-server protocol, but the desktop app does not publish a supported control endpoint that this companion can safely attach to. A future compaction action belongs behind a provider capability interface and must fail closed when a supported active-server connection is unavailable.
