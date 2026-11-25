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

## Connect Cursor

The Cursor pre-compaction hook gives Token HUD a historical context snapshot when a compaction is about to run. Expanded shows it as **Historical context**. Normal Cursor chat activity cannot refresh it through this hook. [Cursor hook documentation](https://cursor.com/docs/hooks)

For Windows, save a small `cursor-precompact.cmd` wrapper in a directory you own:

```bat
@echo off
"C:\Path To TokenHUD\TokenHUD.exe" --ingest-cursor-hook
```

Add the wrapper to the `preCompact` list in your Cursor `hooks.json`. Keep your existing hook entries. Remove this entry to stop submitting readings.

```json
{
  "version": 1,
  "hooks": {
    "preCompact": [
      { "command": "C:/PathToScripts/cursor-precompact.cmd" }
    ]
  }
}
```

For request usage, Token HUD can read a local Cursor `filtered-usage-events` response. Run the import yourself on a JSON file you can access. This command accepts up to 1 MiB and imports the newest reported request per conversation, up to 20 conversations. It uses input, output and cache-read metrics and does not persist emails, costs or raw conversation identifiers. Personal CSVs with different schemas require a converter to the local telemetry format. [Cursor Admin API usage events](https://cursor.com/docs/account/teams/admin-api)

```powershell
& $tokenHudExe --import-cursor-usage 'C:\Path\cursor-usage.json'
```

## Connect an OpenAI API or Gemini API application

Send the usage object your application already receives after a completed request. Token HUD does not contact the APIs. Use a stable opaque session ID chosen by your application, and an optional UTC `observedAt` when replaying a recorded response. Pass only the usage object, without message content. The command accepts one JSON object up to 16 KiB on standard input.

For OpenAI Responses, send `usage` with `input_tokens`, `output_tokens`, and the cached or reasoning details if provided. [OpenAI Responses API usage](https://developers.openai.com/api/reference/python/resources/responses/methods/retrieve)

```json
{
  "sessionId": "local-app-session",
  "usage": {
    "input_tokens": 100,
    "output_tokens": 25,
    "input_tokens_details": { "cached_tokens": 0 }
  }
}
```

For Gemini GenerateContent, send `usageMetadata` with `promptTokenCount`, `candidatesTokenCount`, `cachedContentTokenCount`, and `thoughtsTokenCount` when provided. [Gemini GenerateContent API](https://ai.google.dev/api/generate-content)

```powershell
Get-Content -Raw .\openai-usage.json | & $tokenHudExe --ingest-usage openai-api
Get-Content -Raw .\gemini-usage.json | & $tokenHudExe --ingest-usage gemini-api
```

These commands report API request usage. The AI sources panel gives ordinary ChatGPT, Claude and Gemini Apps separate coverage rows and official usage links.

## Other documented sources to evaluate

OpenCode exposes a local session usage event. GitHub Copilot SDK reports session token usage and a context limit for connected SDK sessions. These are candidates for future integrations. [OpenCode API](https://opencode.ai/docs/api) · [GitHub Copilot SDK usage](https://docs.github.com/en/copilot/how-tos/copilot-sdk/features/usage-and-billing)
