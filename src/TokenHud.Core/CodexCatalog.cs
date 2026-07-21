using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TokenHud.Core;

public sealed record CatalogResult(IReadOnlyList<ThreadRecord> Threads, bool DatabaseAvailable, string Status);

public sealed class CodexPaths
{
    public CodexPaths(string? codexHome = null)
    {
        Home = string.IsNullOrWhiteSpace(codexHome)
            ? Environment.GetEnvironmentVariable("CODEX_HOME") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex")
            : codexHome;
    }

    public string Home { get; }
    public string SessionIndex => Path.Combine(Home, "session_index.jsonl");
    public string Sessions => Path.Combine(Home, "sessions");
    public string ArchivedSessions => Path.Combine(Home, "archived_sessions");

    public string? FindStateDatabase() => Directory.Exists(Home)
        ? Directory.EnumerateFiles(Home, "state_*.sqlite", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault()
        : null;
}

public sealed class CodexCatalog
{
    private static readonly object ProviderLock = new();
    private static bool _providerInitialized;
    private readonly CodexPaths _paths;

    public CodexCatalog(CodexPaths paths)
    {
        EnsureProvider();
        _paths = paths;
    }

    public async Task<CatalogResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var index = await LoadSessionIndexAsync(cancellationToken).ConfigureAwait(false);
        var database = _paths.FindStateDatabase();
        if (database is not null)
        {
            try
            {
                return await LoadFromDatabaseAsync(database, index, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is SqliteException or IOException)
            {
                return LoadFallback($"State database unavailable ({exception.GetType().Name}); using session index.", index);
            }
        }

        return LoadFallback("State database not found; using session index.", index);
    }

    private static async Task<CatalogResult> LoadFromDatabaseAsync(
        string database,
        IReadOnlyDictionary<string, (string Title, DateTimeOffset Updated)> index,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = database,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false,
        };

        await using var connection = new SqliteConnection(builder.ConnectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var parents = new Dictionary<string, (string Parent, string Status)>(StringComparer.Ordinal);
        await using (var edgeCommand = connection.CreateCommand())
        {
            edgeCommand.CommandText = "SELECT parent_thread_id, child_thread_id, status FROM thread_spawn_edges";
            await using var reader = await edgeCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                parents[reader.GetString(1)] = (reader.GetString(0), reader.GetString(2));
            }
        }

        var threads = new List<ThreadRecord>();
        await using (var command = connection.CreateCommand())
        {
            // Intentionally select metadata only. Message and preview columns are never queried.
            command.CommandText = """
                SELECT id, rollout_path, created_at, updated_at, cwd, title, tokens_used, archived,
                       model, reasoning_effort, agent_nickname, agent_role, created_at_ms, updated_at_ms,
                       recency_at, recency_at_ms
                FROM threads
                ORDER BY recency_at_ms DESC, updated_at_ms DESC
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var id = reader.GetString(0);
                parents.TryGetValue(id, out var edge);
                index.TryGetValue(id, out var indexed);
                var chatName = string.IsNullOrWhiteSpace(indexed.Title) ? reader.GetString(5) : indexed.Title;
                threads.Add(new ThreadRecord(
                    id,
                    reader.GetString(1),
                    ReadTimestamp(reader, 12, 2),
                    Max(ReadTimestamp(reader, 13, 3), ReadTimestamp(reader, 15, 14)),
                    reader.GetString(4),
                    chatName,
                    reader.GetInt64(6),
                    reader.GetInt64(7) != 0,
                    reader.IsDBNull(8) ? null : reader.GetString(8),
                    reader.IsDBNull(9) ? null : reader.GetString(9),
                    reader.IsDBNull(10) ? null : reader.GetString(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    edge.Parent,
                    edge.Status));
            }
        }

        return new CatalogResult(threads, true, $"Reading {Path.GetFileName(database)}");
    }

    private async Task<Dictionary<string, (string Title, DateTimeOffset Updated)>> LoadSessionIndexAsync(CancellationToken cancellationToken)
    {
        var index = new Dictionary<string, (string Title, DateTimeOffset Updated)>(StringComparer.Ordinal);
        if (!File.Exists(_paths.SessionIndex))
        {
            return index;
        }

        await foreach (var line in File.ReadLinesAsync(_paths.SessionIndex, cancellationToken).ConfigureAwait(false))
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (!root.TryGetProperty("id", out var idElement) ||
                    !root.TryGetProperty("thread_name", out var titleElement) ||
                    !root.TryGetProperty("updated_at", out var updatedElement))
                {
                    continue;
                }

                var id = idElement.GetString();
                var title = titleElement.GetString();
                var updatedText = updatedElement.GetString();
                if (id is not null && title is not null && DateTimeOffset.TryParse(updatedText, out var updated))
                {
                    index[id] = (title, updated);
                }
            }
            catch (JsonException)
            {
                // Ignore a partially written index line.
            }
        }

        return index;
    }

    private CatalogResult LoadFallback(
        string status,
        IReadOnlyDictionary<string, (string Title, DateTimeOffset Updated)> index)
    {
        var threads = new List<ThreadRecord>();
        AddFallbackFiles(_paths.Sessions, archived: false, index, threads);
        AddFallbackFiles(_paths.ArchivedSessions, archived: true, index, threads);
        return new CatalogResult(threads.OrderByDescending(item => item.UpdatedAt).ToArray(), false, status);
    }

    private static void AddFallbackFiles(
        string directory,
        bool archived,
        IReadOnlyDictionary<string, (string Title, DateTimeOffset Updated)> index,
        ICollection<ThreadRecord> output)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*.jsonl", SearchOption.AllDirectories))
        {
            var id = ExtractId(Path.GetFileNameWithoutExtension(path));
            if (id is null)
            {
                continue;
            }

            var fileTime = File.GetLastWriteTimeUtc(path);
            index.TryGetValue(id, out var indexed);
            var updated = indexed.Updated == default ? new DateTimeOffset(fileTime) : indexed.Updated;
            var title = string.IsNullOrWhiteSpace(indexed.Title) ? string.Empty : indexed.Title;
            output.Add(new ThreadRecord(id, path, updated, updated, string.Empty, title, 0, archived, null, null, null, null, null, null));
        }
    }

    private static string? ExtractId(string fileName)
    {
        for (var index = 0; index <= fileName.Length - 36; index++)
        {
            var candidate = fileName.Substring(index, 36);
            if (Guid.TryParse(candidate, out _))
            {
                return candidate;
            }
        }

        return null;
    }

    private static DateTimeOffset ReadTimestamp(SqliteDataReader reader, int millisecondsIndex, int secondsIndex)
    {
        if (!reader.IsDBNull(millisecondsIndex) && reader.GetInt64(millisecondsIndex) > 0)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(millisecondsIndex));
        }

        return DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(secondsIndex));
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;

    private static void EnsureProvider()
    {
        if (_providerInitialized)
        {
            return;
        }

        lock (ProviderLock)
        {
            if (_providerInitialized)
            {
                return;
            }

            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            SQLitePCL.raw.FreezeProvider(true);
            _providerInitialized = true;
        }
    }
}
