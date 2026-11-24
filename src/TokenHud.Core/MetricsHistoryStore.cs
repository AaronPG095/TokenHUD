using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace TokenHud.Core;

public enum MetricsHistoryRetention
{
    Off,
    Days7,
    Days30,
    Days90,
    Indefinite,
}

public sealed record MetricsHistoryPoint(
    DateTimeOffset ObservedAt,
    long ContextLoad,
    long EffectiveBudget,
    BudgetZone Zone);

public sealed record MetricsHistorySample(string ChatId, MetricsHistoryPoint Point)
{
    public static MetricsHistorySample? FromChat(ChatUsage chat) =>
        chat.Latest is { HasContext: true } latest && chat.Analysis.Zone != BudgetZone.Unavailable
            ? new MetricsHistorySample(chat.Root.Id, new MetricsHistoryPoint(
                latest.ContextObservedAt, chat.Analysis.Load, chat.Budget.EffectiveTokens, chat.Analysis.Zone))
            : null;
}

public sealed class MetricsHistoryStore(string path, TimeProvider? timeProvider = null)
{
    private const int CodexProvider = 1;
    private readonly string _path = Path.GetFullPath(path);
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task RecordAsync(
        IEnumerable<MetricsHistorySample> samples,
        MetricsHistoryRetention retention,
        CancellationToken cancellationToken = default)
    {
        if (retention == MetricsHistoryRetention.Off)
        {
            return;
        }

        var valid = samples.Where(sample => !string.IsNullOrWhiteSpace(sample.ChatId) &&
                sample.Point.ContextLoad >= 0 && sample.Point.EffectiveBudget > 0 &&
                sample.Point.Zone is >= BudgetZone.Healthy and <= BudgetZone.OverBudget)
            .OrderBy(sample => sample.Point.ObservedAt).ToArray();
        await WithDatabaseAsync(create: valid.Length > 0, connection =>
        {
            using var transaction = connection.BeginTransaction();
            var retainedSince = GetRetentionStart(retention);
            Prune(connection, transaction, retainedSince);
            using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT OR IGNORE INTO samples (provider, chat_key, observed_at, context_load, budget, zone)
                SELECT $provider, $chat, $observed, $load, $budget, $zone
                WHERE $observed > COALESCE((SELECT erased_at FROM history_state WHERE id = 1), 0)
                  AND NOT EXISTS (
                      SELECT 1 FROM (
                          SELECT context_load, budget, zone FROM samples
                          WHERE provider = $provider AND chat_key = $chat AND observed_at < $observed
                          ORDER BY observed_at DESC LIMIT 1
                      ) AS previous
                      WHERE previous.context_load = $load AND previous.budget = $budget AND previous.zone = $zone
                  );
                """;
            insert.Parameters.AddWithValue("$provider", CodexProvider);
            var chatKey = insert.Parameters.Add("$chat", SqliteType.Blob);
            var observed = insert.Parameters.Add("$observed", SqliteType.Integer);
            var load = insert.Parameters.Add("$load", SqliteType.Integer);
            var budget = insert.Parameters.Add("$budget", SqliteType.Integer);
            var zone = insert.Parameters.Add("$zone", SqliteType.Integer);
            foreach (var sample in valid)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var point = sample.Point;
                if (point.ObservedAt.UtcTicks < retainedSince)
                {
                    continue;
                }

                chatKey.Value = GetChatKey(sample.ChatId);
                observed.Value = point.ObservedAt.UtcTicks;
                load.Value = point.ContextLoad;
                budget.Value = point.EffectiveBudget;
                zone.Value = (int)point.Zone;
                insert.ExecuteNonQuery();
            }

            transaction.Commit();
            return true;
        }, whenMissing: false, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<MetricsHistoryPoint>> ReadRecentAsync(
        string chatId,
        MetricsHistoryRetention retention,
        int maximumPoints = 40,
        CancellationToken cancellationToken = default)
    {
        if (retention == MetricsHistoryRetention.Off || maximumPoints <= 0 || string.IsNullOrWhiteSpace(chatId))
        {
            return Task.FromResult<IReadOnlyList<MetricsHistoryPoint>>([]);
        }

        return WithDatabaseAsync<IReadOnlyList<MetricsHistoryPoint>>(create: false, connection =>
        {
            using var query = connection.CreateCommand();
            query.CommandText = """
                SELECT observed_at, context_load, budget, zone FROM samples
                WHERE provider = $provider AND chat_key = $chat AND observed_at >= $start
                ORDER BY observed_at DESC LIMIT $limit;
                """;
            query.Parameters.AddWithValue("$provider", CodexProvider);
            query.Parameters.AddWithValue("$chat", GetChatKey(chatId));
            query.Parameters.AddWithValue("$start", GetRetentionStart(retention));
            query.Parameters.AddWithValue("$limit", maximumPoints);
            using var reader = query.ExecuteReader();
            var points = new List<MetricsHistoryPoint>();
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                points.Add(new MetricsHistoryPoint(new DateTimeOffset(reader.GetInt64(0), TimeSpan.Zero),
                    reader.GetInt64(1), reader.GetInt64(2), (BudgetZone)reader.GetInt32(3)));
            }

            points.Reverse();
            return points;
        }, whenMissing: [], cancellationToken);
    }

    public async Task ApplyRetentionAsync(
        MetricsHistoryRetention retention,
        CancellationToken cancellationToken = default)
    {
        if (retention is MetricsHistoryRetention.Off or MetricsHistoryRetention.Indefinite)
        {
            return;
        }

        await WithDatabaseAsync(create: false, connection =>
        {
            using var transaction = connection.BeginTransaction();
            Prune(connection, transaction, GetRetentionStart(retention));
            transaction.Commit();
            return true;
        }, whenMissing: false, cancellationToken).ConfigureAwait(false);
    }

    public async Task EraseAsync(CancellationToken cancellationToken = default)
    {
        await WithDatabaseAsync(create: true, connection =>
        {
            using var transaction = connection.BeginTransaction();
            using var erase = connection.CreateCommand();
            erase.Transaction = transaction;
            erase.CommandText = """
                DELETE FROM samples;
                INSERT OR IGNORE INTO history_state (id, erased_at) VALUES (1, $now);
                UPDATE history_state SET erased_at = MAX(erased_at, $now) WHERE id = 1;
                """;
            erase.Parameters.AddWithValue("$now", _time.GetUtcNow().UtcTicks);
            erase.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }, whenMissing: false, cancellationToken).ConfigureAwait(false);
    }

    private async Task<T> WithDatabaseAsync<T>(
        bool create,
        Func<SqliteConnection, T> operation,
        T whenMissing,
        CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!create && !File.Exists(_path))
            {
                return whenMissing;
            }

            CodexCatalog.EnsureProvider();
            if (create)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            }

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = _path,
                Mode = create ? SqliteOpenMode.ReadWriteCreate : SqliteOpenMode.ReadWrite,
                Pooling = false,
            };
            using var connection = new SqliteConnection(connectionString.ConnectionString);
            connection.Open();
            using (var schema = connection.CreateCommand())
            {
                schema.CommandText = """
                    PRAGMA secure_delete = ON;
                    CREATE TABLE IF NOT EXISTS samples (
                        provider INTEGER NOT NULL,
                        chat_key BLOB NOT NULL,
                        observed_at INTEGER NOT NULL,
                        context_load INTEGER NOT NULL,
                        budget INTEGER NOT NULL,
                        zone INTEGER NOT NULL,
                        PRIMARY KEY (provider, chat_key, observed_at)
                    ) WITHOUT ROWID;
                    CREATE INDEX IF NOT EXISTS samples_by_time ON samples (observed_at);
                    CREATE TABLE IF NOT EXISTS history_state (
                        id INTEGER PRIMARY KEY CHECK (id = 1),
                        erased_at INTEGER NOT NULL
                    );
                    """;
                schema.ExecuteNonQuery();
            }

            cancellationToken.ThrowIfCancellationRequested();
            return operation(connection);
        }
        finally
        {
            _gate.Release();
        }
    }

    private long GetRetentionStart(MetricsHistoryRetention retention)
    {
        var days = retention switch
        {
            MetricsHistoryRetention.Days7 => 7,
            MetricsHistoryRetention.Days30 => 30,
            MetricsHistoryRetention.Days90 => 90,
            _ => 0,
        };
        return days == 0 ? 0 : Math.Max(0, _time.GetUtcNow().UtcTicks - TimeSpan.FromDays(days).Ticks);
    }

    private static void Prune(SqliteConnection connection, SqliteTransaction transaction, long retainedSince)
    {
        if (retainedSince <= 0)
        {
            return;
        }

        using var delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText = "DELETE FROM samples WHERE observed_at < $start;";
        delete.Parameters.AddWithValue("$start", retainedSince);
        delete.ExecuteNonQuery();
    }

    private static byte[] GetChatKey(string chatId) => SHA256.HashData(Encoding.UTF8.GetBytes(chatId));
}
