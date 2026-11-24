# Local provider telemetry

Token HUD accepts one versioned metrics record on standard input with `TokenHUD.exe --ingest-provider`. The command runs without opening the HUD, returns zero for an accepted reading, and stores the allowlisted metrics in the current user's local app data. An invalid input returns 2 and a storage failure returns 1.

Use one stable session identifier per source. Token HUD derives a provider-scoped key and does not store the original identifier. The maximum input is 16 KiB. Omit fields the source did not report. An explicit zero remains a measurement.

```json
{
  "version": 1,
  "sourceId": "claude-code",
  "sessionId": "example-session",
  "observedAt": "2026-11-24T12:00:00Z",
  "currentContext": { "tokens": 15000, "capacityTokens": 200000 },
  "latestExchange": { "inputTokens": 15000, "outputTokens": 900 },
  "accountQuotas": [{ "kind": "five-hour", "remainingPercent": 76 }]
}
```

Replace the example timestamp with the reading time in UTC. Accepted source IDs are `claude-code`, `antigravity`, `cursor`, `openai-api`, and `gemini-api`. Each source can report current context, historical context, latest exchange usage, or account quota when available. Current and historical context accept a token count, capacity, or a provider-reported `usedPercent`. Quota counts need a unit. A reading older than five minutes is shown as stale.

Codex uses its existing local adapter. The separate OpenAI API and Gemini API feeds represent applications configured to send usage. Consumer ChatGPT and Gemini Apps conversations have no documented live context feed for this integration.
