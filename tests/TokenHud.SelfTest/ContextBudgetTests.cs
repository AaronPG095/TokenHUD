using TokenHud.Core;

internal static class ContextBudgetTests
{
    public static async Task ResolvesHierarchyAndReportedLimitsAsync()
    {
        var profiles = new ContextBudgetProfiles(120_000, new Dictionary<string, long>
        {
            ["root"] = 200_000,
            ["invalid"] = 0,
        });
        Check(new ContextBudgetProfiles().Resolve(150_000, "root", null) == ResolvedContextBudget.Default,
            "The default profile should preserve the installation budget without a model limit.");
        Check(profiles.Resolve(150_000, "invalid", 300_000) ==
            new ResolvedContextBudget(120_000, 120_000, ContextBudgetSource.Codex, 300_000),
            "An invalid chat value should fall back to Codex, and higher limits should leave the budget unchanged.");
        Check(profiles.Resolve(150_000, "root", 2) ==
            new ResolvedContextBudget(200_000, 1, ContextBudgetSource.Chat, 2),
            "A tiny valid model limit should produce a positive budget strictly below it.");

        var directory = Directory.CreateTempSubdirectory("tokenhud-context-budgets-");
        try
        {
            var paths = new CodexPaths(directory.FullName);
            Directory.CreateDirectory(paths.Sessions);
            var rootRollout = Path.Combine(paths.Sessions, "root.jsonl");
            var childRollout = Path.Combine(paths.Sessions, "child.jsonl");
            var now = DateTimeOffset.UtcNow;
            await File.WriteAllLinesAsync(rootRollout,
            [
                $$"""{"timestamp":"{{now.AddMinutes(-3):O}}","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":260000,"last_token_usage":{"total_tokens":10000} } } }""",
                $$"""{"timestamp":"{{now.AddMinutes(-2):O}}","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":100001,"last_token_usage":{"total_tokens":90000} } } }""",
                $$"""{"timestamp":"{{now.AddMinutes(-1):O}}","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"total_tokens":95000} } } }""",
            ]);
            await File.WriteAllTextAsync(childRollout,
                $$"""{"timestamp":"{{now:O}}","type":"event_msg","payload":{"type":"token_count","info":{"model_context_window":50000,"last_token_usage":{"total_tokens":32000} } } }""");

            using (var connection = CatalogFixtures.OpenStateDatabase(paths))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO threads (id, rollout_path, updated_at, title)
                    VALUES ('root', $root, $now, 'Test chat'),
                           ('child', $child, $now, 'Active child'),
                           ('other', '', $now, 'No measurements');
                    INSERT INTO thread_spawn_edges VALUES ('root', 'child', 'open');
                    """;
                command.Parameters.AddWithValue("$root", rootRollout);
                command.Parameters.AddWithValue("$child", childRollout);
                command.Parameters.AddWithValue("$now", now.ToUnixTimeSeconds());
                await command.ExecuteNonQueryAsync();
            }

            var builder = new SnapshotBuilder(new CodexCatalog(paths), new UsageCache(new RolloutReader()));
            var snapshot = await builder.BuildAsync("root", new BudgetPolicy(), includeArchived: false, search: null, profiles: profiles);
            var root = snapshot.Tracked ?? throw new InvalidOperationException("The selected fixture chat was not found.");
            Check(root.Budget == new ResolvedContextBudget(200_000, 95_000, ContextBudgetSource.Chat, 100_001) &&
                root.Budget.IsCapped && root.Analysis.Zone == BudgetZone.OverBudget && root.Analysis.BudgetPercent == 100,
                "The newest reported limit should cap the selected chat across later samples that omit the limit.");

            var child = root.Threads.Single(thread => thread.Thread.Id == "child");
            Check(child.Budget == new ResolvedContextBudget(200_000, 47_500, ContextBudgetSource.Chat, 50_000) &&
                child.Analysis.Zone == BudgetZone.Caution,
                "A child should inherit the chat profile and use its own model limit for relative zones.");

            var other = snapshot.RecentChats.Single(chat => chat.Root.Id == "other");
            Check(other.Budget == new ResolvedContextBudget(120_000, 120_000, ContextBudgetSource.Codex, null) &&
                other.Analysis.Zone == BudgetZone.Unavailable,
                "Missing usage should still expose the inherited budget without a model cap or measurement.");
        }
        finally
        {
            directory.Delete(recursive: true);
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
