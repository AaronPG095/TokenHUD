---
title: Plan - Feature: Local Metrics History
ticket: https://app.notion.com/p/TH-Plan-Feature-Local-Metrics-History-4220b9c6786f81229ea9c86bb5040ef4
status: in-progress
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Local-Metrics-History-2026-11
agent: Codex
branch: codex/tokenhud-local-metrics-history
worktree: TokenHUD
---
# Local Metrics History

## Summary

Keep a local, configurable timeline of measured Context Load and show recent readings for the selected chat. Audit Privacy Mode across visible labels, search, accessibility and alerts.

## Context

- `PRODUCT.md` sets a default 30-day history and Off, 7-day, 30-day, 90-day and indefinite options.
- Only measurements, timestamps, opaque provider and chat IDs and Budget Zones are eligible for storage.
- Budget profiles and alert preferences are implemented in the preceding slice of the fourth approved plan.

## Open questions

None

## Acceptance criteria

- [x] AC1: A per-user local store records measured Context Load with opaque identifiers and zones, deduplicates unchanged samples, and handles missing measurements without introducing synthetic zeros. verified: Deterministic SQLite fixture checked repeated measurements, measured zero, unavailable readings, opaque keys and post-clear cutoff.
- [x] AC2: Settings offer the five retention choices and a separately confirmed clear operation. Expanded shows a compact trend for the selected chat with accessible text and safe empty, disabled, and error states. verified: Synthetic WPF showed five choices, Off and a numeric chart summary; inspected the explicit clear confirmation and verified store erase.
- [x] AC3: Privacy Mode hides names and paths in both views, search, tooltips, automation names and alert copy. History stores no names, paths, prompts, or transcripts. verified: Synthetic WPF removed names and search text, earlier Compact tests covered tooltips and UI Automation, and store fixture verified hashed identity.
- [x] AC4: Windows build and self-tests pass. Deterministic storage fixtures cover duplicate writes, retention and erase, and a synthetic WPF run exercises the selected history and privacy workflow. verified: Clean Windows build, all 13 self-tests including live checks, plus the synthetic privacy and retention workflow.

## Steps

- [x] Implement and verify the local metrics-only store (AC1). verified: SQLite fixture passed for deduplication, retention and erase.
- [x] Connect retention, confirmed clear, and Expanded trend presentation (AC2). verified: WPF settings and chart states passed, and SQLite cutoff prevented reappearing samples.
- [x] Verify Privacy Mode through search, text, accessibility and persisted history (AC3). verified: WPF privacy behavior and SQLite opaque keys passed; alert copy gating remains in the reviewed implementation.
- [x] Run storage, Windows and synthetic workflow verification (AC4). verified: Windows build and synthetic WPF history workflow passed.

## Risks

Scheduled monitoring and user actions can overlap. Serialize store mutations, prevent a cached sample from reappearing immediately after clear, and keep read and UI failures visible without interrupting monitoring.

## Out of scope

Release packaging, cloud storage, provider expansion and transcript-derived analysis.

## Notes

Aaron approved the original scope on 2026-11-24 with “Agreed.” Local Metrics History and privacy are isolated from budget settings and alerts for independent review.

## Progress log