using TokenHud.Core;

internal static class SnapshotTests
{
    public static async Task KeepsSixRecentTopLevelChatsAsync()
    {
        var directory = Directory.CreateTempSubdirectory("tokenhud-snapshot-");
        try
        {
            var paths = new CodexPaths(directory.FullName);
            var catalog = new CodexCatalog(paths);
            await WriteCatalogAsync(paths);

            var builder = new SnapshotBuilder(catalog, new UsageCache(new RolloutReader()));
            var snapshot = await builder.BuildAsync(null, new BudgetPolicy(), includeArchived: true, search: null);

            Check(snapshot.StateDatabaseAvailable, "The fixture should exercise SQLite catalog behavior.");
            Check(snapshot.RecentChats.Select(chat => chat.Root.Id).SequenceEqual(
                ["root-7", "root-1", "root-2", "root-3", "root-4", "root-5"]),
                "The six recent roots should follow tree activity and exclude archived roots and children.");
            Check(snapshot.Tracked?.Root.Id == "root-7", "The most recently active tree should be tracked by default.");
            Check(snapshot.SearchableChats.Any(chat => chat.Id == "archived"), "Archived roots should remain searchable when included.");

            var pinned = await builder.BuildAsync("root-6", new BudgetPolicy(), includeArchived: false, search: null);
            Check(pinned.Tracked?.Root.Id == "root-6", "An older root should still be available for detail.");
            Check(pinned.RecentChats.Select(chat => chat.Root.Id).SequenceEqual(snapshot.RecentChats.Select(chat => chat.Root.Id)),
                "Tracking an older root should not change the Compact list.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    public static async Task RetainsIndependentQuotaWindowsAsync()
    {
        var directory = Directory.CreateTempSubdirectory("tokenhud-quota-");
        try
        {
            var paths = new CodexPaths(directory.FullName);
            Directory.CreateDirectory(paths.Sessions);
            var rollout = Path.Combine(paths.Sessions, "rollout-dcc6f47d-1255-4d59-9ad2-283dfacdcb55.jsonl");
            var now = DateTimeOffset.UtcNow;
            await File.WriteAllLinesAsync(rollout,
            [
                $$"""{"timestamp":"{{now.AddMinutes(-5):O}}","type":"event_msg","payload":{"type":"token_count","rate_limits":{"primary":{"used_percent":20,"window_minutes":10080} } } }""",
                $$"""{"timestamp":"{{now.AddMinutes(-2):O}}","type":"event_msg","payload":{"type":"token_count","rate_limits":{"secondary":{"used_percent":55,"window_minutes":300} } } }""",
                $$"""{"timestamp":"{{now.AddMinutes(-1):O}}","type":"event_msg","payload":{"type":"token_count","info":{"last_token_usage":{"total_tokens":1000} } } }""",
            ]);

            var builder = new SnapshotBuilder(new CodexCatalog(paths), new UsageCache(new RolloutReader()));
            var snapshot = await builder.BuildAsync(null, new BudgetPolicy(), includeArchived: false, search: null);

            Check(snapshot.Quota?.FiveHour?.RemainingPercent == 45, "A later context event should not erase five-hour quota.");
            Check(snapshot.Quota?.Weekly?.RemainingPercent == 80, "A partial quota should not erase a current weekly observation.");
        }
        finally
        {
            directory.Delete(recursive: true);
        }
    }

    private static async Task WriteCatalogAsync(CodexPaths paths)
    {
        using var connection = CatalogFixtures.OpenStateDatabase(paths);
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO thread_spawn_edges VALUES ('root-7', 'child', 'closed'), ('child', 'grandchild', 'closed');
            INSERT INTO threads
                (id, rollout_path, created_at, updated_at, cwd, title, tokens_used, archived, recency_at)
            VALUES
                ('root-1', '', 1, 107, '', 'Root 1', 0, 0, 107),
                ('root-2', '', 1, 106, '', 'Root 2', 0, 0, 106),
                ('root-3', '', 1, 105, '', 'Root 3', 0, 0, 105),
                ('root-4', '', 1, 104, '', 'Root 4', 0, 0, 104),
                ('root-5', '', 1, 103, '', 'Root 5', 0, 0, 103),
                ('root-6', '', 1, 102, '', 'Root 6', 0, 0, 102),
                ('root-7', '', 1, 101, '', 'Root 7', 0, 0, 101),
                ('child', '', 1, 101, '', 'Child', 0, 0, 101),
                ('grandchild', '', 1, 108, '', 'Grandchild', 0, 0, 108),
                ('archived', '', 1, 109, '', 'Archived', 0, 1, 109);
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}