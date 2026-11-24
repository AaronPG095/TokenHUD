using TokenHud.Core;

internal static class QuotaTests
{
    public static Task RunAsync()
    {
        var now = new DateTimeOffset(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);
        var fiveHour = new RateLimitWindow(40, 300, now.AddHours(1));
        var weekly = new RateLimitWindow(20, 10_080, now.AddDays(2));
        var swapped = Quota(weekly, fiveHour);
        Check(swapped.FiveHour == fiveHour && swapped.Weekly == weekly, "Windows depended on slot order.");

        var unrelated = Quota(fiveHour with { WindowMinutes = 299 }, weekly with { WindowMinutes = 1_440 });
        Check(unrelated.FiveHour is null && unrelated.Weekly is null, "Unrelated durations were classified as quotas.");
        var unknown = Quota(fiveHour with { WindowMinutes = null }, null);
        Check(unknown.FiveHour is null && unknown.Weekly is null, "Missing durations were classified as quotas.");

        var mostRecentFiveHour = fiveHour with { UsedPercent = 50 };
        var combined = AccountQuota.LatestFrom(
            [Observe(now.AddMinutes(-30), swapped), Observe(now.AddMinutes(-5), Quota(null, mostRecentFiveHour))],
            now);
        Check(combined?.FiveHour == mostRecentFiveHour && combined.Weekly == weekly, "Latest available observations were not chosen independently.");

        var expired = AccountQuota.LatestFrom(
            [Observe(now.AddMinutes(-10), Quota(fiveHour with { ResetsAt = now }, null)), Observe(now.AddHours(-1), swapped)],
            now);
        Check(expired?.FiveHour is null && expired?.Weekly == weekly, "An expired window was shown or replaced by an older observation.");

        var withoutReset = fiveHour with { ResetsAt = null };
        var missingReset = Observe(now.AddHours(-5), Quota(withoutReset, null));
        var beforeExpiry = AccountQuota.LatestFrom([missingReset], now.AddSeconds(-1));
        Check(beforeExpiry?.FiveHour == withoutReset && beforeExpiry.Weekly is null, "A recent window without a reset was discarded.");
        Check(AccountQuota.LatestFrom([missingReset], now)?.FiveHour is null, "A window without a reset outlived its reported duration.");
        Check(AccountQuota.LatestFrom([Observe(now, null)], now) is null, "Missing quota observations produced a quota.");

        return Task.CompletedTask;
    }

    private static AccountQuota Quota(RateLimitWindow? primary, RateLimitWindow? secondary) =>
        new(primary, secondary, null, false, null);

    private static UsageSnapshot Observe(DateTimeOffset timestamp, AccountQuota? quota) =>
        new(timestamp, TokenBreakdown.Empty, TokenBreakdown.Empty, null, quota);

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
