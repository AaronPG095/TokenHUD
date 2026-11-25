using TokenHud.Core;

var tests = new List<(string Name, Func<Task> Run)>
{
    ("parses token events", ParseTokenEventAsync),
    ("stores only retained local metrics", MetricsHistoryTests.StoresOnlyRetainedMeasurementsAsync),
    ("keeps diagnostic export operational and private", VerifyDiagnosticPrivacyAsync),
    ("preserves settings across cancelled and concurrent saves", SettingsStoreFixtures.VerifyInterruptedAndOverlappingSavesAsync),
    ("imports allowlisted local provider readings", ProviderTelemetryFixtures.RunAsync),
    ("projects Claude Code and Antigravity status lines", VendorStatusLineFixtures.RunAsync),
    ("imports Cursor historical context and request usage", CursorTelemetryFixtures.RunAsync),
    ("imports measured API usage without claiming current context", ApiUsageImportFixtures.RunAsync),
    ("enforces budget boundaries", BudgetBoundariesAsync),
    ("resolves inherited budgets against current model limits", ContextBudgetTests.ResolvesHierarchyAndReportedLimitsAsync),
    ("deduplicates zone alerts and respects cooldown and snooze", BudgetAlertTests.RunAsync),
    ("detects velocity and spikes", VelocityAndSpikeAsync),
    ("resolves current indexed chat names safely", CatalogFixtures.ResolveCurrentNamesAsync),
    ("orders the six recent chats by tree activity", SnapshotTests.KeepsSixRecentTopLevelChatsAsync),
    ("classifies and expires independent quota windows", QuotaTests.RunAsync),
    ("retains current quotas across partial observations", SnapshotTests.RetainsIndependentQuotaWindowsAsync),
    ("distinguishes missing context from measured zero", ContextReadingTests.RunAsync),
    ("reads the local Codex metadata catalog", ReadLocalCatalogAsync),
    ("builds a live privacy-safe HUD snapshot", BuildLiveSnapshotAsync),
};

