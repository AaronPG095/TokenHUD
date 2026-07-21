namespace TokenHud.Core;

public enum BudgetZone
{
    Healthy,
    Caution,
    Critical,
    OverBudget,
}

public sealed record TokenBreakdown(
    long InputTokens,
    long CachedInputTokens,
    long CacheWriteInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens)
{
    public static TokenBreakdown Empty { get; } = new(0, 0, 0, 0, 0, 0);
    public long ContextWindowTokens => Math.Max(0, TotalTokens - ReasoningOutputTokens);

    public static TokenBreakdown operator +(TokenBreakdown left, TokenBreakdown right) => new(
        left.InputTokens + right.InputTokens,
        left.CachedInputTokens + right.CachedInputTokens,
        left.CacheWriteInputTokens + right.CacheWriteInputTokens,
        left.OutputTokens + right.OutputTokens,
        left.ReasoningOutputTokens + right.ReasoningOutputTokens,
        left.TotalTokens + right.TotalTokens);
}

public sealed record RateLimitWindow(double UsedPercent, int? WindowMinutes, DateTimeOffset? ResetsAt)
{
    public double RemainingPercent => Math.Clamp(100d - UsedPercent, 0d, 100d);
}

public sealed record AccountQuota(
    RateLimitWindow? Primary,
    RateLimitWindow? Secondary,
    string? PlanType,
    bool SpendControlReached,
    string? RateLimitReachedType)
{
    public RateLimitWindow? FiveHour => KnownWindows
        .Where(window => window.WindowMinutes is > 0 and < 1_440)
        .OrderBy(window => Math.Abs(window.WindowMinutes!.Value - 300))
        .FirstOrDefault();

    public RateLimitWindow? Weekly => KnownWindows
        .Where(window => window.WindowMinutes is >= 1_440)
        .OrderBy(window => Math.Abs(window.WindowMinutes!.Value - 10_080))
        .FirstOrDefault();

    private IEnumerable<RateLimitWindow> KnownWindows =>
        new[] { Primary, Secondary }.OfType<RateLimitWindow>();
}

public sealed record UsageSnapshot(
    DateTimeOffset Timestamp,
    TokenBreakdown SessionTotal,
    TokenBreakdown Context,
    long? ModelContextWindow,
    AccountQuota? Quota);

public sealed record ThreadRecord(
    string Id,
    string RolloutPath,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Cwd,
    string Title,
    long CatalogTokensUsed,
    bool Archived,
    string? Model,
    string? ReasoningEffort,
    string? AgentNickname,
    string? AgentRole,
    string? ParentThreadId,
    string? SpawnStatus)
{
    public bool IsTopLevel => string.IsNullOrEmpty(ParentThreadId);
    public bool IsSubagent => !IsTopLevel;

    public bool IsActivelyRunning(DateTimeOffset observedAt) =>
        IsSubagent &&
        string.Equals(SpawnStatus, "open", StringComparison.OrdinalIgnoreCase) &&
        UpdatedAt >= observedAt.Subtract(TimeSpan.FromMinutes(30));
}

public sealed record ContextAnalysis(
    long Load,
    double BudgetPercent,
    BudgetZone Zone,
    long MedianPositiveDelta,
    double TokensPerMinute,
    int? EventsToNextZone,
    string? NextZoneName,
    bool HasSpike,
    long LatestDelta,
    DateTimeOffset? SpikeAt);

public sealed record ThreadBreakdown(
    ThreadRecord Thread,
    UsageSnapshot? Latest,
    ContextAnalysis Analysis,
    int Depth);

public sealed record ChatUsage(
    ThreadRecord Root,
    UsageSnapshot? Latest,
    ContextAnalysis Analysis,
    TokenBreakdown TreeConsumption,
    IReadOnlyList<ThreadBreakdown> Threads,
    ThreadBreakdown? HighestChild,
    DateTimeOffset LastObservedActivity);

public sealed record HudSnapshot(
    ChatUsage? Tracked,
    IReadOnlyList<ChatUsage> RecentChats,
    IReadOnlyList<ThreadRecord> SearchableChats,
    AccountQuota? Quota,
    DateTimeOffset RefreshedAt,
    bool StateDatabaseAvailable,
    int ParsedRolloutCount,
    string SourceStatus);
