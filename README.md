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

Aaron's default Context Budget is 150,000 tokens. His Smart Zones are Healthy below 100,000, Caution from 100,000, Critical from 125,000, and Over Budget from 150,000. Product profiles remain configurable for other users and providers.

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
```

## Private Windows x64 package

On Windows, use the .NET 10 SDK to build the self-contained WPF package:

```powershell
.\scripts\Build-TokenHudPackage.ps1
```

The build writes the ZIP, the ZIP's SHA-256 checksum, and three installer scripts to `artifacts\windows-x64`. Keep those five files together when sharing the package privately. The checksum verifies the ZIP against the accompanying digest. Obtain both from the same trusted build. The application is unsigned.

Open PowerShell in that package directory and install for your Windows account:

```powershell
.\Install-TokenHud.ps1
```

Launch **Token HUD** from the Start menu. The installer places the app in `%LOCALAPPDATA%\Programs\TokenHUD\app`. The app includes its .NET runtime and requires no administrator access.

To update, exit Token HUD from its tray menu, then run the installer from the new package directory. The installer checks the ZIP and stages a complete app before replacing the previous copy. To uninstall, exit the app and run `.\Uninstall-TokenHud.ps1` from the package directory. Uninstall preserves settings, metrics history, and diagnostics in `%LOCALAPPDATA%\TokenHUD`.

For a disposable smoke check, run the following from the package directory. Close the demo before uninstalling.

```powershell
$smokeRoot = Join-Path ([IO.Path]::GetTempPath()) ("TokenHUD-smoke-" + [Guid]::NewGuid().ToString('N'))
.\Install-TokenHud.ps1 -InstallRoot $smokeRoot -NoShellIntegration
& "$smokeRoot\app\TokenHUD.exe" --demo
.\Uninstall-TokenHud.ps1 -InstallRoot $smokeRoot -NoShellIntegration
```

## Current boundary

The Expanded inspector also has an **AI sources** panel for explicitly connected local metrics feeds. See [provider telemetry](docs/provider-telemetry.md) for the schema and what each field means.

The current Codex integration is local-only and read-only. Codex exposes `thread/compact/start` through its app-server protocol, but the desktop app does not publish a supported control endpoint that this companion can safely attach to. A future compaction action belongs behind a provider capability interface and must fail closed when a supported active-server connection is unavailable.
