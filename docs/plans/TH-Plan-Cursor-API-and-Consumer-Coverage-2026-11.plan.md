---
title: Plan - Feature: Cursor API and Consumer Coverage
ticket: https://app.notion.com/p/TH-Plan-Feature-Cursor-API-and-Consumer-Coverage-4220b9c6786f81a996dae39ca98799ee
status: planned
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Cursor-API-and-Consumer-Coverage-2026-11
agent: Codex
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

- [ ] AC1: Cursor pre-compaction input appears as historical context, and a supported local Usage CSV or documented usage event produces request metrics without claiming live context or requiring account credentials.
- [ ] AC2: Locally submitted OpenAI and Gemini usage objects produce only their measured request fields, preserve unavailable and zero, and leave context and account quota unavailable unless a source reports them.
- [ ] AC3: Expanded provides clear coverage and links for ordinary ChatGPT, Claude and Gemini consumer chats. It does not read browser, login or transcript data.
- [ ] AC4: The Windows build, local payload fixtures and native no-window adapter entry points pass. Setup, import limits and supported fields are documented.

## Steps

- [ ] Implement Cursor hook and local usage import (AC1).
- [ ] Implement API request usage projection and bounded input (AC2).
- [ ] Wire Windows command modes, consumer coverage and documentation (AC3).
- [ ] Verify local adapter records, Windows entry points and provider display (AC4).

## Risks

Vendor totals can have different units and subsets. Preserve original field semantics, keep Cursor snapshots historical, and project only numeric allowlisted metrics.

## Out of scope

Provider login and billing credentials, consumer chat scraping, automated context mutations, and implementation of additional suggested integrations.

## Notes

On 2026-11-24 Aaron wrote “We want this to be implemented” and replied “Continue.” after the separate provider plan was proposed.

## Progress log