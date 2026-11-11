---
title: Plan - Process: AI Dev Workflow rollout
ticket: https://app.notion.com/p/TH-Plan-Process-AI-Dev-Workflow-rollout-4150b9c6786f81ce8307d7a2cc059e7e
status: in-progress
devTicketKey: TH:TH-Plan-AI-Dev-Workflow-27c67cd8cd07-2026-11
agent: Codex
branch: codex/adw-rollout-27c67cd8cd07
worktree: TokenHUD
---

# Plan - Process: AI Dev Workflow rollout

## Summary

Sync the reviewed AI Dev Workflow release 27c67cd8cd0720b06a6c73329e5c05bb765ca7a3 to AaronPG095/TokenHUD. Preserve this repository's tracker configuration and project files.

## Acceptance criteria

- [x] The pinned release matches the downstream managed workflow and preserves the repository configuration. verified: Sync check passed at 27c67cd8cd0720b06a6c73329e5c05bb765ca7a3 and SHA-256 of downstream config is unchanged.
- [x] The installed Node workflow suite passes. verified: node --test scripts/ticket.test.mjs passed all 67 tests.

## Steps

- [x] Apply and check the pinned release.
- [x] Run the complete Node workflow suite.

## Notes

Approval: Plan approved. Execute the entire plan instead of just doing little bits of it.

## Progress log
