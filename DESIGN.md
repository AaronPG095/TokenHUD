# Token HUD Design System

## Direction

**Precision instrument at the edge of attention.** Token HUD should feel like a small native Windows instrument: matte surfaces, disciplined density, and a measured indigo signal. It is designed for peripheral monitoring beside Codex, not prolonged dashboard use.

- Restraint: one primary context meter, quiet supporting numbers, no decorative charts.
- Hierarchy: context load first; account quota always separate.
- Semantic color: budget state owns the meter color. Brand indigo is reserved for focus, selection, and small active-state details.
- Native behavior: Windows theme, high contrast, scaling, keyboard navigation, tray conventions, and reduced motion all take precedence over visual novelty.

## Color

OKLCH values are the design source of truth. The hex values are the clipped sRGB implementation colors used by WPF.

### Light

| Token | OKLCH | sRGB | Use |
|---|---:|---:|---|
| `Canvas` | `oklch(1 0 0)` | `#FFFFFF` | Window background |
| `Surface` | `oklch(0.965 0.006 280)` | `#F2F3F7` | Cards and list rows |
| `SurfaceStrong` | `oklch(0.925 0.012 280)` | `#E4E5EE` | Dividers and controls |
| `Ink` | `oklch(0.22 0.025 280)` | `#181926` | Primary text |
| `Muted` | `oklch(0.48 0.025 280)` | `#5A5C6C` | Supporting text |
| `Primary` | `oklch(0.50 0.16 280)` | `#5552BB` | Selection and focus |
| `Accent` | `oklch(0.72 0.12 220)` | `#2FB5D8` | Informational state only |
| `Healthy` | `oklch(0.60 0.14 155)` | `#1C985A` | Below 100k |
| `Caution` | `oklch(0.72 0.15 80)` | `#D59800` | 100k–124,999 |
| `Critical` | `oklch(0.62 0.20 32)` | `#E5462C` | 125k–149,999 |
| `OverBudget` | `oklch(0.53 0.22 25)` | `#CD001D` | 150k and above |

### Dark

| Token | OKLCH | sRGB | Use |
|---|---:|---:|---|
| `Canvas` | `oklch(0.10 0 0)` | `#030303` | Window background |
| `Surface` | `oklch(0.16 0.008 280)` | `#0C0D11` | Cards and list rows |
| `SurfaceStrong` | `oklch(0.22 0.012 280)` | `#191A20` | Dividers and controls |
| `Ink` | `oklch(0.94 0.01 280)` | `#EAEBF2` | Primary text |
| `Muted` | `oklch(0.70 0.02 280)` | `#9C9DAB` | Supporting text |
| `Primary` | `oklch(0.72 0.14 280)` | `#9499FA` | Selection and focus |
| `Accent` | `oklch(0.78 0.11 220)` | `#59C8E8` | Informational state only |
| `Healthy` | `oklch(0.72 0.13 155)` | `#57BC80` | Below 100k |
| `Caution` | `oklch(0.82 0.14 80)` | `#F3B94C` | 100k–124,999 |
| `Critical` | `oklch(0.74 0.17 32)` | `#FF7C63` | 125k–149,999 |
| `OverBudget` | `oklch(0.68 0.20 25)` | `#FC5855` | 150k and above |

Text and interactive foregrounds must maintain WCAG AA contrast. Semantic colors are never the only carrier of meaning: every state is named and exposed to assistive technology.

## Typography

- Family: `Segoe UI Variable Text`, falling back to `Segoe UI`.
- Tracked-chat title: 13px semibold, wrapped to two lines in Compact View with the complete title available on hover.
- Primary context value: 28px semibold with tabular-number behavior where available.
- Labels and supporting metrics: 12–13px regular/medium.
- Metadata and source health: 11px regular.
- Numbers use stable-width columns and culture-aware grouping.

## Geometry and spacing

