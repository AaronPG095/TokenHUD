---
title: Plan - Feature: Provider Telemetry Foundation
ticket: https://app.notion.com/p/TH-Plan-Feature-Provider-Telemetry-Foundation-4220b9c6786f8127b690f60dc5e596fa
status: shipped
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Provider-Telemetry-Foundation-2026-11
agent: Codex
branch: codex/tokenhud-provider-telemetry
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewModel: gpt-6.2-sol
reviewSession: /root/compact_reviewer
baseBranch: main
reviewFingerprint: 5bbeda8120e87b8d7aba0737d5d1b625c503f5d79adf21606f728a6ccb6a0eaa
pr: https://github.com/AaronPG095/TokenHUD/pull/18
---
# Provider Telemetry Foundation

## Summary

Add a versioned local metrics feed for multiple tools and surface its readings in TokenHUD. Prepare the shared model and bridge for Claude Code, Antigravity, Cursor and API-fed usage.

## Context

- Codex is the existing local Windows source and remains operational without enabling other feeds.
- Claude Code and Antigravity CLI publish documented status-line data. Cursor has event-specific context and usage rather than a documented continuous context feed.
- TokenHUD distinguishes current context, token traffic, account limits and unavailable measurements.

## Open questions

None

## Acceptance criteria

- [x] AC1: The normalized local reading model identifies source and session independently, distinguishes live context, historical context, latest usage and quota, and preserves missing versus zero. verified: Fixtures verified provider and session identity, measured zero, partial exchange, historical context and typed quota.
- [x] AC2: A bounded opt-in ingest path accepts typed JSON and writes only an allowlisted metrics record to local storage. Raw input, secrets, prompts and paths are excluded. Opaque keys, atomic replace and freshness validation are verified. verified: Verified oversize rejection, typed validation, opaque keys and atomic concurrent replace using temporary storage.
- [x] AC3: Expanded shows connected sources, fresh readings, stale data and the verified coverage of each source while Codex keeps its existing Compact behavior. verified: WPF verified connected and stale source rows, missing readings and consumer feed limitations; local Codex smoke passed.
- [x] AC4: The Windows solution and deterministic tests pass, with a synthetic WPF session exercising source status, a connected reading and qualified missing fields. verified: Clean rebased Windows build, 16 of 16 integrated checks, and synthetic WPF provider panel load.

## Steps

- [x] Implement the local provider reading and storage contract (AC1 and AC2). verified: Synthetic source and privacy projection fixtures passed, including concurrent deliveries and older replay.
- [x] Wire bounded ingest and the connected-source panel (AC2 and AC3). verified: Built the CLI import entry and verified the WPF view using disposable data.
- [x] Verify Windows build, local-feed fixtures and source presentation (AC4). verified: Windows solution, telemetry fixture, live Codex and WPF panel verification passed.

## Risks

Raw provider status payloads can include paths or content. Persist only an explicitly projected DTO and bind the provider separately from each session hash. Treat stale and historical readings as such.

## Out of scope

Individual vendor wire adapters, live account setup, model-traffic capture, and new provider management commands are separate.

## Notes

On 2026-11-24 Aaron wrote “We want this to be implemented” and replied “Continue.” after the separate provider plan was proposed.

## Progress log
- 2026-11-24 16:35 UTC, Codex (review: block): Capacity-only, cached and reasoning-only metrics and quota-window labels were incomplete; a global file bound could hide other providers. (model: gpt-6.2-sol)
- 2026-11-24 16:48 UTC, Codex (review: pass): Verified every supported partial reading and isolated the per-source reading bounds. Combined release-base build and all 16 checks passed. [reviewer: /root/compact_reviewer] (model: gpt-6.2-sol)
