using System.Text;
using TokenHud.Core;

internal static class VendorStatusLineFixtures
{
    public static async Task RunAsync()
    {
        var directory = Directory.CreateTempSubdirectory("tokenhud-vendor-fixture-");
        try
        {
            var store = new ProviderTelemetryStore(directory.FullName);
            var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
            var futureReset = now.AddHours(2).ToUnixTimeSeconds();
            var priorReset = now.AddMinutes(-1).ToUnixTimeSeconds();
            var claude = $$"""
                {
                  "session_id":"private-claude-id", "session_name":"private-title", "cwd":"private-path",
                  "context_window":{
                    "context_window_size":200000,"used_percentage":7,"total_input_tokens":91000,"total_output_tokens":9999,
                    "current_usage":{"input_tokens":8000,"output_tokens":0,"cache_read_input_tokens":4000,"cache_creation_input_tokens":2000}
                  },
                  "rate_limits":{
                    "five_hour":{"used_percentage":100,"resets_at":{{futureReset}}},
                    "seven_day":{"used_percentage":15.5},
                    "spend_limit":{"used_percentage":99,"private":"private-credentials"}
                  }
                }
                """;
            var outcome = await ImportAsync("claude-code", claude, store, now);
            Check(outcome is { ImportResult: ProviderImportResult.Imported, Text: "Claude Code | Context 7%" },
                "Claude status output was incomplete or unsafe.");
            var reading = (await store.LoadAsync(now)).Single();
            Check(reading is
            {
                CurrentContext: { Tokens: 14000, CapacityTokens: 200000, UsedPercent: 7 },
                LatestExchange: { InputTokens: 8000, OutputTokens: 0, CachedInputTokens: 4000 },
                AccountQuotas.Count: 2,
            }, "Claude context included output or lost the latest usage or independent quota windows.");
            Check(reading.AccountQuotas[0] is { Kind: "five-hour", RemainingPercent: 0 } &&
                  reading.AccountQuotas[1] is { Kind: "weekly", RemainingPercent: 84.5m },
                "Claude quota windows lost their independent remaining percentages.");

            const string compacted = """
                {"session_id":"private-claude-id",
                 "context_window":{"context_window_size":200000,"used_percentage":0,
                    "current_usage":{"input_tokens":0,"output_tokens":0,"cache_read_input_tokens":0,"cache_creation_input_tokens":0}}}
                """;
            var compactedAt = now.AddSeconds(1);
            Check((await ImportAsync("claude-code", compacted, store, compactedAt)).Text == "Claude Code | Context unavailable",
                "Claude compaction reported a measured zero.");
            reading = (await store.LoadAsync(compactedAt)).Single();
            Check(reading is { CurrentContext: { Tokens: null, CapacityTokens: 200000, UsedPercent: null }, LatestExchange: null },
                "Compaction kept stale Claude context or fabricated latest exchange usage.");

            var partial = $$"""
                {"session_id":"private-claude-id","context_window":{"used_percentage":null,"current_usage":null,"context_window_size":200000},
                 "rate_limits":{"five_hour":{"used_percentage":32,"resets_at":{{priorReset}}},"seven_day":{"used_percentage":0} } }
                """;
            var partialAt = now.AddSeconds(2);
            await ImportAsync("claude-code", partial, store, partialAt);
            reading = (await store.LoadAsync(partialAt)).Single();
            Check(reading.AccountQuotas.Single() is { Kind: "weekly", RemainingPercent: 100 },
                "An expired five-hour quota masked a current weekly quota.");

            const string antigravity = """
                {
                  "session_id":"private-legacy-id","conversation_id":"private-conversation-id",
                  "product":"antigravity","email":"private-email","conversation_title":"private-title",
                  "transcript_path":"private-transcript",
                  "context_window":{
                    "total_input_tokens":83211,"total_output_tokens":43078,"context_window_size":1048576,"used_percentage":14.24,
                    "current_usage":{"input_tokens":63382,"output_tokens":346,"cache_creation_input_tokens":0,"cache_read_input_tokens":20857}
                  },
                  "quota":{
                    "gemini-weekly":{"remaining_fraction":0.9378,"reset_in_seconds":560580},
                    "private-bucket":{"remaining_amount":300}
                  }
                }
                """;
            outcome = await ImportAsync("antigravity", antigravity, store, now);
            Check(outcome is { ImportResult: ProviderImportResult.Imported, Text: "Gemini (Antigravity CLI) | Context 14.24%" },
                "The Gemini alias or reported percentage was lost.");
            reading = (await store.LoadAsync(now)).Single(item => item.SourceId == "antigravity");
            Check(reading is
            {
                CurrentContext: { Tokens: null, CapacityTokens: 1048576, UsedPercent: 14.24m },
                LatestExchange: { InputTokens: 63382, OutputTokens: 346, CachedInputTokens: 20857 },
            }, "Antigravity inferred occupied context or mixed latest usage with different counters.");
            Check(reading.AccountQuotas.Single() is { Kind: "weekly", RemainingPercent: 93.78m, ResetsAt: not null },
                "Antigravity lost the documented weekly quota or guessed an undocumented amount.");
            foreach (var file in directory.GetFiles("*.json"))
            {
                Check(!(await File.ReadAllTextAsync(file.FullName)).Contains("private-", StringComparison.Ordinal),
                    "A vendor payload field escaped metrics-only projection.");
            }

            foreach (var invalid in new[] { "{", "[]", "{}", new string(' ', VendorStatusLine.MaximumInputBytes + 1) })
            {
                Check((await ImportAsync("claude-code", invalid, store, now)).ImportResult == ProviderImportResult.Invalid,
                    "Malformed, missing, or oversized raw status input was accepted.");
            }
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<VendorStatusLineResult> ImportAsync(
        string source, string json, ProviderTelemetryStore store, DateTimeOffset now)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await VendorStatusLine.ImportAsync(source, input, store, now);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
