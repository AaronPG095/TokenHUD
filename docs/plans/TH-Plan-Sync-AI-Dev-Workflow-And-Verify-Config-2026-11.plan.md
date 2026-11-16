---
title: Plan - Process: Sync AI Dev Workflow And Verify Config
ticket: https://app.notion.com/p/TH-Plan-Process-Sync-AI-Dev-Workflow-And-Verify-Config-41a0b9c6786f81fd8f94e9c136d2dee1
status: planned
priority: 
state: 
due: 
appetite: 
version: 
impact: 
devTicketKey: TH:TH-Plan-Sync-AI-Dev-Workflow-And-Verify-Config-2026-11
agent: Gemini
---
# Sync AI Dev Workflow And Verify Config

## Summary

Sync managed AI-Dev-Workflow bundle (2b1a902) onto main and configure verify, hygiene, and agentic settings in scripts/ticket.config.json.

## Context

scripts/ticket.config.json adds TokenHud.SelfTest verify command, hygiene budgets, and agentic defaults; workflow bundle synced from AI-Dev-Workflow 2b1a902.

## Open questions

None

## Acceptance criteria

- [ ] AC1: Synced workflow CLI passes 87 tests via node --test scripts/ticket.test.mjs.
- [ ] AC2: Project tests pass via dotnet run --project tests/TokenHud.SelfTest and ticket.mjs check passes.

## Steps

- [ ] Run dotnet run --project tests/TokenHud.SelfTest (AC2)
- [ ] Run node --test scripts/ticket.test.mjs and verify workflow sync (AC1)

## Risks

Config or workflow drift mitigated by running TokenHud.SelfTest and ticket.test.mjs.

## Out of scope

TokenHUD application source code and README/NEXT notes.

## Notes

<Discoveries and decisions during implementation, including Aaron's plan approval.>

## Progress log

<!-- One line per session/review, added with `ticket.mjs log` or `ticket.mjs review` -->
