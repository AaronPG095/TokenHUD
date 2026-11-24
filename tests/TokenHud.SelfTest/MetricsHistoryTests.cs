using System.Text;
using TokenHud.Core;

internal static class MetricsHistoryTests
{
    public static async Task StoresOnlyRetainedMeasurementsAsync()
    {
        var home = Directory.CreateTempSubdirectory("tokenhud-history-");
        try
        {
            var path = new AppDataPaths(home.FullName).MetricsHistory;
            var clock = new HistoryClock(new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero));
            var now = clock.GetUtcNow();
            var store = new MetricsHistoryStore(path, clock);
            const string id = "fixture-private-opaque-chat-id";
            MetricsHistorySample Sample(DateTimeOffset at, long load, BudgetZone zone = BudgetZone.Healthy) =>
                new(id, new MetricsHistoryPoint(at, load, 150_000, zone));

            await store.RecordAsync([Sample(now.AddMinutes(-1), 10)], MetricsHistoryRetention.Off);
            Check(!File.Exists(path), "Off created a local metrics database.");
            await store.RecordAsync(
            [
                Sample(now.AddDays(-8), 25_000),
                Sample(now.AddHours(-1), 90_000),
                Sample(now.AddHours(-1), 90_000),
                Sample(now.AddMinutes(-30), 90_000),
                Sample(now.AddMinutes(-15), 0),
                Sample(now.AddMinutes(-10), 0, BudgetZone.Unavailable),
            ], MetricsHistoryRetention.Days30);
            var points = await store.ReadRecentAsync(id, MetricsHistoryRetention.Days30);
            Check(points.Select(point => point.ContextLoad).SequenceEqual([25_000L, 90_000L, 0L]),
                "Repeated or unavailable samples affected the timeline, or a real zero was lost.");
            Check((await store.ReadRecentAsync(id, MetricsHistoryRetention.Off)).Count == 0, "Off exposed stored history.");
            Check(!Encoding.UTF8.GetString(await File.ReadAllBytesAsync(path)).Contains(id, StringComparison.Ordinal),
                "The store retained a source chat identifier instead of a derived opaque key.");

            await store.ApplyRetentionAsync(MetricsHistoryRetention.Days7);
            Check((await store.ReadRecentAsync(id, MetricsHistoryRetention.Indefinite)).Count == 2,
                "Applying shorter retention did not delete the expired sample.");

            await store.EraseAsync();
            var reopened = new MetricsHistoryStore(path, clock);
            await reopened.RecordAsync([Sample(now.AddMinutes(-1), 120_000)], MetricsHistoryRetention.Days30);
            Check((await reopened.ReadRecentAsync(id, MetricsHistoryRetention.Days30)).Count == 0,
                "A pre-clear sample reappeared after reopening.");
            await reopened.RecordAsync([Sample(now.AddSeconds(1), 140_000, BudgetZone.Critical)], MetricsHistoryRetention.Days30);
            Check((await reopened.ReadRecentAsync(id, MetricsHistoryRetention.Days30)).Single().Zone == BudgetZone.Critical,
                "A new reading after erase was not recorded.");
        }
        finally
        {
            home.Delete(recursive: true);
        }
    }

    private static void Check(bool test, string message)
    {
        if (!test)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class HistoryClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
