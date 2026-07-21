# Product

## Register

product

## Users

The initial customer is an individual professional AI power user on Windows who runs long or parallel Codex chats and needs to manage context deliberately without repeatedly opening status commands. They monitor the HUD peripherally while working in Codex and need immediate confidence about which chat is approaching its preferred context budget.

Teams and enterprises are not first-release customers. Shared telemetry, administration, organizational policy, and collaborative dashboards may be considered only after the individual product proves its value.

## Product Purpose

Token HUD continuously exposes Context Load for the six most recent local Codex chats, with Account Quota kept separate. Success means users can scan every recent chat at once and keep each below a configurable Context Budget without exposing prompt content.

## Product Horizon

Token HUD begins as a local Codex companion but is intended to grow into a Fluent Studio product for people who want deeper, customizable visibility and control over AI context and token usage. The long-term product may support multiple AI applications and operating systems, including macOS, while preserving a coherent cross-platform product model and native behavior on each platform. Commercial distribution remains an option rather than a current commitment.

## Product Sequence

Codex on Windows is the flagship first release and should become reliable, coherent, and polished before Token HUD expands to another provider or operating system. The first release must nevertheless keep provider-specific data access and platform-specific presentation behind explicit boundaries so future integrations do not require a product rewrite.

## Permanent Privacy Position

Token HUD is local-first. Core monitoring and management must work without a Fluent Studio account, and chat content, token telemetry, and provider credentials stay on the user's device by default. Future cloud synchronization or collaboration must be optional, explicit, and data-minimized. Licensing and update checks must remain technically separate from monitored usage data. Provider credentials must not be collected when an on-device integration can perform the work.

## Management Boundary

Token HUD combines observation with safe management. It may open a relevant chat or offer a context-changing Chat Action, such as compaction, only when the provider exposes a supported capability. Unavailable actions remain disabled with an intelligible reason. Every context-changing action is user-initiated in the first release; Token HUD must not automatically compact, archive, or start chats.

## Context Budget Hierarchy

Each installation has a global default Context Budget; Aaron's default is 150,000 tokens. A provider may override that default, and an individual chat may override its provider. The resolved budget is capped below the active model's actual context limit when the provider reports one. Compact surfaces present only the resulting Budget Zone; inheritance and override controls belong in settings and detail tooltips.

## Brand Personality

Quiet, precise, native. The interface should feel like a focused Windows instrument: calm during normal operation, unmistakable when attention is needed, and never theatrical.

## Anti-references

Avoid gaming overlays, neon AI styling, glass-heavy panels, financial-trading dashboards, decorative charts, and dense telemetry consoles. Do not make routine healthy state visually urgent.

## Design Principles

1. Lead with Context Load because it is the metric that guides immediate action.
2. Make important state legible at a peripheral glance without interrupting work.
3. Keep unlike quantities separate: current context, token flow, and account quota must never collapse into one number.
4. Minimize content access, process required content locally and transiently, and create no duplicate transcript store.
5. Be honest about data freshness and capability boundaries; unavailable data and unsupported actions must be explicit.

## Accessibility & Inclusion

Meet WCAG AA contrast, support complete keyboard navigation and screen-reader labels, respect reduced-motion and Windows high-contrast preferences, and render correctly from 100% through 200% display scaling. Follow the Windows light or dark setting by default.
