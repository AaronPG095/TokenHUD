# Workflow Lessons

<!-- Captured via `node scripts/ticket.mjs lesson` or `--retro` on `ship`/`pr`. Surfaced by `recall`, `review --prompt`, and `step --role reviewer`. -->
- 2026-11-24 | review-blocker · docs/plans/TH-Plan-Local-Metrics-History-2026-11.plan.md | Resolved review blocker (Clear could replay future-dated observations. Scheduled retention did not prune rows while monitoring was paused. (model: gpt-6.2-sol)): Erasure cutoff and queued-write generation prevent replay. Retention prunes on startup and while monitoring is paused. Independent pack passed all 13 checks.
