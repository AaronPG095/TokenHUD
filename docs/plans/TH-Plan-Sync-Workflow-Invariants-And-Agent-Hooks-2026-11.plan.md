---
title: Plan - Chore: Sync Workflow Invariants And Agent Hooks
ticket: https://app.notion.com/p/TH-Plan-Chore-Sync-Workflow-Invariants-And-Agent-Hooks-4220b9c6786f81e982dacde24582dc00
status: in-progress
priority: 
state: 
due: 
appetite: L
version: 
impact: 
dependsOn: 
devTicketKey: TH:TH-Plan-Sync-Workflow-Invariants-And-Agent-Hooks-2026-11
agent: Gemini
branch: gemini/sync-workflow-invariants-and-agent-hooks
worktree: TokenHUD
reviewedBy: Gemini
reviewVerdict: pass
reviewSession: 3772d675-9632-4b37-80a3-afdb49a89ff5
baseBranch: main
reviewFingerprint: a0e120809b776ab6ca174461afdd28f945bfc075345b2a6d93c55d3d564fcb96
---
# Sync Workflow Invariants And Agent Hooks

## Summary

Sync latest AI-Dev-Workflow bundle from upstream main, configure repository invariants, and track .claude/settings.json runtime hooks.

## Context

None noted.

## Open questions

None

## Acceptance criteria

- [x] Managed workflow files synced with zero drift against AI-Dev-Workflow verified: sync.mjs synced bundle and sync --check exited 0
- [x] Agent runtime hooks installed and tracked in .claude/settings.json (3/3 events) verified: hook status --agent-hooks confirmed 3/3 events in .claude/settings.json
- [x] Repository verification command and close-out check pass verified: ticket.mjs verify --tier test passed (TokenHud.SelfTest 5/5 checks passed)

## Steps

- [x] Sync workflow bundle and invariants config
- [x] Verify 3/3 agent runtime hooks in .claude/settings.json
- [x] Run repository verify command, independent subagent review, and close-out check

## Risks

None noted.

## Out of scope

None noted.

## Notes

- Approval (Aaron): "Do all"

## Progress log

<!-- One line per session/review, added with `ticket.mjs log` or `ticket.mjs review` -->
- 2026-11-24 11:13 UTC, Gemini (review: pass): Atomic AI-Dev-Workflow@93381dd bundle sync, invariants config, and 3/3 .claude/settings.json hooks verified cleanly with zero drift; L-appetite exceedance (2700/1000 lines) is structural to the indivisible upstream bundle sync and warrants pass. [reviewer: 3772d675-9632-4b37-80a3-afdb49a89ff5]
