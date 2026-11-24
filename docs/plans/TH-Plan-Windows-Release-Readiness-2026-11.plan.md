---
title: Plan - Feature: Windows Release Readiness
ticket: https://app.notion.com/p/TH-Plan-Feature-Windows-Release-Readiness-4220b9c6786f817494cbfa733971e31b
status: planned
appetite: L
impact: minor
devTicketKey: TH:TH-Plan-Windows-Release-Readiness-2026-11
agent: Codex
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

- [ ] AC1: A reproducible Windows x64 package builds from the native project and supports per-user installation and uninstall, with validation and safe upgrade behavior. Removing the app preserves metrics and settings by default.
- [ ] AC2: The app can recover from startup preference, single-instance and periodic source failures without corrupting settings or leaking a background process.
- [ ] AC3: A user can export a reviewable, sanitized diagnostic report without chat names, identifiers, paths, content, credentials, or token history.
- [ ] AC4: The Windows build and self-tests pass. A disposable install, launch and uninstall smoke and app-only demo captures verify release output. Any unavailable signing or physical DPI validation is recorded with scope and reason.

## Steps

- [ ] Implement package build, install and uninstall scripts with smoke coverage (AC1).
- [ ] Verify and harden Windows startup, tray, single-instance and recovery paths (AC2).
- [ ] Implement and verify the user-initiated diagnostic export (AC3).
- [ ] Build the package and app-only demo visuals, and verify the release paths (AC4).

## Risks

Updates must preserve the previous usable install until validation. No certificate is assumed and no public distribution is part of this plan. Monitor errors and the installer use disposable fixture paths for smoke tests.

## Out of scope

Public publication, certificate issuance, changes to the user’s live install and provider adapters.

## Notes

Aaron approved the five-plan Windows scope with “Agreed” on 2026-11-24.

## Progress log