- Compact window: 392 × 340 logical pixels; minimum 360 × 324.
- Expanded window: 820 × 700 logical pixels; minimum 720 × 680.
- Base spacing: 4px; common gaps 8, 12, 16, and 24px.
- Outer inset: 16px compact, 20px expanded.
- Corners: 10px for major surfaces, 6px for controls, fully round only for status dots.
- Borders: 1px neutral; avoid broad shadows and floating-card stacks.

## Compact view

The compact view is list-first and contains only:

1. The six most recently active Top-level Chats in a compact list.
2. Each Chat Name and its Context Load against the user budget.
3. A quiet but unambiguous Budget Zone signal for each row.
4. One visually separate, text-only Account Quota footer.

Session Consumption and Highest Child are removed from the entire interface. Context Velocity, Token Spike, forecasts, and secondary badges are excluded from Compact View. Compact View has no selected or pinned row and therefore no previous/next or pin controls. Its title strip retains only privacy, expand, and hide. Double-clicking the strip toggles compact/expanded.

Each Recent Chat List row uses a compact 12.5–13px Chat Name that normally fits on one line but may wrap to a second line. Rows cap names at two lines and ellipsize only beyond that boundary. A Budget Zone dot and exact Context Load remain aligned at the row's trailing edge; complete names and metrics are available to assistive technology and on hover.

The list header names the shared budget once as `CONTEXT / 150k`. Individual rows show only their own Context Load, avoiding six repeated budget values and preserving width for Chat Names.

Recent Chat List rows are read-only overview entries. Activating a row opens that chat's detail in Expanded View; selection, search, historical chats, and deeper inspection do not occupy the compact surface.

List ordering uses the latest activity anywhere in each Chat Tree. A newly active subagent therefore moves its Top-level Chat according to that activity even when the root chat itself has remained idle.

Budget severity never changes Recent Chat List ordering or membership. Critical and Over Budget chats retain their recency position; threshold notifications carry risk outside the six-row overview, and older risky chats remain available in Expanded View.

Account Quota uses one footer line: `QUOTA  5h —  ·  Weekly 68% left`. It has no progress bars. Reset times remain available on hover, and an unreported window remains `—` rather than borrowing another window's value.

## Expanded view

- Left: search, recent unarchived top-level chats, archived filter, pin affordance.
- Right: selected chat Context Load, velocity/spike detail, per-thread Context Load, quotas, and source health.
- Session Consumption and Highest Child do not appear in Expanded View.
- The selected chat should explain both token traffic and broad context origin without combining them into a misleading total. Codex provides exact Input, Cached Input, Cache Write, Output, and Reasoning counts; Cached Input is part of Input, and Reasoning is part of Output. A broad origin view should distinguish instruction-like input (including rules) from working history without exposing individual rule or message names. Source attribution is not provided by Codex and must be labelled as estimated if derived locally.
- Use two compact visuals. The primary Context bar shows exact current Context Load against the 150,000-token budget and divides it into estimated Instructions and Working Context. A secondary Token Flow view shows exact latest-exchange Input split into Cached, Cache Write, and New, plus Output split into Reasoning and Visible. Lifetime totals remain available through tooltips rather than persistent panels.
- Provide an Open Chat action for the selected chat. Expose context-changing Chat Actions only through supported provider capabilities, keep them user-initiated, and explain disabled states rather than presenting inert controls.
- Compact Context requires confirmation every time. The confirmation identifies the chat, shows its current Context Load, explains that older context will be summarized, and offers only Cancel and Compact Context. Compaction is unavailable while the target chat is active, has no first-release confirmation bypass, and reports progress plus its final result inside Token HUD.
- A same-chat Context Reset option is required. It clears accumulated working history from the model-visible context while retaining the existing Chat Name and chat identity; it must not create, fork, or silently substitute a new chat. Persistent provider, user, project, and tool instructions may still load afterward, so the reset Context Load is not necessarily zero.
- Context Reset is exposed only through a stable provider capability. Codex `/clear` and `/new` do not satisfy the requirement because they start a fresh chat, and the deprecated `thread/rollback` endpoint is not a production-safe substitute.
- Context Reset preserves the visible transcript for the user while excluding pre-reset working history from later model context. The interface must visibly mark the reset boundary so retained history is not mistaken for model-visible context.
- Context Reset uses an inline two-click confirmation rather than a modal. The first activation arms the existing Clear Context control and changes that same control into a red `Confirm Clear` action. Only a second activation of the armed control executes the reset; the first activation never changes chat state.
- `Confirm Clear` remains armed for eight seconds. It disarms on Escape, window focus loss, chat selection change, view collapse, or timeout. Pointer and keyboard use both require two distinct activations, and the changed label provides a non-color confirmation cue alongside the red styling. The action cannot be armed while the target chat is running.
- Settings are a compact panel, not a separate visual language: context budget, always-on-top, privacy, notifications, monitoring pause, and login startup.
- No prompt excerpts, first-message previews, or message content appear anywhere.

