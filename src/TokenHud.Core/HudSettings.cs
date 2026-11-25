using System.Text.Json;

namespace TokenHud.Core;

public sealed class HudSettings
{
    public long ContextBudget { get; set; } = 150_000;
    public long? CodexContextBudget { get; set; }
    public Dictionary<string, long> ChatContextBudgets { get; set; } = new(StringComparer.Ordinal);
    public MetricsHistoryRetention HistoryRetention { get; set; } = MetricsHistoryRetention.Days30;
    public string? PinnedThreadId { get; set; }
    public bool PrivacyMode { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool Expanded { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
    public BudgetAlertPreferences AlertPreferences { get; set; } = new();
    public DateTimeOffset? AlertsSnoozedUntil { get; set; }
    public Dictionary<string, BudgetAlertState> BudgetAlertStates { get; set; } = new(StringComparer.Ordinal);
    public bool MonitoringPaused { get; set; }
    public bool ShowArchived { get; set; }
    public bool StartWithWindows { get; set; } = true;
    public Dictionary<string, int> AlertStates { get; set; } = new(StringComparer.Ordinal);
    public double? Left { get; set; }
    public double? Top { get; set; }
    public double CompactWidth { get; set; } = 392;
    public double CompactHeight { get; set; } = 390;
    public double ExpandedWidth { get; set; } = 860;
    public double ExpandedHeight { get; set; } = 720;
}

public sealed class AppDataPaths
{
    public AppDataPaths(string? root = null)
    {
        Root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TokenHUD");
        Settings = Path.Combine(Root, "settings.json");
        Diagnostics = Path.Combine(Root, "diagnostics");
        MetricsHistory = Path.Combine(Root, "metrics-history.sqlite");
        ProviderTelemetry = Path.Combine(Root, "provider-telemetry");
    }

    public string Root { get; }
    public string Settings { get; }
    public string Diagnostics { get; }
    public string MetricsHistory { get; }
    public string ProviderTelemetry { get; }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly AppDataPaths _paths;
    private bool _existingSettingsUnreadable;
    private readonly SemaphoreSlim _saveGate = new(1, 1);

    public SettingsStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task<HudSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var stream = new FileStream(_paths.Settings, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var settings = await JsonSerializer.DeserializeAsync<HudSettings>(stream, Options, cancellationToken).ConfigureAwait(false)
                ?? new HudSettings();
            settings.AlertStates ??= new Dictionary<string, int>(StringComparer.Ordinal);
            settings.ChatContextBudgets ??= new Dictionary<string, long>(StringComparer.Ordinal);
            settings.AlertPreferences ??= new BudgetAlertPreferences();
            settings.BudgetAlertStates ??= new Dictionary<string, BudgetAlertState>(StringComparer.Ordinal);
            _existingSettingsUnreadable = false;
            return settings;
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException or JsonException)
        {
            _existingSettingsUnreadable = false;
            return new HudSettings();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _existingSettingsUnreadable = true;
            return new HudSettings();
        }
    }

    public async Task SaveAsync(HudSettings settings, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var data = JsonSerializer.SerializeToUtf8Bytes(settings, Options);
        await _saveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_existingSettingsUnreadable)
            {
                throw new IOException("Existing settings could not be read and must be preserved.");
            }

            Directory.CreateDirectory(_paths.Root);
            var temporary = $"{_paths.Settings}.{Guid.NewGuid():N}.tmp";
            try
            {
                await File.WriteAllBytesAsync(temporary, data, cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, _paths.Settings, overwrite: true);
            }
            finally
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Preserve the original save error if temporary file cleanup also fails.
                }
            }
        }
        finally
        {
            _saveGate.Release();
        }
    }
}
