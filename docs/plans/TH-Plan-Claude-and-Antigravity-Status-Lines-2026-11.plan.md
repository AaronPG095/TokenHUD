---
title: Plan - Feature: Claude and Antigravity Status Lines
ticket: https://app.notion.com/p/TH-Plan-Feature-Claude-and-Antigravity-Status-Lines-4220b9c6786f81249113e8ab66b07b35
status: planned
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Claude-and-Antigravity-Status-Lines-2026-11
agent: Codex
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

- [ ] AC1: A Claude Code status-line sample updates a local reading with context, available usage and reported supported quota windows. Missing startup or compaction fields remain unavailable.
- [ ] AC2: An Antigravity status-line sample updates a local Gemini reading. Reported context percentage and capacity remain distinct from request counters and unavailable exact occupied tokens.
- [ ] AC3: Both feeds run through the local Windows executable without launching a second HUD or storing titles, paths, prompts, credentials or raw input. Existing user-configured status lines are kept unless the user deliberately configures the integration.
- [ ] AC4: The Windows solution and fixtures pass. Document opt-in setup, foreground status-line output, provider limits and how to stop sending readings.

## Steps

- [ ] Translate Claude Code and Antigravity status-line inputs to allowlisted provider readings (AC1 and AC2).
- [ ] Connect the normal Windows entry point and document explicit opt-in status-line setup (AC3).
- [ ] Verify adapters against documented synthetic fixtures and exercise the Windows executable path (AC4).

## Risks

Provider status-line callbacks are hot paths and may contain sensitive metadata. Bound inputs, use minimal storage operations, and discard the raw JSON after projecting metrics.

## Out of scope

Cursor, API exporters, consumer chat live telemetry, provider credentials and modifications to Aaron's live provider configuration.

## Notes

On 2026-11-24 Aaron wrote “We want this to be implemented” and replied “Continue.” after the separate provider plan was proposed.

## Progress log