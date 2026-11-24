using System.Text;
using System.Text.Json;
using TokenHud.Core;

internal static class ProviderTelemetryFixtures
{
    public static async Task RunAsync()
    {
        var root = Directory.CreateTempSubdirectory("tokenhud-provider-fixture-");
        try
        {
            var store = new ProviderTelemetryStore(root.FullName);
            var observed = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
            var now = observed.AddMinutes(4);
            const string raw = """
                {
                  "version":1,"sourceId":"claude-code","sessionId":"private-session-key",
                  "observedAt":"2026-11-24T12:00:00Z","currentContext":{"tokens":0,"capacityTokens":200000,"secret":"secret-value"},
                  "historicalContext":{"usedPercent":40},"latestExchange":{"inputTokens":0},
                  "accountQuotas":[{"kind":"five-hour","remainingPercent":0,"resetsAt":"2026-11-24T12:10:00Z"},
                                   {"kind":"weekly","remainingAmount":22.5,"unit":"credits"}],
                  "prompt":"private-prompt","path":"private-path","credential":"private-credential"
                }
                """;

            Check(await ImportAsync(store, raw, now) == ProviderImportResult.Imported, "A valid reading was rejected.");
            var reading = (await store.LoadAsync(now)).Single();
            Check(reading is
            {
                SourceId: "claude-code",
                CurrentContext: { Tokens: 0, CapacityTokens: 200000, UsedPercent: null },
                HistoricalContext: { Tokens: null, UsedPercent: 40 },
                LatestExchange: { InputTokens: 0, OutputTokens: null },
                AccountQuotas.Count: 2,
            }, "Missing, zero, historical context or partial exchange values were lost.");
            Check(reading.AccountQuotas[0] is { RemainingPercent: 0, RemainingAmount: null }, "Zero percentage was lost.");
            Check(reading.AccountQuotas[1] is { RemainingAmount: 22.5m, Unit: "credits", RemainingPercent: null }, "Quota unit was lost.");
            Check(reading.GetFreshness(now) == ProviderFreshness.Fresh &&
                  reading.GetFreshness(observed.AddMinutes(5)) == ProviderFreshness.Stale &&
                  reading.AccountQuotas[0].IsExpired(observed.AddMinutes(10)),
                "Freshness or quota expiry was not evaluated at the requested time.");

            var stored = root.GetFiles("*.json").Single();
            var content = await File.ReadAllTextAsync(stored.FullName);
            Check(stored.Name == reading.SessionKey + ".json" &&
                  reading.SessionKey.Length == 64 &&
                  !content.Contains("private-", StringComparison.Ordinal) &&
                  !content.Contains("secret-value", StringComparison.Ordinal),
                "Raw identifiers or non-metric fields leaked into a persisted record.");
            using (var json = JsonDocument.Parse(content))
            {
                Check(json.RootElement.EnumerateObject().Select(item => item.Name).ToHashSet().SetEquals(
                    ["version", "sourceId", "sessionKey", "observedAt", "currentContext", "historicalContext", "latestExchange", "accountQuotas"]),
                    "The persisted schema differs from its allowlist.");
            }

            var replay = raw.Replace("12:00:00Z", "11:59:00Z", StringComparison.Ordinal)
                .Replace("\"tokens\":0", "\"tokens\":50", StringComparison.Ordinal);
            Check(await ImportAsync(store, replay, now) == ProviderImportResult.Imported &&
                  (await store.LoadAsync(now)).Single().CurrentContext?.Tokens == 0,
                "An older reading overwrote the latest reading for a session.");

            var otherProvider = raw.Replace("claude-code", "cursor", StringComparison.Ordinal);
            var otherStore = new ProviderTelemetryStore(root.FullName);
            var outcomes = await Task.WhenAll(ImportAsync(store, otherProvider, now), ImportAsync(otherStore, replay, now));
            Check(outcomes.All(result => result == ProviderImportResult.Imported), "Concurrent ingest lost a valid reading.");
            var readings = await store.LoadAsync(now);
            Check(readings.Count == 2 && readings.Select(item => item.SessionKey).Distinct().Count() == 2,
                "Session keys were not scoped by provider.");

            foreach (var invalid in new[]
            {
                "{",
                raw.Replace("\"version\":1", "\"version\":2", StringComparison.Ordinal),
                raw.Replace("claude-code", "codex", StringComparison.Ordinal),
                raw.Replace("12:00:00Z", "12:10:00Z", StringComparison.Ordinal),
                raw.Replace("12:00:00Z", "12:00:00", StringComparison.Ordinal),
                raw.Replace("\"tokens\":0", "\"tokens\":-1", StringComparison.Ordinal),
                raw.Replace("\"tokens\":0", "\"tokens\":1.25", StringComparison.Ordinal),
                raw.Replace("\"remainingPercent\":0", "\"remainingPercent\":101", StringComparison.Ordinal),
                raw.Replace("\"credits\"", "\"private-arbitrary-unit\"", StringComparison.Ordinal),
                raw.Replace("\"weekly\"", "\"five-hour\"", StringComparison.Ordinal),
                raw + new string(' ', ProviderTelemetryStore.MaximumInputBytes),
            })
            {
                Check(await ImportAsync(store, invalid, now) == ProviderImportResult.Invalid, "Invalid input was accepted.");
            }

            await File.WriteAllTextAsync(Path.Combine(root.FullName, new string('c', 64) + ".json"), "malformed");
            Check((await store.LoadAsync(now)).Count == 2 && !root.GetFiles("*.tmp").Any(),
                "Malformed persisted input or temporary files interfered with legitimate readings.");
        }
        finally
        {
            root.Delete(recursive: true);
        }
    }

    private static async Task<ProviderImportResult> ImportAsync(ProviderTelemetryStore store, string payload, DateTimeOffset now)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        return await store.ImportAsync(input, now);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
