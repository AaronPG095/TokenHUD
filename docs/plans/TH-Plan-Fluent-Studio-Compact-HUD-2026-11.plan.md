---
title: Plan - Feature: Fluent Studio Compact HUD
ticket: https://app.notion.com/p/TH-Plan-Feature-Fluent-Studio-Compact-HUD-4220b9c6786f818f9ba7d97cccf354e3
status: in-progress
appetite: M
impact: minor
devTicketKey: TH:TH-Plan-Fluent-Studio-Compact-HUD-2026-11
agent: Codex
branch: codex/tokenhud-fluent-compact
worktree: TokenHUD
---
# Fluent Studio Compact HUD

## Summary

Bring TokenHUD’s shared controls and Compact View into the Fluent Studio family. Keep six recent chats scannable beside Codex and carry source state, keyboard focus, and privacy through the new layout.

## Context

- `DESIGN.md` and `src/TokenHud.App/ThemeService.cs` define the existing indigo palette.
- Fluent Studio’s Brand Visual Guide uses layered zinc neutrals and Windows blue.
- `src/TokenHud.App/MainWindow.xaml` defines the Compact list, the window controls and the Account Quota footer.
- The readings reliability plan preserves tree recency and provides explicit unavailable context and stale source states.

## Open questions

None

## Acceptance criteria

- [x] AC1: Shared WPF resources and the design guide agree on a zinc and Windows blue palette, distinct Budget Zone colors, native Segoe typography and Windows high-contrast system colors. verified: Matched 13 palette roles in XAML, ThemeService and DESIGN; minimum light contrast 4.59:1, dark 5.81:1.
- [x] AC2: Compact shows up to six recent chats without clipping at the documented minimum under normal scaling conditions, including a two-line chat name, aligned Context Load, and a separate text-only quota footer. verified: Offscreen WPF harness measured six two-line rows and a distinct footer at 360x360 and 392x390.
- [x] AC3: Visible labels or shapes convey actionable Budget Zones without relying on color. Keyboard focus, icon control names, full-title access and Privacy Mode work in Compact. verified: Synthetic WPF verified named zones, accessible activation, logical focus and privacy text, tooltip and automation names.
- [x] AC4: The Windows build and self-tests pass. Synthetic runtime verification covers the Compact default and minimum geometry, keyboard activation and available Windows theme modes. verified: Clean build, 10 of 10 checks and synthetic Windows WPF layout passes in light, dark and high-contrast palettes.

## Steps

- [x] Update the shared palette, controls and design source of truth (AC1). verified: Palette comparisons passed and focus rings preserve geometry.
- [x] Build and verify the Compact six-row layout, quota footer and empty state (AC2). verified: Synthetic WPF verified row and footer bounds at both sizes; empty state changes were inspected.
- [x] Verify keyboard navigation, automation labels, privacy and non-color Budget Zone cues (AC3). verified: WPF rows were focusable and UI Automation activated the selected chat.
- [x] Run build, self-tests and synthetic Windows visual and interaction checks (AC4). verified: Solution and self-tests passed; offscreen WPF used synthetic chats and process-local palettes.

## Risks

Fixed-height titles can clip or crowd the sixth row at minimum size, and live theme changes can leave stale accent resources on controls. Verify actual measured layout and dynamic theme resource lookups. Use a synthetic feed so review images omit real chat names.

## Out of scope

Expanded navigation and settings, budget profiles, alerts, history and provider integrations are tracked separately.

## Notes

Aaron approved the five-PR scope and layout direction on 2026-11-24 with “Agreed.”

## Progress log