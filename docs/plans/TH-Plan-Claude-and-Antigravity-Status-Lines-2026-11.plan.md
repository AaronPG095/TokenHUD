---
title: Plan - Feature: Claude and Antigravity Status Lines
ticket: (redacted)
status: shipped
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Claude-and-Antigravity-Status-Lines-2026-11
agent: Codex
branch: codex/tokenhud-claude-antigravity
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewSession: /root/compact_qa_plan
reviewModel: gpt-6.2-sol
baseBranch: main
reviewFingerprint: 32186a09bc83d0de8b482ac069e2e84623a4a721918135dd8db9ef81faf1948d
pr: https://github.com/AaronPG095/TokenHUD/pull/19
---
# Claude and Antigravity Status Lines

## Summary

Connect documented Windows CLI status lines to TokenHUD's local feed. Alias Google Antigravity CLI clearly as a Gemini integration and keep provider measurements and coverage explicit.

## Context

- Claude Code documents a status-line JSON with an input-only context calculation and optional five-hour and seven-day allowance.
- Google Antigravity CLI documents a provider-reported context percentage, capacity, latest usage and model quota buckets.
- The provider foundation persists only projected metrics and opaque provider-scoped session keys.

## Open questions

None

## Acceptance criteria

- [x] AC1: A Claude Code status-line sample updates a local reading with context, available usage and reported supported quota windows. Missing startup or compaction fields remain unavailable. verified: Documented synthetic payload verified exact input-only context, two quotas, and compaction with no fake occupied count.
- [x] AC2: An Antigravity status-line sample updates a local Gemini reading. Reported context percentage and capacity remain distinct from request counters and unavailable exact occupied tokens. verified: Synthetic payload kept reported percentage and model capacity separate from request usage.
- [x] AC3: Both feeds run through the local Windows executable without launching a second HUD or storing titles, paths, prompts, credentials or raw input. Existing user-configured status lines are kept unless the user deliberately configures the integration. verified: Native executable stdin/stdout smoke ran for both modes without opening the HUD. Store fixtures verified raw metadata exclusion.
- [x] AC4: The Windows solution and fixtures pass. Document opt-in setup, foreground status-line output, provider limits and how to stop sending readings. verified: Clean solution build, 17 of 17 checks on the merged base, plus native smoke in Git Bash, Windows PowerShell and cmd.

## Steps

- [x] Translate Claude Code and Antigravity status-line inputs to allowlisted provider readings (AC1 and AC2). verified: Integrated fixtures passed for both translators and privacy-safe projection.
- [x] Connect the normal Windows entry point and document explicit opt-in status-line setup (AC3). verified: App starts the no-window status-line mode and documentation gives opt-in and removal steps without touching live settings.
- [x] Verify adapters against documented synthetic fixtures and exercise the Windows executable path (AC4). verified: Both native callback modes and full Windows build and self-tests passed.

## Risks

Provider status-line callbacks are hot paths and may contain sensitive metadata. Bound inputs, use minimal storage operations, and discard the raw JSON after projecting metrics.

## Out of scope

Cursor, API exporters, consumer chat live telemetry, provider credentials and modifications to Aaron's live provider configuration.

## Notes

On 2026-11-24 Aaron wrote “We want this to be implemented” and replied “Continue.” after the separate provider plan was proposed.

## Progress log
- 2026-11-25 11:06 UTC, Codex (review: pass): Independent review verified corrected Windows shell commands, privacy projection and no-window startup. Combined build and all 17 checks passed. [reviewer: /root/compact_qa_plan] (model: gpt-6.2-sol)