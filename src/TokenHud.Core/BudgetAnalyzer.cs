namespace TokenHud.Core;

public sealed record BudgetPolicy(
    long Budget = 150_000,
    double CautionAt = 100_000d / 150_000d,
    double CriticalAt = 125_000d / 150_000d)
{
    public BudgetZone GetZone(long load)
    {
        if (load >= Budget)
        {
            return BudgetZone.OverBudget;
        }

        if (load >= Math.Round(Budget * CriticalAt))
        {
            return BudgetZone.Critical;
        }

        if (load >= Math.Round(Budget * CautionAt))
        {
            return BudgetZone.Caution;
        }

        return BudgetZone.Healthy;
    }

    public (long Threshold, string Name)? GetNextZone(long load) => GetZone(load) switch
    {
        BudgetZone.Healthy => ((long)Math.Round(Budget * CautionAt), "caution"),
        BudgetZone.Caution => ((long)Math.Round(Budget * CriticalAt), "critical"),
        BudgetZone.Critical => (Budget, "budget"),
        _ => null,
    };
}

public static class BudgetAnalyzer
{
    public static ContextAnalysis Analyze(IReadOnlyList<UsageSnapshot> history, BudgetPolicy policy)
    {
        if (history.Count == 0)
        {
            return new ContextAnalysis(0, 0, BudgetZone.Healthy, 0, 0, null, null, false, 0, null);
        }

        var ordered = history.OrderBy(item => item.Timestamp).ToArray();
        var latest = ordered[^1];
        var load = latest.Context.ContextWindowTokens;
        var deltas = new List<(long Delta, DateTimeOffset At)>();

        for (var index = 1; index < ordered.Length; index++)
        {
            var delta = ordered[index].Context.ContextWindowTokens - ordered[index - 1].Context.ContextWindowTokens;
            if (delta > 0)
            {
                deltas.Add((delta, ordered[index].Timestamp));
            }
        }

        var recentPositive = deltas.TakeLast(8).Select(item => item.Delta).Order().ToArray();
        var median = Median(recentPositive);
        var latestDelta = ordered.Length > 1
            ? ordered[^1].Context.ContextWindowTokens - ordered[^2].Context.ContextWindowTokens
            : 0;
        var spikeThreshold = Math.Max(8_000, (long)Math.Ceiling(median * 2.5));
        var hasSpike = latestDelta > 0 && latestDelta >= spikeThreshold && recentPositive.Length >= 2;

        var velocityWindow = ordered.TakeLast(10).ToArray();
        var elapsedMinutes = velocityWindow.Length > 1
            ? (velocityWindow[^1].Timestamp - velocityWindow[0].Timestamp).TotalMinutes
            : 0;
        var netChange = velocityWindow.Length > 1
            ? velocityWindow[^1].Context.ContextWindowTokens - velocityWindow[0].Context.ContextWindowTokens
            : 0;
        var perMinute = elapsedMinutes > 0.05 && netChange > 0 ? netChange / elapsedMinutes : 0;

        var next = policy.GetNextZone(load);
        int? eventsToNext = null;
        if (next is not null && median > 0)
        {
            eventsToNext = (int)Math.Max(1, Math.Ceiling((next.Value.Threshold - load) / (double)median));
        }

        return new ContextAnalysis(
            load,
            policy.Budget == 0 ? 0 : load * 100d / policy.Budget,
            policy.GetZone(load),
            median,
            perMinute,
            eventsToNext,
            next?.Name,
            hasSpike,
            latestDelta,
            hasSpike ? latest.Timestamp : null);
    }

    private static long Median(IReadOnlyList<long> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var middle = values.Count / 2;
        return values.Count % 2 == 0
            ? (long)Math.Round((values[middle - 1] + values[middle]) / 2d)
            : values[middle];
    }
}
