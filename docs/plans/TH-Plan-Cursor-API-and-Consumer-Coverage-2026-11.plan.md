---
title: Plan - Feature: Cursor API and Consumer Coverage
ticket: https://app.notion.com/p/TH-Plan-Feature-Cursor-API-and-Consumer-Coverage-4220b9c6786f81a996dae39ca98799ee
status: in-progress
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Cursor-API-and-Consumer-Coverage-2026-11
agent: Codex
branch: codex/tokenhud-cursor-api-coverage
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewModel: gpt-6.2-sol
reviewSession: /root/cursor_api_final_review
baseBranch: main
reviewFingerprint: 29c1fa70fce7bc5a52994e8e73b9d3982f3bb4eff97ca7f278c71ab166cd0930
---
# Cursor, API and Consumer Coverage

## Summary

Connect the documented Cursor pre-compaction hook and local usage reports from Cursor, OpenAI API and Gemini API. Surface supported usage alongside honest coverage for ChatGPT, Claude and Gemini consumer chats.

## Context

- Cursor reports context in a pre-compaction hook. Per-request usage exists separately in its usage reports.
- OpenAI API and Gemini API responses contain provider-reported request usage fields.
- The local bridge already preserves missing, measured zero, historical context and opaque identities.

## Open questions

None

## Acceptance criteria

- [x] AC1: Cursor pre-compaction input appears as historical context, and a supported local Usage CSV or documented usage event produces request metrics without claiming live context or requiring account credentials. verified: Cursor fixtures pass historical and usage imports, replay, malformed Unicode, and preservation after 20 newer usage rows.
- [x] AC2: Locally submitted OpenAI and Gemini usage objects produce only their measured request fields, preserve unavailable and zero, and leave context and account quota unavailable unless a source reports them. verified: API fixtures pass measured zero, missing fields, unsupported contexts, and bounded privacy-safe projection.
- [x] AC3: Expanded provides clear coverage and links for ordinary ChatGPT, Claude and Gemini consumer chats. It does not read browser, login or transcript data. verified: WPF fixture finds three separate coverage rows with provider-owned links and distinct Cursor historical and usage data.
- [x] AC4: The Windows build, local payload fixtures and native no-window adapter entry points pass. Setup, import limits and supported fields are documented. verified: Windows build clean, 19/19 fixtures pass after combining the vendor adapters, and four native adapter command paths return expected input errors without a window.

## Steps

- [x] Implement Cursor hook and local usage import (AC1). verified: Cursor hook and documented usage event fixtures pass.
- [x] Implement API request usage projection and bounded input (AC2). verified: Bounded OpenAI and Gemini API response fixtures pass.
- [x] Wire Windows command modes, consumer coverage and documentation (AC3). verified: Four native commands and the three WPF coverage rows verified.
- [x] Verify local adapter records, Windows entry points and provider display (AC4). verified: Windows build, fixture suite and WPF checks pass.

## Risks

Vendor totals can have different units and subsets. Preserve original field semantics, keep Cursor snapshots historical, and project only numeric allowlisted metrics.

## Out of scope

Provider login and billing credentials, consumer chat scraping, automated context mutations, and implementation of additional suggested integrations.

## Notes

On 2026-11-24 Aaron wrote “We want this to be implemented” and replied “Continue.” after the separate provider plan was proposed.

## Progress log
- 2026-11-25 11:08 UTC, Codex (review: block): Cursor usage imports can obscure historical context, and malformed escaped Unicode identities can abort the command. (model: gpt-6.2-sol)
- 2026-11-25 11:11 UTC, Codex (review: pass): Independent re-review verified the two Cursor fixes, 19/19 tests, and clean hygiene on the combined provider branch. [reviewer: /root/cursor_api_final_review] (model: gpt-6.2-sol)