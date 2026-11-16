---
title: Plan - Process: Sync AI Dev Workflow And Verify Config
ticket: https://app.notion.com/p/TH-Plan-Process-Sync-AI-Dev-Workflow-And-Verify-Config-41a0b9c6786f81fd8f94e9c136d2dee1
status: in-progress
priority: 
state: 
due: 
appetite: 
version: 
impact: 
devTicketKey: TH:TH-Plan-Sync-AI-Dev-Workflow-And-Verify-Config-2026-11
agent: Gemini
branch: chore/sync-workflow
worktree: TokenHUD
reviewedBy: Gemini
reviewVerdict: warn
reviewSelfReason: Synced workflow bundle and verify config; 87/87 ticket tests and dotnet build pass while SelfTest rollout checks skipped offline
reviewFingerprint: 6d911de4973ea9a5aa32b2fdd6b0612cede049a7c564abfdc2bf29999140b322
---
# Sync AI Dev Workflow And Verify Config

## Summary

Sync managed AI-Dev-Workflow bundle (2b1a902) onto main and configure verify, hygiene, and agentic settings in scripts/ticket.config.json.

## Context

scripts/ticket.config.json adds TokenHud.SelfTest verify command, hygiene budgets, and agentic defaults; workflow bundle synced from AI-Dev-Workflow 2b1a902.

## Open questions

None

## Acceptance criteria

- [x] AC1: Synced workflow CLI passes 87 tests via node --test scripts/ticket.test.mjs. verified(test): node --test scripts/ticket.test.mjs passed 87/87 tests
- [ ] AC2: Project tests pass via dotnet run --project tests/TokenHud.SelfTest and ticket.mjs check passes. deferred: TokenHud.SelfTest requires live Codex rollout logs on disk (3/5 offline checks passed; dotnet build TokenHud.slnx and check passed)

## Steps

- [ ] Run dotnet run --project tests/TokenHud.SelfTest (AC2) deferred: TokenHud.SelfTest requires live Codex rollout logs on disk (3/5 offline checks passed; dotnet build TokenHud.slnx passed)
- [x] Run node --test scripts/ticket.test.mjs and verify workflow sync (AC1)

## Risks

Config or workflow drift mitigated by running TokenHud.SelfTest and ticket.test.mjs.

## Out of scope

TokenHUD application source code and README/NEXT notes.

## Notes

<Discoveries and decisions during implementation, including Aaron's plan approval.>

## Progress log

<!-- One line per session/review, added with `ticket.mjs log` or `ticket.mjs review` -->
- 2026-11-16 11:07 UTC, Gemini (review: warn): Workflow sync and dotnet build verified (87/87 ticket tests pass); TokenHud.SelfTest 2 rollout-dependent checks deferred cleanly. [self-review: Synced workflow bundle and verify config; 87/87 ticket tests and dotnet build pass while SelfTest rollout checks skipped offline]
