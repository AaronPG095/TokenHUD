using System.Text.Json;

namespace TokenHud.Core;

public enum DiagnosticLevel
{
    Info,
    Warning,
    Error,
}

public sealed class RollingDiagnostics
{
    private const long MaximumBytes = 10 * 1024 * 1024;
    private readonly AppDataPaths _paths;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public RollingDiagnostics(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task WriteAsync(
        string code,
        DiagnosticLevel level = DiagnosticLevel.Info,
        IReadOnlyDictionary<string, long>? counters = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(_paths.Diagnostics);
            Purge();
            var payload = new
            {
                timestamp = DateTimeOffset.UtcNow,
                level = level.ToString().ToLowerInvariant(),
                code,
                counters,
            };
            var line = JsonSerializer.Serialize(payload) + Environment.NewLine;
            var path = Path.Combine(_paths.Diagnostics, $"token-hud-{DateTime.UtcNow:yyyy-MM-dd}.jsonl");
            await File.AppendAllTextAsync(path, line, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public string BuildSummary(HudSnapshot? snapshot)
    {
        var version = typeof(RollingDiagnostics).Assembly.GetName().Version?.ToString() ?? "unknown";
        if (snapshot is null)
        {
            return $"Token HUD {version}{Environment.NewLine}No monitoring snapshot is available.";
        }

        return string.Join(Environment.NewLine,
            $"Token HUD {version}",
            $"Refreshed: {snapshot.RefreshedAt:O}",
            $"State database available: {snapshot.StateDatabaseAvailable}",
            $"Parsed rollouts: {snapshot.ParsedRolloutCount}",
            $"Tracked context: {snapshot.Tracked?.Analysis.Load ?? 0}",
            $"Tracked zone: {snapshot.Tracked?.Analysis.Zone.ToString() ?? "none"}");
    }

    private void Purge()
    {
        var directory = new DirectoryInfo(_paths.Diagnostics);
        if (!directory.Exists)
        {
            return;
        }

        var files = directory.EnumerateFiles("token-hud-*.jsonl")
            .OrderByDescending(item => item.LastWriteTimeUtc)
            .ToList();
        var cutoff = DateTime.UtcNow.AddDays(-7);
        foreach (var old in files.Where(item => item.LastWriteTimeUtc < cutoff))
        {
            old.Delete();
        }

        files = directory.EnumerateFiles("token-hud-*.jsonl")
            .OrderByDescending(item => item.LastWriteTimeUtc)
            .ToList();
        long total = 0;
        foreach (var file in files)
        {
            total += file.Length;
            if (total > MaximumBytes)
            {
                file.Delete();
            }
        }
    }
}
