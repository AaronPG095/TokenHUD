---
title: Plan - Process: Sync AI Dev Workflow And Verify Config
ticket: https://app.notion.com/p/TH-Plan-Process-Sync-AI-Dev-Workflow-And-Verify-Config-41a0b9c6786f81fd8f94e9c136d2dee1
status: shipped
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
reviewSelfReason: Synced c01ed59 plan-check diff fix and populated Notes approval
reviewFingerprint: 6a248d5b8a3c8ceca981789f7dc2971c80641e47321a6d7d3ae1b60c0f78ea07
pr: https://github.com/AaronPG095/TokenHUD/pull/7
---
# Sync AI Dev Workflow And Verify Config

## Summary

Sync managed AI-Dev-Workflow bundle (2b1a902) onto main and configure verify, hygiene, and agentic settings in scripts/ticket.config.json.

## Context

scripts/ticket.config.json adds TokenHud.SelfTest verify command, hygiene budgets, and agentic defaults; workflow bundle synced from AI-Dev-Workflow 2b1a902.

## Open questions

None

## Acceptance criteria

- [x] AC1: Synced workflow CLI passes 87 tests via node --test scripts/ticket.test.mjs. verified: node --test scripts/ticket.test.mjs passed 87/87 tests
- [ ] AC2: Project tests pass via dotnet run --project tests/TokenHud.SelfTest and ticket.mjs check passes. deferred: TokenHud.SelfTest requires live Codex rollout logs on disk (3/5 offline checks passed; dotnet build TokenHud.slnx and check passed)

## Steps

- [ ] Run dotnet run --project tests/TokenHud.SelfTest (AC2) deferred: TokenHud.SelfTest requires live Codex rollout logs on disk (3/5 offline checks passed; dotnet build TokenHud.slnx passed)
- [x] Run node --test scripts/ticket.test.mjs and verify workflow sync (AC1)

## Risks

Config or workflow drift mitigated by running TokenHud.SelfTest and ticket.test.mjs.

## Out of scope

TokenHUD application source code and README/NEXT notes.

## Notes

- Approved by user ("And please also then commit and PR the changes for the rest of the repos.") on 2026-11-16.

## Progress log

<!-- One line per session/review, added with `ticket.mjs log` or `ticket.mjs review` -->
- 2026-11-16 11:07 UTC, Gemini (review: warn): Workflow sync and dotnet build verified (87/87 ticket tests pass); TokenHud.SelfTest 2 rollout-dependent checks deferred cleanly. [self-review: Synced workflow bundle and verify config; 87/87 ticket tests and dotnet build pass while SelfTest rollout checks skipped offline]
- 2026-11-16 11:14 UTC, Gemini (review: warn): Updated evidence tag to verified: for BASE_SHA V4 compatibility; dotnet build and 87/87 ticket tests pass. [self-review: Compatible verified: tag syntax for origin/main V4 ci-check]
- 2026-11-16 11:39 UTC, Gemini (review: warn): Synced streamlined ef684e4 workflow bundle and removed weekly workflow-sync.yml cron. [self-review: Updated to streamlined ef684e4 bundle (removed workflow-sync.yml, gated ticket.test.mjs in plan-check.yml)]
- 2026-11-16 13:34 UTC, Gemini (review: warn): Synced c01ed59 bundle and recorded user approval in Notes. [self-review: Synced c01ed59 plan-check diff fix and populated Notes approval]
