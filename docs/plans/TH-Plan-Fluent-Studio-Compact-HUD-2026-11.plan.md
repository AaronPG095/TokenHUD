---
title: Plan - Feature: Fluent Studio Compact HUD
ticket: https://app.notion.com/p/TH-Plan-Feature-Fluent-Studio-Compact-HUD-4220b9c6786f818f9ba7d97cccf354e3
status: planned
appetite: M
impact: minor
devTicketKey: TH:TH-Plan-Fluent-Studio-Compact-HUD-2026-11
agent: Codex
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

- [ ] AC1: Shared WPF resources and the design guide agree on a zinc and Windows blue palette, distinct Budget Zone colors, native Segoe typography and Windows high-contrast system colors.
- [ ] AC2: Compact shows up to six recent chats without clipping at the documented minimum under normal scaling conditions, including a two-line chat name, aligned Context Load, and a separate text-only quota footer.
- [ ] AC3: Visible labels or shapes convey actionable Budget Zones without relying on color. Keyboard focus, icon control names, full-title access and Privacy Mode work in Compact.
- [ ] AC4: The Windows build and self-tests pass. Synthetic runtime verification covers the Compact default and minimum geometry, keyboard activation and available Windows theme modes.

## Steps

- [ ] Update the shared palette, controls and design source of truth (AC1).
- [ ] Build and verify the Compact six-row layout, quota footer and empty state (AC2).
- [ ] Verify keyboard navigation, automation labels, privacy and non-color Budget Zone cues (AC3).
- [ ] Run build, self-tests and synthetic Windows visual and interaction checks (AC4).

## Risks

Fixed-height titles can clip or crowd the sixth row at minimum size, and live theme changes can leave stale accent resources on controls. Verify actual measured layout and dynamic theme resource lookups. Use a synthetic feed so review images omit real chat names.

## Out of scope

Expanded navigation and settings, budget profiles, alerts, history and provider integrations are tracked separately.

## Notes

Aaron approved the five-PR scope and layout direction on 2026-11-24 with “Agreed.”

## Progress log