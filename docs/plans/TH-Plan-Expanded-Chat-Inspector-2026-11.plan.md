---
title: Plan - Feature: Expanded Chat Inspector
ticket: https://app.notion.com/p/TH-Plan-Feature-Expanded-Chat-Inspector-4220b9c6786f8147bfb4f48ac8290f48
status: in-progress
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Expanded-Chat-Inspector-2026-11
agent: Codex
branch: codex/tokenhud-expanded-inspector
worktree: TokenHUD
---
# Expanded Chat Inspector

## Summary

Make Expanded View a Fluent Studio chat browser and inspector. Keep a selected chat steady through refresh, make per-chat readings easy to audit, and put window settings and adapter limitations where users can find them.

## Context

- `DESIGN.md` specifies search, archived chats, pinning, Context Load, Token Flow, per-thread detail, source health and settings.
- `src/TokenHud.App/MainWindow.xaml.cs` reconstructs expanded collections on refresh and persists normal selection as a pinned chat.
- `src/TokenHud.Core/SnapshotBuilder.cs` has the searchable catalog, independent Account Quota and sampled tracked-chat details.
- The Compact plan provides shared Fluent Studio resources and a synthetic demo for Windows verification.

## Open questions

None

## Acceptance criteria

- [ ] AC1: Expanded provides searchable and archived chat browsing, a stable selected chat through refresh, and explicit pin and follow behavior. Opening a Compact row reveals its detail.
- [ ] AC2: The selected detail prioritizes Context Load, shows latest-exchange Token Flow with honest field availability and per-thread readings, and presents quota and source health in distinct labelled sections.
- [ ] AC3: The chat browser and detail reflow or scroll on a constrained Windows work area. Settings open and close by keyboard with predictable focus. Unsupported chat actions expose reasons, and Open Chat is enabled only for a verified safe route.
- [ ] AC4: The Windows solution and self-tests pass, with synthetic runtime checks for browsing, selection, focus, geometry, settings and measured versus unavailable fields.

## Steps

- [ ] Reconcile the expanded chat and thread lists by stable identity and implement navigation and follow behavior (AC1).
- [ ] Rebuild the inspector hierarchy and preserve measurement provenance and availability (AC2).
- [ ] Verify constrained layout, settings focus, screen-reader controls and action states (AC3).
- [ ] Run build, self-tests and synthetic workflow checks (AC4).

## Risks

Live refresh can replace the selected item or make stale detail appear current. Reconcile IDs and exercise result changes in the demo. The experimental adapter cannot safely mutate chats or attribute context by instruction source without data to support it.

## Out of scope

History, profile settings, alert policy, packaging and provider expansion have separate plans. No new context-changing integration is introduced here.

## Notes

Aaron approved the five-PR scope and layout direction on 2026-11-24 with “Agreed.”

## Progress log