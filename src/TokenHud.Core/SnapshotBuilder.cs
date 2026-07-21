namespace TokenHud.Core;

public sealed class SnapshotBuilder
{
    private readonly CodexCatalog _catalog;
    private readonly UsageCache _usage;

    public SnapshotBuilder(CodexCatalog catalog, UsageCache usage)
    {
        _catalog = catalog;
        _usage = usage;
    }

    public async Task<HudSnapshot> BuildAsync(
        string? pinnedThreadId,
        BudgetPolicy policy,
        bool includeArchived,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var catalog = await _catalog.LoadAsync(cancellationToken).ConfigureAwait(false);
        var byId = catalog.Threads.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var children = catalog.Threads
            .Where(item => item.ParentThreadId is not null)
            .GroupBy(item => item.ParentThreadId!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.CreatedAt).ToArray(), StringComparer.Ordinal);

        var roots = catalog.Threads
            .Where(item => item.IsTopLevel)
            .OrderByDescending(item => item.UpdatedAt)
            .ToArray();
        var recentRoots = roots.Where(item => !item.Archived).Take(6).ToList();

        ThreadRecord? pinned = null;
        if (!string.IsNullOrWhiteSpace(pinnedThreadId) && byId.TryGetValue(pinnedThreadId, out var pinnedRecord))
        {
            pinned = FindRoot(pinnedRecord, byId);
            if (recentRoots.All(item => item.Id != pinned.Id))
            {
                recentRoots.Insert(0, pinned);
            }
        }

        var trackedRoot = pinned ?? recentRoots.FirstOrDefault();
        var usageTasks = recentRoots
            .DistinctBy(item => item.Id)
            .Select(root => BuildChatUsageAsync(root, children, policy, cancellationToken))
            .ToArray();
        var recent = await Task.WhenAll(usageTasks).ConfigureAwait(false);

        ChatUsage? tracked = null;
        if (trackedRoot is not null)
        {
            tracked = recent.FirstOrDefault(item => item.Root.Id == trackedRoot.Id)
                ?? await BuildChatUsageAsync(trackedRoot, children, policy, cancellationToken).ConfigureAwait(false);
        }

        var normalizedSearch = search?.Trim();
        var searchable = roots
            .Where(item => includeArchived || !item.Archived)
            .Where(item => string.IsNullOrWhiteSpace(normalizedSearch) ||
                item.Title.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                item.Id.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ||
                item.Cwd.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .Take(200)
            .ToArray();

        var latestQuota = recent
            .SelectMany(item => item.Threads)
            .Where(item => item.Latest?.Quota is not null)
            .OrderByDescending(item => item.Latest!.Timestamp)
            .Select(item => item.Latest!.Quota)
            .FirstOrDefault();
        var parsedCount = recent.SelectMany(item => item.Threads).Count(item => item.Latest is not null);

        return new HudSnapshot(
            tracked,
            recent,
            searchable,
            latestQuota,
            DateTimeOffset.Now,
            catalog.DatabaseAvailable,
            parsedCount,
            catalog.Status);
    }

    private async Task<ChatUsage> BuildChatUsageAsync(
        ThreadRecord root,
        IReadOnlyDictionary<string, ThreadRecord[]> children,
        BudgetPolicy policy,
        CancellationToken cancellationToken)
    {
        var flattened = Flatten(root, children);
        var observedAt = DateTimeOffset.UtcNow;
        var histories = await Task.WhenAll(flattened.Select(async item =>
            (item.Thread, item.Depth, History: item.Depth == 0 || item.Thread.IsActivelyRunning(observedAt)
                ? await _usage.GetAsync(item.Thread.RolloutPath, cancellationToken).ConfigureAwait(false)
                : (IReadOnlyList<UsageSnapshot>)[]))).ConfigureAwait(false);

        var breakdown = histories.Select(item =>
        {
            var latest = item.History.LastOrDefault();
            return new ThreadBreakdown(item.Thread, latest, BudgetAnalyzer.Analyze(item.History, policy), item.Depth);
        }).ToArray();

        var rootBreakdown = breakdown[0];
        var treeConsumption = breakdown.Aggregate(TokenBreakdown.Empty, (total, item) =>
            total + (item.Latest?.SessionTotal ?? new TokenBreakdown(0, 0, 0, 0, 0, item.Thread.CatalogTokensUsed)));
        var highestChild = breakdown
            .Skip(1)
            .Where(item => item.Latest is not null && item.Thread.IsActivelyRunning(observedAt))
            .OrderByDescending(item => item.Analysis.Load)
            .FirstOrDefault();
        var lastActivity = breakdown
            .Select(item => Max(item.Thread.UpdatedAt, item.Latest?.Timestamp ?? DateTimeOffset.MinValue))
            .Max();

        return new ChatUsage(
            root,
            rootBreakdown.Latest,
            rootBreakdown.Analysis,
            treeConsumption,
            breakdown,
            highestChild,
            lastActivity);
    }

    private static IReadOnlyList<(ThreadRecord Thread, int Depth)> Flatten(
        ThreadRecord root,
        IReadOnlyDictionary<string, ThreadRecord[]> children)
    {
        var output = new List<(ThreadRecord, int)> { (root, 0) };
        var visited = new HashSet<string>(StringComparer.Ordinal) { root.Id };

        void AddChildren(ThreadRecord parent, int depth)
        {
            if (!children.TryGetValue(parent.Id, out var directChildren))
            {
                return;
            }

            foreach (var child in directChildren)
            {
                if (!visited.Add(child.Id))
                {
                    continue;
                }

                output.Add((child, depth));
                AddChildren(child, depth + 1);
            }
        }

        AddChildren(root, 1);
        return output;
    }

    private static ThreadRecord FindRoot(ThreadRecord record, IReadOnlyDictionary<string, ThreadRecord> byId)
    {
        var current = record;
        var visited = new HashSet<string>(StringComparer.Ordinal) { current.Id };
        while (current.ParentThreadId is { } parentId && byId.TryGetValue(parentId, out var parent) && visited.Add(parent.Id))
        {
            current = parent;
        }

        return current;
    }

    private static DateTimeOffset Max(DateTimeOffset left, DateTimeOffset right) => left > right ? left : right;
}
