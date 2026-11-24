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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Diagnostics must not prevent monitoring or settings recovery.
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
            $"Windows build: {Environment.OSVersion.Version.Build}",
            $"Last successful refresh (UTC): {snapshot.RefreshedAt.UtcDateTime:O}",
            $"Codex state database available: {snapshot.StateDatabaseAvailable}",
            $"Recent chat count: {snapshot.RecentChats.Count}",
            $"Readable rollout count: {snapshot.ParsedRolloutCount}");
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