var failed = 0;
foreach (var test in tests)
{
    try
    {
        await test.Run();
        Console.WriteLine($"PASS  {test.Name}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.Error.WriteLine($"FAIL  {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{tests.Count - failed}/{tests.Count} checks passed");
return failed == 0 ? 0 : 1;

static Task ParseTokenEventAsync()
{
    const string line = """
        {"timestamp":"2026-07-20T12:00:00Z","type":"event_msg","payload":{"type":"token_count","info":{"total_token_usage":{"input_tokens":120000,"cached_input_tokens":80000,"cache_write_input_tokens":0,"output_tokens":14000,"reasoning_output_tokens":4000,"total_tokens":134000},"last_token_usage":{"input_tokens":85000,"cached_input_tokens":60000,"cache_write_input_tokens":0,"output_tokens":5000,"reasoning_output_tokens":1000,"total_tokens":90000},"model_context_window":258400},"rate_limits":{"primary":{"used_percent":35.5,"window_minutes":300,"resets_at":1784563200},"secondary":{"used_percent":18,"window_minutes":10080,"resets_at":1785168000},"plan_type":"pro","spend_control_reached":false}}}
        """;
    var parsed = new RolloutReader().TryParseUsage(line);
    if (parsed is null)
    {
        throw new InvalidOperationException("Parser returned no snapshot.");
    }
    Check(parsed.Context.TotalTokens == 90_000, "Context total was not parsed.");
    Check(parsed.SessionTotal.CachedInputTokens == 80_000, "Cached input was not parsed.");
    Check(parsed.ModelContextWindow == 258_400, "Model context window was not parsed.");
    Check(Math.Abs(parsed.Quota!.Primary!.RemainingPercent - 64.5) < 0.001, "Quota remaining was not calculated.");
    Check(parsed.Quota.FiveHour?.WindowMinutes == 300, "Five-hour quota was not classified by duration.");
    Check(parsed.Quota.Weekly?.WindowMinutes == 10_080, "Weekly quota was not classified by duration.");
    var weeklyOnly = new AccountQuota(new RateLimitWindow(31, 10_080, null), null, "pro", false, null);
    Check(weeklyOnly.FiveHour is null && weeklyOnly.Weekly?.RemainingPercent == 69, "A weekly primary window was mislabeled as five-hour quota.");
    Check(BudgetAnalyzer.Analyze([parsed], new BudgetPolicy()).Load == 89_000, "Reasoning tokens were not excluded from context load.");
    return Task.CompletedTask;
}

static Task BudgetBoundariesAsync()
{
    var policy = new BudgetPolicy();
    Check(policy.GetZone(99_999) == BudgetZone.Healthy, "Healthy boundary is wrong.");
    Check(policy.GetZone(100_000) == BudgetZone.Caution, "Caution boundary is wrong.");
    Check(policy.GetZone(125_000) == BudgetZone.Critical, "Critical boundary is wrong.");
    Check(policy.GetZone(150_000) == BudgetZone.OverBudget, "Over-budget boundary is wrong.");
    return Task.CompletedTask;
}

static Task VelocityAndSpikeAsync()
{
    var start = DateTimeOffset.Parse("2026-07-20T12:00:00Z");
    var loads = new long[] { 30_000, 32_000, 34_100, 36_000, 50_000 };
    var history = loads.Select((load, index) => new UsageSnapshot(
        start.AddMinutes(index),
        new TokenBreakdown(0, 0, 0, 0, 0, load),
        new TokenBreakdown(0, 0, 0, 0, 0, load),
        258_400,
        null)).ToArray();
    var analysis = BudgetAnalyzer.Analyze(history, new BudgetPolicy());
    Check(analysis.MedianPositiveDelta == 2_050, "Median positive delta is wrong.");
    Check(analysis.HasSpike, "Large latest delta was not detected as a spike.");
    Check(analysis.EventsToNextZone == 25, "Estimated events to caution is wrong.");
    return Task.CompletedTask;
}

static async Task ReadLocalCatalogAsync()
{
    var paths = new CodexPaths();
    var catalog = await new CodexCatalog(paths).LoadAsync();
    Check(catalog.Threads.Count > 0, "No local Codex metadata was found.");
    Check(catalog.Threads.All(item => !string.IsNullOrWhiteSpace(item.Id)), "A thread is missing its identifier.");

    var reader = new RolloutReader();
    var recentRollouts = catalog.Threads
        .Where(item => File.Exists(item.RolloutPath))
        .OrderByDescending(item => item.UpdatedAt)
        .Take(20);
    foreach (var thread in recentRollouts)
    {
        var history = await reader.ReadRecentUsageAsync(thread.RolloutPath, maxEvents: 8);
        if (history.Count > 0)
        {
            return;
        }
    }

    throw new InvalidOperationException("No token-count events were found in the recent readable rollouts.");
}

static async Task BuildLiveSnapshotAsync()
{
    var paths = new CodexPaths();
    var builder = new SnapshotBuilder(new CodexCatalog(paths), new UsageCache(new RolloutReader()));
    var snapshot = await builder.BuildAsync(null, new BudgetPolicy(), includeArchived: false, search: null);
    Check(snapshot.Tracked is not null, "No tracked top-level chat was selected.");
    Check(snapshot.RecentChats.Count is > 0 and <= 6, "Recent chat selection is outside the expected range.");
    Check(snapshot.ParsedRolloutCount > 0, "No rollout token events were parsed into the snapshot.");
}

static async Task VerifyDiagnosticPrivacyAsync()
{
    var home = Directory.CreateTempSubdirectory("tokenhud-diagnostics-");
    try
    {
        var paths = new AppDataPaths(home.FullName);
        await File.WriteAllTextAsync(paths.Diagnostics, "The diagnostics directory is intentionally blocked.");
        var diagnostics = new RollingDiagnostics(paths);
        await diagnostics.WriteAsync("fixture-blocked-output");

        var now = DateTimeOffset.UtcNow;
        var root = new ThreadRecord("private-chat-id", @"C:\secret\rollout.jsonl", now, now,
            @"C:\secret\workspace", "Private fixture chat name", 765_432, false, null, null, null, null, null, null);
        var tokens = new TokenBreakdown(765_432, 0, 0, 0, 0, 765_432);
        var usage = new UsageSnapshot(now, tokens, tokens, null, null);
        var tracked = new ChatUsage(root, usage, BudgetAnalyzer.Analyze([usage], new BudgetPolicy()),
            tokens, [], null, now);
        var snapshot = new HudSnapshot(tracked, [tracked], [root], null, now, true, 1, "Private fixture source");
        var summary = diagnostics.BuildSummary(snapshot);
        Check(summary.Contains("Readable rollout count: 1", StringComparison.Ordinal),
            "The diagnostic summary did not include operational counts.");
        foreach (var forbidden in new[] { root.Id, root.RolloutPath, root.Cwd, root.Title, snapshot.SourceStatus, "765432" })
        {
            Check(!summary.Contains(forbidden, StringComparison.Ordinal), "The diagnostic summary exposed a non-operational field.");
        }
    }
    finally
    {
        home.Delete(recursive: true);
    }
}

static void Check(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
