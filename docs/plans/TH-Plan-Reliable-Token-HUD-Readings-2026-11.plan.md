---
title: Plan - Bug: Reliable Token HUD Readings
ticket: (redacted)
status: shipped
appetite: M
impact: patch
devTicketKey: TH:TH-Plan-Reliable-Token-HUD-Readings-2026-11
agent: Codex
branch: codex/tokenhud-reliable-readings
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewModel: gpt-6.2-sol
reviewSession: /root/readings_reviewer
baseBranch: main
reviewFingerprint: 251399d4c6223b8d6334ebe264a00038bed0d2cc82cd39ba30dfa54934a5cd7c
pr: https://github.com/AaronPG095/TokenHUD/pull/11
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
- [x] AC2: Compact includes at most six unarchived top-level chats ordered by latest activity across each chat tree, including subagent activity. verified: SQLite fixture verified six unarchived roots ranked by subagent activity.
- [x] AC3: Five-hour and weekly quotas are classified by their respective durations, independent of slot order. Missing, expired and unrelated windows remain unavailable. verified: Quota model and snapshot fixtures passed for swapped, expired, unrelated and partial observations.
- [x] AC4: An unavailable context measurement is shown as unavailable and stays distinguishable from a measured zero across Compact and Expanded. Stale monitoring data is identified. verified: Context fixture passed for missing and zero usage. Inspected both WPF views and the paused or stale status path.
- [x] AC5: The Windows solution builds and deterministic self-tests pass. A local Codex smoke check exercises available metadata and snapshots without disclosing chat content. verified: Solution built with zero warnings or errors and all 10 self-tests passed, including live Codex checks.

## Steps

- [x] Add deterministic metadata fixtures and fix current-name resolution (AC1). verified: Chat name and local catalog smoke tests passed.
- [x] Test and preserve six-chat membership and tree-based recency (AC2). verified: Snapshot fixture passed, including pinning without changing Compact membership.
- [x] Add quota fixtures and fix supported window classification and freshness (AC3). verified: QuotaTests and the retained-quota snapshot fixture passed.
- [x] Carry measured versus unavailable context and source freshness through the display paths (AC4). verified: Context fixture passed and the WPF data and freshness update paths were inspected.
- [x] Run the Windows build, deterministic self-tests and local source smoke checks (AC5). verified: Windows solution build and all 10 self-tests passed.

## Risks

Experimental Codex storage may omit fields or change ordering. Fixtures cover malformed and partial records, and the live smoke check exercises the installed adapter. Avoid additional prompt access and keep periodic monitoring bounded.

## Out of scope

Fluent Studio restyling, expanded layout changes, budget profiles, alert preferences, and metrics history are handled by later approved PRs.

## Notes

Aaron approved the five-PR scope and layout direction on 2026-11-24 with “Agreed.”

## Progress log
- 2026-11-24 10:54 UTC, Codex (handoff): Done: Created the approved plan and Notion Development ticket, implemented the reading reliability changes in the isolated worktree, and passed 9 of 9 tests before registering the final context fixture. Corrected evidence attached to the wrong items.; Next: Resume the evidence step with full checkbox text instead of changing indexes, build and test the final revision, then obtain independent review and ship PR 1.; Open: A batch reused changing open-item indexes and tick 7 failed. The remaining criteria are open. Final context fixture and exact final build are unverified. Implementation edits are preserved in the isolated worktree.; Tree: 13 dirty file(s)
- 2026-11-24 11:02 UTC, Codex (review: block): Fallback catalog ignores newer rollout activity for named sessions. Malformed quota fields can abort a refresh. Independent pack passed 10 of 10 checks. (model: gpt-6.2-sol)
- 2026-11-24 11:07 UTC, Codex (review: pass): Both findings resolved. Independent pack passed all 10 checks. Fallback ranks per rollout when tree linkage is unavailable. Diff exceeds M appetite due in part to deterministic fixtures. [reviewer: /root/readings_reviewer] (model: gpt-6.2-sol)