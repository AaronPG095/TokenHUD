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

## Connect Claude Code or Antigravity CLI on Windows

Token HUD reads the JSON sent to a provider's documented status-line command and prints a short status line for that provider. Open **Expanded → AI sources** in Token HUD to see the local readings. The Windows executable can be installed or taken from the private package.

Use the full path to `TokenHUD.exe` in the appropriate settings file. These are settings excerpts. Merge the block into your existing file. If you already have a custom status line, keep its configuration and opt in only if you want to replace the custom command.

### Claude Code

In the Claude Code user settings file, `~/.claude/settings.json`, use the command for your status-line shell. For Git Bash, use a quoted executable path with forward slashes:

```json
{
  "statusLine": {
    "type": "command",
    "command": "\"C:/Path To TokenHUD/TokenHUD.exe\" --statusline claude-code",
    "padding": 0
  }
}
```

If Claude Code uses PowerShell instead, set `command` to `& 'C:/Path To TokenHUD/TokenHUD.exe' --statusline claude-code`. Keep the rest of the block the same.

Context occupancy uses the reported input plus cache-read and cache-write input when those counts are available. Latest input and output and the five-hour or weekly quota are shown only when Claude reports them. Remove the entry or restore your previous status line to stop sending readings. [Claude Code status-line documentation](https://code.claude.com/docs/en/statusline)

### Google Antigravity CLI

In `~/.gemini/antigravity-cli/settings.json`, use:

```json
{
  "statusLine": {
    "type": "command",
    "command": "call \"C:\\Path To TokenHUD\\TokenHUD.exe\" --statusline antigravity",
    "stack_with_default": true
  }
}
```

Token HUD shows the provider-reported context percentage and model capacity when available. Latest input and output remain separate measurements. The documented payload does not provide an independent exact occupied-token numerator. The weekly quota is displayed when Antigravity reports its remaining fraction. Set `enabled` to `false` in the block or restore your previous status line to stop sending readings. [Antigravity CLI status-line documentation](https://antigravity.google/docs/cli/statusline/)

These settings affect the CLI tools that invoke the status line. They do not attach to existing Claude or Gemini consumer conversations.
