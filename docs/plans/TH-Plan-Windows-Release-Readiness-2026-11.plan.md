---
title: Plan - Feature: Windows Release Readiness
ticket: (redacted)
status: shipped
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Windows-Release-Readiness-2026-11
agent: Codex
branch: codex/tokenhud-windows-release
worktree: TokenHUD
reviewedBy: Codex
reviewVerdict: pass
reviewModel: gpt-6.2-sol
reviewSession: /root/layout_audit
baseBranch: main
reviewFingerprint: 933af0a2f54cc3fd08947ce942b80de0a09189f0fbf7578d1f5bca22b8460ea4
pr: https://github.com/AaronPG095/TokenHUD/pull/17
---
# Windows Release Readiness

## Summary

Prepare a privately distributable Windows package and verify the app lifecycle and privacy-safe diagnostics before a release. Keep installer operations separate from stored user data.

## Context

- The existing install scripts provision AI Dev Workflow and do not install the TokenHUD application.
- Windows uses a native WPF tray, a user-scoped login registration and a single-instance coordinator.
- Existing diagnostics can summarize live usage, so any new export needs an explicit allowlist.

## Open questions

None

## Acceptance criteria

- [x] AC1: A reproducible Windows x64 package builds from the native project and supports per-user installation and uninstall, with validation and safe upgrade behavior. Removing the app preserves metrics and settings by default. verified: Private self-contained archive checksum verified. Isolated install, update, invalid package rejection and uninstall data preservation passed.
- [x] AC2: The app can recover from startup preference, single-instance and periodic source failures without corrupting settings or leaking a background process. verified: Isolated native single-instance activation and shutdown, locked source recovery at 15s, and settings integrity fixtures passed. Registry failures reviewed by code.
- [x] AC3: A user can export a reviewable, sanitized diagnostic report without chat names, identifiers, paths, content, credentials, or token history. verified: Allowlist and inaccessible diagnostics fixture passed. Export UI previews the report before choosing a local destination.
- [x] AC4: The Windows build and self-tests pass. A disposable install, launch and uninstall smoke and app-only demo captures verify release output. Any unavailable signing or physical DPI validation is recorded with scope and reason. verified: 15 of 15 checks, private package smoke and app-only demo PNGs at 100 and 200 percent rendering. Physical mixed DPI and signing unavailable.

## Steps

- [x] Implement package build, install and uninstall scripts with smoke coverage (AC1). verified: Disposable Windows x64 install, update, demo launch and uninstall and path-rejection smoke passed.
- [x] Verify and harden Windows startup, tray, single-instance and recovery paths (AC2). verified: Second-instance, close and restart smoke, monitor recovery, and overlapping and cancelled save fixture passed.
- [x] Implement and verify the user-initiated diagnostic export (AC3). verified: Diagnostic fixture excludes source paths, titles, identifiers and token readings.
- [x] Build the package and app-only demo visuals, and verify the release paths (AC4). verified: Final archive and app-only renders checked. Release remains unsigned and private.

## Risks

Updates must preserve the previous usable install until validation. No certificate is assumed and no public distribution is part of this plan. Monitor errors and the installer use disposable fixture paths for smoke tests.

## Out of scope

Public publication, certificate issuance, changes to the user’s live install and provider adapters.

## Notes

Aaron approved the five-plan Windows scope with “Agreed” on 2026-11-24.

## Progress log
- 2026-11-24 16:42 UTC, Codex (review: block): Shell cleanup was not retryable after app removal and settings fallback could overwrite an unread existing file. (model: gpt-6.2-sol)
- 2026-11-24 16:44 UTC, Codex (review: pass): Retryable shell cleanup and preservation of unread settings resolved. Independent pack passed all 15 tests. [reviewer: /root/layout_audit] (model: gpt-6.2-sol)