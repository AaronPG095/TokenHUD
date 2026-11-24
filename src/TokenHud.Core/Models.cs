namespace TokenHud.Core;

public enum BudgetZone
{
    Healthy,
    Caution,
    Critical,
    OverBudget,
    Unavailable,
}

[Flags]
public enum TokenFields
{
    None = 0,
    Input = 1,
    CachedInput = 2,
    CacheWrite = 4,
    Output = 8,
    Reasoning = 16,
    Total = 32,
    All = Input | CachedInput | CacheWrite | Output | Reasoning | Total,
}

public sealed record TokenBreakdown(
    long InputTokens,
    long CachedInputTokens,
    long CacheWriteInputTokens,
    long OutputTokens,
    long ReasoningOutputTokens,
    long TotalTokens)
{
    public static TokenBreakdown Empty { get; } = new(0, 0, 0, 0, 0, 0) { ReportedFields = TokenFields.None };
    public TokenFields ReportedFields { get; init; } = TokenFields.All;
    public long ContextWindowTokens => Math.Max(0, TotalTokens - ReasoningOutputTokens);

    public bool Reports(TokenFields fields) => (ReportedFields & fields) == fields;

    public long? NewInputTokens =>
        Reports(TokenFields.Input | TokenFields.CachedInput | TokenFields.CacheWrite) &&
        CachedInputTokens <= InputTokens && CacheWriteInputTokens <= InputTokens - CachedInputTokens
            ? InputTokens - CachedInputTokens - CacheWriteInputTokens
            : null;

    public long? VisibleOutputTokens =>
        Reports(TokenFields.Output | TokenFields.Reasoning) && ReasoningOutputTokens <= OutputTokens
            ? OutputTokens - ReasoningOutputTokens
            : null;

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
    public RateLimitWindow? FiveHour => FindWindow(300);
    public RateLimitWindow? Weekly => FindWindow(10_080);

    public static AccountQuota? LatestFrom(IEnumerable<UsageSnapshot> observations, DateTimeOffset asOf)
    {
        AccountQuota? latestQuota = null;
        (RateLimitWindow Window, DateTimeOffset Timestamp)? fiveHour = null;
        (RateLimitWindow Window, DateTimeOffset Timestamp)? weekly = null;

        foreach (var observation in observations.OrderByDescending(observation => observation.Timestamp))
        {
            if (observation.Quota is not { } quota)
            {
                continue;
            }

            latestQuota ??= quota;
            if (fiveHour is null && quota.FiveHour is { } shortWindow)
            {
                fiveHour = (shortWindow, observation.Timestamp);
            }

            if (weekly is null && quota.Weekly is { } longWindow)
            {
                weekly = (longWindow, observation.Timestamp);
            }

            if (fiveHour is not null && weekly is not null)
            {
                break;
            }
        }

        return latestQuota is null ? null : latestQuota with
        {
            Primary = AvailableWindow(fiveHour, asOf),
            Secondary = AvailableWindow(weekly, asOf),
        };
    }

    private RateLimitWindow? FindWindow(int minutes) =>
        Primary?.WindowMinutes == minutes ? Primary :
        Secondary?.WindowMinutes == minutes ? Secondary : null;

    private static RateLimitWindow? AvailableWindow(
        (RateLimitWindow Window, DateTimeOffset Timestamp)? observation,
        DateTimeOffset asOf)
    {
        if (observation is not { } sample || !double.IsFinite(sample.Window.UsedPercent))
        {
            return null;
        }

        var isCurrent = sample.Window.ResetsAt is { } reset
            ? reset > asOf
            : asOf - sample.Timestamp < TimeSpan.FromMinutes(sample.Window.WindowMinutes!.Value);
        return isCurrent ? sample.Window : null;
    }
}

public sealed record UsageSnapshot(
    DateTimeOffset Timestamp,
    TokenBreakdown SessionTotal,
    TokenBreakdown Context,
    long? ModelContextWindow,
    AccountQuota? Quota,
    bool HasContext = true,
    bool HasSessionTotal = true);

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
    int Depth)
{
    public ResolvedContextBudget Budget { get; init; } = ResolvedContextBudget.Default;
}

public sealed record ChatUsage(
    ThreadRecord Root,
    UsageSnapshot? Latest,
    ContextAnalysis Analysis,
    TokenBreakdown TreeConsumption,
    IReadOnlyList<ThreadBreakdown> Threads,
    ThreadBreakdown? HighestChild,
    DateTimeOffset LastObservedActivity)
{
    public ResolvedContextBudget Budget { get; init; } = ResolvedContextBudget.Default;
}

public sealed record HudSnapshot(
    ChatUsage? Tracked,
    IReadOnlyList<ChatUsage> RecentChats,
    IReadOnlyList<ThreadRecord> SearchableChats,
    AccountQuota? Quota,
    DateTimeOffset RefreshedAt,
    bool StateDatabaseAvailable,
    int ParsedRolloutCount,
    string SourceStatus);
