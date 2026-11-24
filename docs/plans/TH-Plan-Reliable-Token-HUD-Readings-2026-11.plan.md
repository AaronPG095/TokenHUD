---
title: Plan - Bug: Reliable Token HUD Readings
ticket: https://app.notion.com/p/TH-Plan-Bug-Reliable-Token-HUD-Readings-4220b9c6786f816c921cce14bc620d61
status: in-progress
appetite: M
impact: patch
devTicketKey: TH:TH-Plan-Reliable-Token-HUD-Readings-2026-11
agent: Codex
branch: codex/tokenhud-reliable-readings
worktree: TokenHUD
---
# Reliable Token HUD Readings

## Summary

Make the six-chat overview reliable before changing its layout. Verify the local adapter and UI paths for chat names, recency, account quota, and missing context measurements.

## Context

- `src/TokenHud.Core/CodexCatalog.cs:105` resolves titles from the local index and SQLite metadata.
- `src/TokenHud.Core/SnapshotBuilder.cs:25` ranks top-level chats using tree activity and samples the most recent six.
- `src/TokenHud.Core/Models.cs:33` classifies rate-limit windows using broad ranges that can admit unrelated durations.
- `src/TokenHud.App/MainWindow.xaml.cs:167` reads analysis values even when measurement availability differs across chats.

## Open questions

None

## Acceptance criteria

- [x] AC1: Current indexed chat names survive out-of-order renames and malformed index entries, and missing names use a safe fallback without reading message content. verified: Self-test exercised both metadata paths, reordered and malformed names, and the neutral fallback.
- [ ] AC2: Compact includes at most six unarchived top-level chats ordered by latest activity across each chat tree, including subagent activity.
- [ ] AC3: Five-hour and weekly quotas are classified by their respective durations, independent of slot order. Missing, expired and unrelated windows remain unavailable.
- [ ] AC4: An unavailable context measurement is shown as unavailable and stays distinguishable from a measured zero across Compact and Expanded. Stale monitoring data is identified.
- [ ] AC5: The Windows solution builds and deterministic self-tests pass. A local Codex smoke check exercises available metadata and snapshots without disclosing chat content.

## Steps

- [ ] Add deterministic metadata fixtures and fix current-name resolution (AC1).
- [ ] Test and preserve six-chat membership and tree-based recency (AC2).
- [ ] Add quota fixtures and fix supported window classification and freshness (AC3).
- [ ] Carry measured versus unavailable context and source freshness through the display paths (AC4).
- [ ] Run the Windows build, deterministic self-tests and local source smoke checks (AC5).

## Risks

Experimental Codex storage may omit fields or change ordering. Fixtures cover malformed and partial records, and the live smoke check exercises the installed adapter. Avoid additional prompt access and keep periodic monitoring bounded.

## Out of scope

Fluent Studio restyling, expanded layout changes, budget profiles, alert preferences, and metrics history are handled by later approved PRs.

## Notes

Aaron approved the five-PR scope and layout direction on 2026-11-24 with “Agreed.”

## Progress log
- 2026-11-24 10:54 UTC, Codex (handoff): Done: Created the approved plan and Notion Development ticket, implemented the reading reliability changes in the isolated worktree, and passed 9 of 9 tests before registering the final context fixture. Corrected evidence attached to the wrong items.; Next: Resume the evidence step with full checkbox text instead of changing indexes, build and test the final revision, then obtain independent review and ship PR 1.; Open: A batch reused changing open-item indexes and tick 7 failed. The remaining criteria are open. Final context fixture and exact final build are unverified. Implementation edits are preserved in the isolated worktree.; Tree: 13 dirty file(s)