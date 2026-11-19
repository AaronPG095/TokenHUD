---
title: Plan - Chore: Sync ADW 40 To 44
ticket: https://app.notion.com/p/TH-Plan-Chore-Sync-ADW-40-To-44-41d0b9c6786f8159a2a7e00dc51d46d6
status: shipped
priority: 
state: 
due: 
appetite: L
version: 
impact: none
devTicketKey: TH:TH-Plan-Sync-ADW-40-To-44-2026-11
agent: Gemini
branch: chore/sync-adw-40-43
worktree: TokenHUD
reviewedBy: Gemini
reviewVerdict: pass
reviewSession: 30e83ae2-7909-43a3-ba93-a7d7c2c21931
baseBranch: main
reviewFingerprint: 0f98cbac1d0e8b05360840100f7284129e3f635088ccf621cacec37175326d09
pr: https://github.com/AaronPG095/TokenHUD/pull/10
---
# Sync ADW 40 To 44
## Summary

Sync the updated AI-Dev-Workflow managed bundle covering upstream PRs #40 through #44 (trusted CI/review evidence rules, strict review enforcement, `--pushed-head` pre-push verification, `agentic.models` role-to-model routing, and downstream missing `ticket.config.example.json` test guard) into TokenHUD.

## Context

- Upstream `AI-Dev-Workflow` merged PRs #40 (CI & review evidence trust, removing workflow-sync `no-plan` exemption), #41 (strict independent review enforcement), #42 (`--pushed-head` pre-push verify), #43 (`agentic.models` role routing), and #44 (guard missing `ticket.config.example.json` in `role-to-model routing` test).
- `scripts/ticket.config.json` in TokenHUD adds default `agentic.models` (`planner: gemini-4-neon-medium`, `builder: gemini-4-hydrogen-medium`, `fixer: gemini-4-hydrogen-medium`, `reviewer: gemini-4-neon-low`) matching `scripts/workflow-manifest.json`.
- Only managed workflow bundle files, `scripts/ticket.config.json`, and this plan file are changed.

## Open questions

None

## Acceptance criteria

- [x] AC1: WHEN `sync.mjs --check` runs against TokenHUD THE SYSTEM SHALL exit 0 with `scripts/ticket.config.json` `agentic.models` matching `scripts/workflow-manifest.json`. verified: verified(cmd): sync.mjs --check exited 0; doctor reports Workflow synced to local bundle and agentic.models matches manifest
- [x] AC2: WHEN workflow and repository verification runs THE SYSTEM SHALL pass: `node --test scripts/ticket.test.mjs` and `node scripts/ticket.mjs doctor` exit 0. verified: verified(test): node --test scripts/ticket.test.mjs passed 93/93 and doctor reported synced bundle
- [x] AC3: WHEN independent review completes THE SYSTEM SHALL record a non-blocked independent subagent review verdict and pass `node scripts/ticket.mjs check`. verified: verified(cmd): independent subagent 30e83ae2 PASS on pack --test (5/5) and staged diff; ticket.mjs check passes

## Steps

- [x] Apply synced `AI-Dev-Workflow` bundle (#40-#44), `agentic.models` config, and pre-push hook installation without altering unrelated files. (AC1)
- [x] Run `node --test scripts/ticket.test.mjs` and `node scripts/ticket.mjs doctor`. (AC2)
- [x] Obtain independent subagent review via `node scripts/ticket.mjs pack --test`, record verdict via `review --reviewer-id`, and verify `node scripts/ticket.mjs check`. (AC3)

## Risks

- Downstream repos omit `ticket.config.example.json`; mitigated by upstream PR #44 guard in `scripts/ticket.test.mjs` and verifying `node --test scripts/ticket.test.mjs` directly in TokenHUD.
- Repo-specific hooks (e.g. `.githooks/pre-push` in website) or local WIP could be clobbered; mitigated by working on clean branch/worktree and preserving custom hooks/stashes.

## Out of scope

- Any product feature or application source code changes in TokenHUD.
- Direct pushes to protected branches or bypassing pre-push/CI gates.

## Notes

- Plan approval: Aaron approved full fleet rollout of AI-Dev-Workflow #40-#44 via `"Yes, do this."` on 2026-11-19.

## Progress log

<!-- One line per session/review, added with `ticket.mjs log` or `ticket.mjs review` -->
- 2026-11-19 17:23 UTC, Gemini (review: pass): PASS: ADW #40-#44 bundle synced cleanly (sync --check OK), no README/NEXT.md leak, ticket.config.json preserves TH settings and adds agentic.models, hygiene clean, Test Integrity OK, TokenHud.SelfTest passed 5/5, only AC3/Step3 await review stamp. [reviewer: 30e83ae2-7909-43a3-ba93-a7d7c2c21931]
