using System.Collections.Concurrent;

namespace TokenHud.Core;

public sealed class UsageCache
{
    private sealed record Entry(long Length, DateTime LastWriteUtc, IReadOnlyList<UsageSnapshot> History);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly RolloutReader _reader;

    public UsageCache(RolloutReader reader)
    {
        _reader = reader;
    }

    public async Task<IReadOnlyList<UsageSnapshot>> GetAsync(string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return [];
        }

        var info = new FileInfo(path);
        if (_entries.TryGetValue(path, out var cached) &&
            cached.Length == info.Length &&
            cached.LastWriteUtc == info.LastWriteTimeUtc)
        {
            return cached.History;
        }

        var history = await _reader.ReadRecentUsageAsync(path, cancellationToken: cancellationToken).ConfigureAwait(false);
        _entries[path] = new Entry(info.Length, info.LastWriteTimeUtc, history);
        return history;
    }

    public void Invalidate(string path) => _entries.TryRemove(path, out _);
}
