namespace TokenHud.Core;

public enum ProviderFreshness
{
    Fresh,
    Stale,
}

public enum ProviderImportResult
{
    Imported,
    Invalid,
    Unavailable,
}

public sealed record ProviderContext(long? Tokens, long? CapacityTokens, decimal? UsedPercent);

public sealed record ProviderExchange(
    long? InputTokens,
    long? OutputTokens,
    long? CachedInputTokens,
    long? ReasoningOutputTokens);

public sealed record ProviderQuota(
    string Kind,
    decimal? RemainingPercent,
    decimal? RemainingAmount,
    string? Unit,
    DateTimeOffset? ResetsAt)
{
    public bool IsExpired(DateTimeOffset now) => ResetsAt is { } reset && reset <= now;
}

public sealed record ProviderReading(
    string SourceId,
    string SessionKey,
    DateTimeOffset ObservedAt,
    ProviderContext? CurrentContext,
    ProviderContext? HistoricalContext,
    ProviderExchange? LatestExchange,
    IReadOnlyList<ProviderQuota> AccountQuotas)
{
    public ProviderFreshness GetFreshness(DateTimeOffset now) =>
        now - ObservedAt >= TimeSpan.FromMinutes(5) ? ProviderFreshness.Stale : ProviderFreshness.Fresh;
}
