using TokenHud.Core;

namespace TokenHud.App;

internal static class DemoData
{
    public static HudSnapshot Create()
    {
        var now = DateTimeOffset.Now;
        var root = new ThreadRecord(
            "019f-demo-root", string.Empty, now.AddHours(-2), now, @"C:\work\atlas", "Refine release diagnostics",
            384_200, false, "gpt-5.6", "high", null, null, null, null);
        var child = new ThreadRecord(
            "019f-demo-child", string.Empty, now.AddMinutes(-30), now, @"C:\work\atlas", "Parser audit",
            146_800, false, "gpt-5.6", "high", "Scout", "worker", root.Id, "running");
        var quota = new AccountQuota(
            new RateLimitWindow(38, 300, now.AddHours(3)),
            new RateLimitWindow(21, 10_080, now.AddDays(4)),
            "pro", false, null);
        var rootSnapshot = new UsageSnapshot(now, new TokenBreakdown(280_000, 180_000, 0, 72_000, 32_000, 352_000), new TokenBreakdown(82_400, 54_000, 0, 5_600, 1_800, 88_000), 258_400, quota);
        var childSnapshot = new UsageSnapshot(now, new TokenBreakdown(116_000, 68_000, 0, 22_000, 8_000, 138_000), new TokenBreakdown(91_000, 61_000, 0, 4_200, 1_200, 95_200), 258_400, quota);
        var rootAnalysis = new ContextAnalysis(88_000, 88, BudgetZone.Caution, 3_600, 1_840, 1, "critical", false, 3_200, null);
        var childAnalysis = new ContextAnalysis(95_200, 95.2, BudgetZone.Critical, 4_400, 2_160, 2, "budget", true, 12_400, now);
        var rootBreakdown = new ThreadBreakdown(root, rootSnapshot, rootAnalysis, 0);
        var childBreakdown = new ThreadBreakdown(child, childSnapshot, childAnalysis, 1);
        var chat = new ChatUsage(root, rootSnapshot, rootAnalysis, rootSnapshot.SessionTotal + childSnapshot.SessionTotal, [rootBreakdown, childBreakdown], childBreakdown, now);
        return new HudSnapshot(chat, [chat], [root], quota, now, true, 2, "Demo data · local sources idle");
    }
}
