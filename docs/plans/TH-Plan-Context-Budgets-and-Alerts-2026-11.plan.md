---
title: Plan - Feature: Context Budgets and Alerts
ticket: https://app.notion.com/p/TH-Plan-Feature-Context-Budgets-and-Alerts-4220b9c6786f81dba9dae426ad3cb8a4
status: shipped
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Context-Budgets-and-Alerts-2026-11
agent: Codex
branch: codex/tokenhud-context-budgets-alerts
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewModel: gpt-6.2-sol
reviewSession: /root/compact_reviewer
baseBranch: main
reviewFingerprint: 5f51eecc37da07e7bbf379074a30aada3a8ede5b8db29d75448b151ccddb2593
pr: https://github.com/AaronPG095/TokenHUD/pull/15
---
# Context Budgets and Alerts

## Summary

Resolve a context budget for each observed chat and make zone alerts predictable. Expose the installation, Codex, and selected-chat budget settings with clear inheritance and a reported model limit safety cap.

## Context

- `PRODUCT.md` defines budget inheritance, model safety cap and Budget Zones.
- `src/TokenHud.Core/BudgetAnalyzer.cs` currently accepts a single BudgetPolicy.
- `src/TokenHud.App/MainWindow.xaml.cs` sends only two alert zones and mixes UI and delivery state.
- This is the budget and alerts part of the fourth approved plan. Local Metrics History and privacy receive their own PR.

## Open questions

None

## Acceptance criteria

- [x] AC1: Installation, provider and chat budgets resolve in that order, with optional overrides and an effective cap below a reported model limit. Existing installation settings remain valid. verified: Deterministic hierarchy and reported limit fixture passed; synthetic WPF migrated legacy alert state and kept the installation budget.
- [x] AC2: Expanded and settings show the resolved budget and its source for the selected chat. Compact remains a six-chat monitor and distinguishes mixed budgets in the header. verified: WPF settings edited all three levels and the inspector and Compact header reflected capped and mixed budgets.
- [x] AC3: Alerts follow per-zone preferences, deduplicate, respect cooldown and temporary snooze, and preserve existing Privacy Mode in toast text. Unavailable measurements do not create or reset alert transitions. verified: Pure alert tests covered deduplication, unavailable data, cooldown and snooze; WPF verified preferences, legacy state and snooze controls.
- [x] AC4: The Windows solution and self-tests pass, including deterministic budget hierarchy, cap, alert, and backwards settings cases. verified: Rebased solution passed without warnings; 12 of 12 tests and the synthetic budget and alerts settings workflow passed.

## Steps

- [x] Implement and verify profile persistence and budget resolution (AC1). verified: Snapshot fixture verified installation, provider and chat precedence, missing limits and the 95 percent cap.
- [x] Carry effective budgets through the snapshot and WPF surfaces (AC2). verified: Synthetic header and resolved budget source checks passed.
- [x] Implement alert transitions, preferences, snooze and compatible persisted state (AC3). verified: Alert fixtures and WPF settings validation passed; inspected Privacy Mode before toast dispatch.
- [x] Verify Windows build, regressions and budget and alert settings workflows (AC4). verified: Windows build, live and deterministic self-tests, and WPF settings check passed.

## Risks

Existing saved alert states must retain their meaning during upgrade. A model can change its limit during a chat, and model-reported data may be missing. The analyzer should keep a useful budget in both cases.

## Out of scope

Metrics History, expanded privacy audit, installer and additional providers. Chat links remain capability-gated.

## Notes

Aaron approved the original scope on 2026-11-24 with “Agreed.” Budget settings and alerts are isolated from Metrics History for independent review.

## Progress log
- 2026-11-24 16:08 UTC, Codex (review: block): Expired snooze retains the Resume action name until a later setting change. (model: gpt-6.2-sol)
- 2026-11-24 16:15 UTC, Codex (review: pass): Snooze label refreshes on expiry and Settings open. Targeted WPF verified active to expired transition. Independent pack 12 of 12 checks. [reviewer: /root/compact_reviewer] (model: gpt-6.2-sol)