using System.Text;
using TokenHud.Core;

internal static class CursorTelemetryFixtures
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("tokenhud-cursor-fixture-");
        try
        {
            var store = new ProviderTelemetryStore(root.FullName);
            var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
            const string hook = """
                {"hook_event_name":"preCompact","session_id":"private-conversation","context_tokens":0,
                 "context_window_size":128000,"context_usage_percent":0.5,"user_email":"private-email",
                 "transcript_path":"private-transcript","workspace_roots":["private-path"]}
                """;
            using var hookStream = Stream(hook);
            Check(await CursorTelemetryImport.ImportPreCompactAsync(hookStream, store, now) == ProviderImportResult.Imported,
                "The documented pre-compaction payload was rejected.");

            var older = now.AddMinutes(-2).ToUnixTimeMilliseconds();
            var newer = now.AddMinutes(-1).ToUnixTimeMilliseconds();
            using var usageStream = Stream($$$"""
                {"usageEvents":[
                    {"timestamp":"{{{newer}}}","conversationId":"private-conversation","userEmail":"private-email",
                     "tokenUsage":{"inputTokens":0,"cacheReadTokens":42,"totalCents":17}},
                    {"timestamp":"{{{older}}}","conversationId":"private-conversation","tokenUsage":{"inputTokens":99}},
                    {"timestamp":{{{older}}},"tokenUsage":{"outputTokens":3},"model":"private-model"},
                    {"timestamp":"{{{older}}}","tokenUsage":{"inputTokens":"999"}},
                    {"timestamp":"{{{older}}}","isTokenBasedCall":false},
                    {"timestamp":"{{{older}}}","product":"grok-bot","tokenUsage":{"inputTokens":99}}
                ]}
                """);
            Check(await CursorTelemetryImport.ImportUsageAsync(usageStream, store, now) == ProviderImportResult.Imported,
                "The documented usage response was rejected.");

            var readings = await store.LoadAsync(now);
            Check(readings.Count == 3 && readings.Select(item => item.SessionKey).Distinct().Count() == 3,
                "Usage, historical context, or anonymous request identity collided.");
            Check(readings[0] is
            {
                SourceId: "cursor", HistoricalContext: { Tokens: 0, CapacityTokens: 128000, UsedPercent: 0.5m },
                CurrentContext: null, LatestExchange: null, AccountQuotas.Count: 0,
            }, "Pre-compaction metrics must be historical only.");
            Check(readings[1] is
            {
                HistoricalContext: null, CurrentContext: null, AccountQuotas.Count: 0,
                LatestExchange: { InputTokens: 0, CachedInputTokens: 42, OutputTokens: null, ReasoningOutputTokens: null },
            }, "Latest request metrics lost zero, inferred fields, or used the older request.");

            var stored = string.Concat(Directory.EnumerateFiles(root.FullName, "*.json").Select(File.ReadAllText));
            Check(!stored.Contains("private-", StringComparison.Ordinal) &&
                !stored.Contains("totalCents", StringComparison.Ordinal), "A private or non-metric field was stored.");

            using var wrongHook = Stream(hook.Replace("preCompact", "sessionEnd", StringComparison.Ordinal));
            Check(await CursorTelemetryImport.ImportPreCompactAsync(wrongHook, store, now) == ProviderImportResult.Invalid,
                "An unrelated Cursor event was accepted as pre-compaction.");
            using var oversized = new MemoryStream(new byte[CursorTelemetryImport.MaximumUsageInputBytes + 1]);
            Check(await CursorTelemetryImport.ImportUsageAsync(oversized, store, now) == ProviderImportResult.Invalid,
                "An oversized usage file was accepted.");

            using var malformedHook = Stream(hook.Replace("private-conversation", @"\uD800", StringComparison.Ordinal));
            Check(await CursorTelemetryImport.ImportPreCompactAsync(malformedHook, store, now) == ProviderImportResult.Invalid,
                "A malformed Unicode hook identity did not return Invalid.");
            var timestamp = now.ToUnixTimeMilliseconds();
            using var mixedUsage = Stream($$$"""
                {"usageEvents":[{"timestamp":{{{timestamp}}},"conversationId":"\uD800","tokenUsage":{"inputTokens":100}},
                 {"timestamp":{{{timestamp}}},"conversationId":"valid-after-malformed","tokenUsage":{"outputTokens":5}}]}
                """);
            Check(await CursorTelemetryImport.ImportUsageAsync(mixedUsage, store, now) == ProviderImportResult.Imported,
                "A malformed identity prevented a valid sibling usage event from importing.");

            var batch = Enumerable.Range(0, 20).Select(index => new
            {
                timestamp,
                conversationId = $"batch-session-{index}",
                tokenUsage = new { inputTokens = 200 + index },
            });
            using var crowded = Stream(System.Text.Json.JsonSerializer.Serialize(new { usageEvents = batch }));
            Check(await CursorTelemetryImport.ImportUsageAsync(crowded, store, now) == ProviderImportResult.Imported,
                "The full usage batch failed to import.");
            var afterBatch = await store.LoadAsync(now);
            Check(afterBatch.Any(item => item.HistoricalContext is not null) &&
                afterBatch.Count(item => item.LatestExchange is not null) == 20,
                "An imported usage batch hid Cursor's historical context.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static MemoryStream Stream(string text) => new(Encoding.UTF8.GetBytes(text));

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
