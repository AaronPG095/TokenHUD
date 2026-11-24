using Microsoft.Data.Sqlite;
using TokenHud.Core;

internal static class CatalogFixtures
{
    internal static async Task ResolveCurrentNamesAsync()
    {
        const string namedId = "dcc6f47d-1255-4d59-9ad2-283dfacdcb55";
        const string unnamedId = "eed39ccf-a152-44e4-aa1b-af21f612c941";
        var home = Directory.CreateTempSubdirectory("tokenhud-names-");
        try
        {
            var paths = new CodexPaths(home.FullName);
            await File.WriteAllTextAsync(paths.SessionIndex, $$"""
                {"id":"{{namedId}}","thread_name":"First name","updated_at":"2026-11-01T11:00:00Z"}
                {"id":"{{namedId}}","thread_name":"Earlier append at same time","updated_at":"2026-11-01T12:00:00Z"}
                {"id":"{{namedId}}","thread_name":"Current chat name","updated_at":"2026-11-01T13:00:00+01:00"}
                {"id":"{{namedId}}","thread_name":"Late older append","updated_at":"2026-11-01T11:45:00Z"}
                {"id":"{{namedId}}","thread_name":"   ","updated_at":"2026-11-01T14:00:00Z"}
                {"id":"{{namedId}}","thread_name":17,"updated_at":"2026-11-01T14:00:00Z"}
                {"id":"{{namedId}}","thread_name":"Bad timestamp type","updated_at":[]}
                {"id":"{{namedId}}","thread_name":"Bad timestamp text","updated_at":"yesterday"}
                {"id":"{{namedId}}","thread_name":null,"updated_at":"2026-11-01T14:00:00Z"}
                {"id":"{{namedId}}","thread_name":"\uD83D","updated_at":"2026-11-01T14:00:00Z"}
                {"id":{"value":"{{namedId}}"},"thread_name":"Bad identifier type","updated_at":"2026-11-01T14:00:00Z"}
                {"id":"{{namedId}}","thread_name":"Partial record"
                []
                {"id":"{{unnamedId}}","updated_at":"2026-11-01T14:00:00Z"}
                """);

            Directory.CreateDirectory(paths.Sessions);
            var namedRollout = Path.Combine(paths.Sessions, $"rollout-{namedId}.jsonl");
            var unnamedRollout = Path.Combine(paths.Sessions, $"rollout-{unnamedId}.jsonl");
            await File.WriteAllTextAsync(namedRollout, "Synthetic rollout content");
            await File.WriteAllTextAsync(unnamedRollout, "Synthetic rollout content");
            var unnamedActivity = new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero);
            File.SetLastWriteTimeUtc(unnamedRollout, unnamedActivity.UtcDateTime);

            var catalog = new CodexCatalog(paths);
            var fallback = await catalog.LoadAsync();
            ValidateNames(fallback, namedId, unnamedId, databaseAvailable: false);
            if (fallback.Threads.Single(thread => thread.Id == unnamedId).UpdatedAt != unnamedActivity ||
                fallback.Threads.Single(thread => thread.Id == namedId).UpdatedAt != unnamedActivity.AddHours(2))
            {
                throw new InvalidOperationException("Index-only catalog activity did not use the valid rename or file timestamp.");
            }

            using (var connection = OpenStateDatabase(paths))
            using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                    INSERT INTO threads (id, rollout_path, title, first_user_message, preview)
                    VALUES ($namedId, $namedRollout, 'Previous database title', 'Synthetic message', 'Synthetic preview'),
                           ($unnamedId, $unnamedRollout, 'Synthetic message', 'Synthetic message', 'Synthetic preview')
                    """;
                command.Parameters.AddWithValue("$namedId", namedId);
                command.Parameters.AddWithValue("$namedRollout", namedRollout);
                command.Parameters.AddWithValue("$unnamedId", unnamedId);
                command.Parameters.AddWithValue("$unnamedRollout", unnamedRollout);
                command.ExecuteNonQuery();
            }

            ValidateNames(await catalog.LoadAsync(), namedId, unnamedId, databaseAvailable: true);
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    internal static SqliteConnection OpenStateDatabase(CodexPaths paths)
    {
        _ = new CodexCatalog(paths);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(paths.Home, "state_5.sqlite"),
            Pooling = false,
        };
        var connection = new SqliteConnection(connectionString.ConnectionString);
        try
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE threads (
                    id TEXT PRIMARY KEY, rollout_path TEXT NOT NULL, cwd TEXT NOT NULL DEFAULT '',
                    created_at INTEGER NOT NULL DEFAULT 0, updated_at INTEGER NOT NULL DEFAULT 0,
                    recency_at INTEGER NOT NULL DEFAULT 0, created_at_ms INTEGER, updated_at_ms INTEGER,
                    recency_at_ms INTEGER, tokens_used INTEGER NOT NULL DEFAULT 0, archived INTEGER NOT NULL DEFAULT 0,
                    model TEXT, reasoning_effort TEXT, agent_nickname TEXT, agent_role TEXT,
                    title TEXT, first_user_message TEXT, preview TEXT
                );
                CREATE TABLE thread_spawn_edges (parent_thread_id TEXT, child_thread_id TEXT, status TEXT);
                """;
            command.ExecuteNonQuery();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void ValidateNames(CatalogResult catalog, string namedId, string unnamedId, bool databaseAvailable)
    {
        if (catalog.DatabaseAvailable != databaseAvailable || catalog.Threads.Count != 2)
        {
            throw new InvalidOperationException("The chat-name fixture did not load through the expected catalog path.");
        }

        if (catalog.Threads.Single(thread => thread.Id == namedId).Title != "Current chat name")
        {
            throw new InvalidOperationException("The latest valid name did not survive the out-of-order and malformed entries.");
        }

        if (catalog.Threads.Single(thread => thread.Id == unnamedId).Title != string.Empty)
        {
            throw new InvalidOperationException("The unnamed chat did not leave its title empty for the neutral display fallback.");
        }
    }
}
