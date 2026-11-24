---
title: Plan - Feature: Context Budgets and Alerts
ticket: https://app.notion.com/p/TH-Plan-Feature-Context-Budgets-and-Alerts-4220b9c6786f81dba9dae426ad3cb8a4
status: in-progress
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Context-Budgets-and-Alerts-2026-11
agent: Codex
branch: codex/tokenhud-context-budgets-alerts
worktree: TokenHUD
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

- [ ] AC1: Installation, provider and chat budgets resolve in that order, with optional overrides and an effective cap below a reported model limit. Existing installation settings remain valid.
- [ ] AC2: Expanded and settings show the resolved budget and its source for the selected chat. Compact remains a six-chat monitor and distinguishes mixed budgets in the header.
- [ ] AC3: Alerts follow per-zone preferences, deduplicate, respect cooldown and temporary snooze, and preserve existing Privacy Mode in toast text. Unavailable measurements do not create or reset alert transitions.
- [ ] AC4: The Windows solution and self-tests pass, including deterministic budget hierarchy, cap, alert, and backwards settings cases.

## Steps

- [ ] Implement and verify profile persistence and budget resolution (AC1).
- [ ] Carry effective budgets through the snapshot and WPF surfaces (AC2).
- [ ] Implement alert transitions, preferences, snooze and compatible persisted state (AC3).
- [ ] Verify Windows build, regressions and budget and alert settings workflows (AC4).

## Risks

Existing saved alert states must retain their meaning during upgrade. A model can change its limit during a chat, and model-reported data may be missing. The analyzer should keep a useful budget in both cases.

## Out of scope

Metrics History, expanded privacy audit, installer and additional providers. Chat links remain capability-gated.

## Notes

Aaron approved the original scope on 2026-11-24 with “Agreed.” Budget settings and alerts are isolated from Metrics History for independent review.

## Progress log