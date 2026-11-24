---
title: Plan - Feature: Provider Telemetry Foundation
ticket: https://app.notion.com/p/TH-Plan-Feature-Provider-Telemetry-Foundation-4220b9c6786f8127b690f60dc5e596fa
status: planned
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Provider-Telemetry-Foundation-2026-11
agent: Codex
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

- [ ] AC1: The normalized local reading model identifies source and session independently, distinguishes live context, historical context, latest usage and quota, and preserves missing versus zero.
- [ ] AC2: A bounded opt-in ingest path accepts typed JSON and writes only an allowlisted metrics record to local storage. Raw input, secrets, prompts and paths are excluded. Opaque keys, atomic replace and freshness validation are verified.
- [ ] AC3: Expanded shows connected sources, fresh readings, stale data and the verified coverage of each source while Codex keeps its existing Compact behavior.
- [ ] AC4: The Windows solution and deterministic tests pass, with a synthetic WPF session exercising source status, a connected reading and qualified missing fields.

## Steps

- [ ] Implement the local provider reading and storage contract (AC1 and AC2).
- [ ] Wire bounded ingest and the connected-source panel (AC2 and AC3).
- [ ] Verify Windows build, local-feed fixtures and source presentation (AC4).

## Risks

Raw provider status payloads can include paths or content. Persist only an explicitly projected DTO and bind the provider separately from each session hash. Treat stale and historical readings as such.

## Out of scope

Individual vendor wire adapters, live account setup, model-traffic capture, and new provider management commands are separate.

## Notes

On 2026-11-24 Aaron wrote “We want this to be implemented” and replied “Continue.” after the separate provider plan was proposed.

## Progress log