## Motion

- Meter changes use a short 140ms ease-out transition only when reduced motion is off.
- View resizing is immediate; information should not move while the user is trying to read it.
- Critical state never pulses. State change is communicated by color, label, and a single system notification.

## Accessibility and privacy

- Full keyboard traversal with visible focus indicators.
- Automation names for icon-only controls, values, meters, and zone labels.
- Windows high-contrast support and 100–200% display scaling.
- Privacy Mode replaces titles and paths with neutral labels while preserving metrics.
- Diagnostics record operational categories and counts only; no titles, paths, prompts, previews, or JSONL source lines.
- Core monitoring requires no Fluent Studio account. Monitored content, token telemetry, and provider credentials remain on-device by default.
- Any content needed for estimated Context Origin is processed transiently and is never copied into a Token HUD transcript store.
- Licensing and update traffic must remain isolated from provider data and monitored usage.
- Behavioral analytics are disabled by default. Crash or diagnostic submission requires manual action or explicit opt-in and provides a human-readable preview of the data to be sent.

## Anti-patterns

Avoid neon AI aesthetics, purple gradients, glass, finance-dashboard density, gaming visuals, giant hero metrics, decorative charts, excessive pills, and attention-seeking animation.

## Product boundaries

- Keep provider-neutral concepts, analysis, and settings outside the Codex adapter. Codex remains the only first-release provider.
- Keep Windows lifecycle, notifications, startup, tray, and window behavior outside provider-neutral logic. Windows remains the only first-release platform.
- Expand to another provider or operating system only after the Windows Codex experience is reliable and polished.
- Use native presentation shells: WPF on Windows and SwiftUI for a future macOS application. Share contracts, schemas, terminology, and behavior tests rather than a cross-platform visual implementation.
- Defer the executable shared-core bridge until macOS development begins; do not rewrite the Windows shell pre-emptively.
- Model every provider feature as an explicit Provider Capability with support level and health. Unsupported and unhealthy capabilities fail closed.
- Undocumented local-storage adapters remain visibly Experimental and read-only. Never use screen scraping, keystroke interception, or credential extraction.

## Budget settings

- Resolve Context Budget in this order: installation default, provider override, chat override, then model-limit safety cap.
- Aaron's installation default is 150,000 tokens; product defaults remain configurable for other users.
- Aaron's Budget Zone boundaries are 100,000 for Caution, 125,000 for Critical, and 150,000 for Over Budget.
- Compact View shows only the resolved Budget Zone. Expanded tooltips and settings explain inheritance and overrides.

## Local data controls

- Metrics History defaults to a rolling 30-day retention period, with Off, 7-day, 30-day, 90-day, and indefinite options.
- Settings provide an immediate Clear Metrics History action with confirmation.
- Historical records contain only timestamps, opaque provider/chat identifiers, token counts, Context Load, and Budget Zone changes.
- Prompt, transcript, Chat Name, and filesystem-path content must never enter Metrics History.
