using TokenHud.Core;

internal static class ContextReadingTests
{
    public static async Task RunAsync()
    {
        const string measured = """
            {"timestamp":"2026-11-24T09:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":200},"last_token_usage":{"total_tokens":100}}}}
            """;
        const string quotaOnly = """
            {"timestamp":"2026-11-24T09:01:00Z","type":"event_msg","payload":{"type":"token_count","info":null,"rate_limits":{}}}
            """;
        const string explicitZero = """
            {"timestamp":"2026-11-24T09:02:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":250},"last_token_usage":{"total_tokens":0}}}}
            """;
        const string sessionOnly = """
            {"timestamp":"2026-11-24T09:03:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"total_tokens":275},"last_token_usage":{"total_tokens":"unknown"}}}}
            """;

        var reader = new RolloutReader();
        const string partialFlow = """
            {"type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"input_tokens":100,"cached_input_tokens":0,"cache_write_input_tokens":"unknown","output_tokens":0,"reasoning_output_tokens":0,"total_tokens":100}}}}
            """;
        var flow = reader.TryParseUsage(partialFlow)!.Context;
        Check(flow.Reports(TokenFields.CachedInput) && flow.CachedInputTokens == 0,
            "An explicit zero cached input should remain a measurement.");
        Check(!flow.Reports(TokenFields.CacheWrite) && flow.NewInputTokens is null,
            "An unreported cache write should make the exact new-input split unavailable.");
        Check(flow.VisibleOutputTokens == 0, "A complete measured zero output split should stay available.");
        Check((flow with { ReasoningOutputTokens = 1 }).VisibleOutputTokens is null,
            "Inconsistent output counts should not produce a fabricated visible-output count.");
        var policy = new BudgetPolicy();
        var quota = reader.TryParseUsage(quotaOnly);
        Check(quota is { HasContext: false, HasSessionTotal: false }, "Quota alone must not supply a context or session measurement.");
        Check(BudgetAnalyzer.Analyze([], policy).Zone == BudgetZone.Unavailable, "An empty history must be unavailable.");
        Check(BudgetAnalyzer.Analyze([quota!], policy).Zone == BudgetZone.Unavailable, "A quota-only history must be unavailable.");

        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllLinesAsync(path, [measured, quotaOnly, explicitZero, sessionOnly]);
            var history = await reader.ReadRecentUsageAsync(path);
            Check(history.Count == 4, "All four usage events should be retained.");
            Check(history[1] is { HasContext: true, Context.TotalTokens: 100 }, "A quota-only update must keep the previous measured context.");
            var zero = BudgetAnalyzer.Analyze(history.Take(3).ToArray(), policy);
            Check(zero is { Zone: BudgetZone.Healthy, Load: 0, BudgetPercent: 0, LatestDelta: -100 },
                "A measured zero must replace the previous context and remain available.");
            Check(history[3] is { HasContext: false, SessionTotal.TotalTokens: 275 }, "Malformed context must not erase a valid session total.");
            Check(BudgetAnalyzer.Analyze(history, policy).Zone == BudgetZone.Unavailable,
                "A session-only usage update must not generate a healthy context reading.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
