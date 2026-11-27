---
title: Plan - Feature: Expanded Chat Inspector
ticket: (redacted)
status: shipped
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Expanded-Chat-Inspector-2026-11
agent: Codex
branch: codex/tokenhud-expanded-inspector
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewModel: gpt-6.2-sol
reviewSession: /root/compact_reviewer
baseBranch: main
reviewFingerprint: 175fa8fac3fb96fb44f5291c9a5d59bbbb8a1805cf2f9a2fd9775b9e36ffb2d3
pr: https://github.com/AaronPG095/TokenHUD/pull/14
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

- [x] AC1: Expanded provides searchable and archived chat browsing, a stable selected chat through refresh, and explicit pin and follow behavior. Opening a Compact row reveals its detail. verified: Synthetic WPF verified search, archived detail, stable selected row and explicit pin and follow. Compact UIA selected the detail.
- [x] AC2: The selected detail prioritizes Context Load, shows latest-exchange Token Flow with honest field availability and per-thread readings, and presents quota and source health in distinct labelled sections. verified: Synthetic WPF showed measured zero versus missing fields, separate Account Quota and details after selection.
- [x] AC3: The chat browser and detail reflow or scroll on a constrained Windows work area. Settings open and close by keyboard with predictable focus. Unsupported chat actions expose reasons, and Open Chat is enabled only for a verified safe route. verified: WPF tested 480x400 reflow, Settings open and routed Escape. Unsupported actions explain their state; no verified Open Chat route.
- [x] AC4: The Windows solution and self-tests pass, with synthetic runtime checks for browsing, selection, focus, geometry, settings and measured versus unavailable fields. verified: Rebased branch built without warnings, 10 of 10 self-tests passed and synthetic inspector workflow passed.

## Steps

- [x] Reconcile the expanded chat and thread lists by stable identity and implement navigation and follow behavior (AC1). verified: Synthetic navigation fixtures and ID reconciliation source inspection passed.
- [x] Rebuild the inspector hierarchy and preserve measurement provenance and availability (AC2). verified: Field availability fixture and synthetic inspector presentation checks passed.
- [x] Verify constrained layout, settings focus, screen-reader controls and action states (AC3). verified: Synthetic WPF exercised the browser toggle, Settings Escape and accessible action explanations.
- [x] Run build, self-tests and synthetic workflow checks (AC4). verified: Windows build, self-tests and offscreen WPF harness passed.

## Risks

Live refresh can replace the selected item or make stale detail appear current. Reconcile IDs and exercise result changes in the demo. The experimental adapter cannot safely mutate chats or attribute context by instruction source without data to support it.

## Out of scope

History, profile settings, alert policy, packaging and provider expansion have separate plans. No new context-changing integration is introduced here.

## Notes

Aaron approved the five-PR scope and layout direction on 2026-11-24 with “Agreed.”

## Progress log
- 2026-11-24 11:24 UTC, Codex (review: block): Reopening a Compact row from the narrow browser can conceal its detail. A move to a smaller same-DPI monitor must reapply work-area bounds. (model: gpt-6.2-sol)
- 2026-11-24 11:26 UTC, Codex (review: pass): Both findings resolved. Verified narrow Browse to Compact row activation with WPF and inspected native monitor work-area message handling. 10 of 10 checks passed. [reviewer: /root/compact_reviewer] (model: gpt-6.2-sol)