using TokenHud.Core;

namespace TokenHud.App;

internal static class DemoData
{
    public static HudSnapshot Create()
    {
        var now = DateTimeOffset.Now;
        var quota = new AccountQuota(
            new RateLimitWindow(38, 300, now.AddHours(3)),
            new RateLimitWindow(21, 10_080, now.AddDays(4)),
            "pro", false, null);

        var recent = new[]
        {
            CreateChat("019f-demo-1", "Refine release diagnostics", 88_000, now, quota),
            CreateChat("019f-demo-2", "Add global chat token counter", 108_400, now.AddMinutes(-7), quota),
            CreateChat("019f-demo-3", "Review authentication migration and remaining edge cases", 132_700, now.AddMinutes(-16), quota),
            CreateChat("019f-demo-4", "Improve Fluent Studio project navigation", 54_200, now.AddMinutes(-31), quota),
            CreateChat("019f-demo-5", "Debug provider cache attribution", 151_300, now.AddMinutes(-48), quota),
            CreateChat("019f-demo-6", "Prepare release notes", 97_600, now.AddHours(-1), quota),
        };

        return new HudSnapshot(
            recent[0],
            recent,
            recent.Select(chat => chat.Root).ToArray(),
            quota,
            now,
            true,
            recent.Length,
            "Demo data · local sources idle");
    }

    private static ChatUsage CreateChat(
        string id,
        string title,
        long contextLoad,
        DateTimeOffset updatedAt,
        AccountQuota quota)
    {
        var root = new ThreadRecord(
            id,
            string.Empty,
            updatedAt.AddHours(-2),
            updatedAt,
            @"C:\work\atlas",
            title,
            contextLoad * 4,
            false,
            "gpt-5.6",
            "high",
            null,
            null,
            null,
            null);

        var reasoning = Math.Max(600, contextLoad / 45);
        var output = Math.Max(reasoning + 300, contextLoad / 18);
        var input = Math.Max(0, contextLoad - output + reasoning);
        var cached = (long)(input * 0.78);
        var cacheWrite = (long)(input * 0.04);
        var total = input + output;
        var flow = new TokenBreakdown(input, cached, cacheWrite, output, reasoning, total);
        var snapshot = new UsageSnapshot(updatedAt, flow, flow, 258_400, quota);
        var policy = new BudgetPolicy();
        var zone = policy.GetZone(contextLoad);
        var analysis = new ContextAnalysis(
            contextLoad,
            contextLoad * 100d / policy.Budget,
            zone,
            2_400,
            900,
            null,
            null,
            false,
            1_200,
            null);
        var breakdown = new ThreadBreakdown(root, snapshot, analysis, 0);
        return new ChatUsage(root, snapshot, analysis, flow, [breakdown], null, updatedAt);
    }
}
