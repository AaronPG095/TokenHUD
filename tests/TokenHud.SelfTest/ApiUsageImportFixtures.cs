using System.Text;
using TokenHud.Core;

internal static class ApiUsageImportFixtures
{
    public static async Task RunAsync()
    {
        var directory = Directory.CreateTempSubdirectory("tokenhud-api-fixture-");
        try
        {
            var store = new ProviderTelemetryStore(directory.FullName);
            var now = new DateTimeOffset(2026, 11, 24, 12, 30, 0, TimeSpan.Zero);
            const string openAi = """
                {"sessionId":"private-session","observedAt":"2026-11-24T13:29:00+01:00",
                 "currentContext":{"tokens":999},"accountQuotas":[{"kind":"account","remainingPercent":20}],
                 "usage":{"input_tokens":45,"output_tokens":17,
                          "input_tokens_details":{"cached_tokens":0,"cache_write_tokens":200},"output_tokens_details":{"reasoning_tokens":6},
                          "total_tokens":99,"private-key":"private-secret"},"output":"private-content"}
                """;
            const string gemini = """
                {"sessionId":"private-session","usageMetadata":{"promptTokenCount":70,"candidatesTokenCount":0,
                     "cachedContentTokenCount":null,"thoughtsTokenCount":30,"toolUsePromptTokenCount":800,"totalTokenCount":900},
                 "candidates":[{"content":"private-output"}]}
                """;

            Check(await ImportAsync("openai-api", openAi, store, now) == ProviderImportResult.Imported, "An OpenAI usage wrapper did not import.");
            Check(await ImportAsync("gemini-api", gemini, store, now) == ProviderImportResult.Imported, "A Gemini usage wrapper did not import.");
            var readings = await store.LoadAsync(now);
            var openAiReading = readings.Single(reading => reading.SourceId == "openai-api");
            var geminiReading = readings.Single(reading => reading.SourceId == "gemini-api");
            Check(openAiReading.LatestExchange is { InputTokens: 45, OutputTokens: 17, CachedInputTokens: 0, ReasoningOutputTokens: 6 } &&
                  openAiReading.ObservedAt == now.AddMinutes(-1), "OpenAI totals, subsets or the explicit observation time changed.");
            Check(geminiReading.LatestExchange is { InputTokens: 70, OutputTokens: 0, CachedInputTokens: null, ReasoningOutputTokens: 30 } &&
                  geminiReading.ObservedAt == now, "Gemini candidate and thought tokens, missing cache, or the default observation time changed.");
            Check(openAiReading.SessionKey != geminiReading.SessionKey &&
                  readings.All(reading => reading.CurrentContext is null && reading.HistoricalContext is null && reading.AccountQuotas.Count == 0),
                "API import populated context or quota or merged identities from different providers.");
            foreach (var file in directory.GetFiles("*.json"))
            {
                Check(!(await File.ReadAllTextAsync(file.FullName)).Contains("private-", StringComparison.Ordinal),
                    "A raw API field escaped the metrics-only projection.");
            }

            foreach (var invalid in new[]
            {
                "{",
                """{"sessionId":"fixture","usage":{"total_tokens":50}}""",
                """{"sessionId":"fixture","usage":{"input_tokens":-1}}""",
                """{"sessionId":"fixture","usage":{"output_tokens":"0"}}""",
                """{"sessionId":"fixture","usage":{"input_tokens_details":[]}}""",
                """{"sessionId":"fixture","usageMetadata":{"promptTokenCount":20}}""",
                """{"sessionId":"fixture","observedAt":"2026-11-24T12:29:00","usage":{"input_tokens":0}}""",
                """{"sessionId":"fixture","observedAt":"2026-11-24T12:32:00Z","usage":{"input_tokens":0}}""",
                openAi + new string(' ', ApiUsageImport.MaximumInputBytes),
            })
            {
                Check(await ImportAsync("openai-api", invalid, store, now) == ProviderImportResult.Invalid, "Invalid API usage imported.");
            }

            Check(await ImportAsync("cursor", openAi, store, now) == ProviderImportResult.Invalid, "An unrecognized API source imported.");
            Check((await store.LoadAsync(now)).Count == 2, "Rejected input added or changed persisted readings.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task<ProviderImportResult> ImportAsync(
        string source, string json, ProviderTelemetryStore store, DateTimeOffset now)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await ApiUsageImport.ImportAsync(source, input, store, now);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
