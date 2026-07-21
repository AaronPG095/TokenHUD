using System.Text.Json;

namespace TokenHud.Core;

public sealed class HudSettings
{
    public long ContextBudget { get; set; } = 150_000;
    public string? PinnedThreadId { get; set; }
    public bool PrivacyMode { get; set; }
    public bool AlwaysOnTop { get; set; } = true;
    public bool Expanded { get; set; }
    public bool NotificationsEnabled { get; set; } = true;
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
    }

    public string Root { get; }
    public string Settings { get; }
    public string Diagnostics { get; }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly AppDataPaths _paths;

    public SettingsStore(AppDataPaths paths)
    {
        _paths = paths;
    }

    public async Task<HudSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.Settings))
        {
            return new HudSettings();
        }

        try
        {
            await using var stream = new FileStream(_paths.Settings, FileMode.Open, FileAccess.Read, FileShare.Read);
            return await JsonSerializer.DeserializeAsync<HudSettings>(stream, Options, cancellationToken).ConfigureAwait(false)
                ?? new HudSettings();
        }
        catch (JsonException)
        {
            return new HudSettings();
        }
    }

    public async Task SaveAsync(HudSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.Root);
        var temporary = _paths.Settings + ".tmp";
        await using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, _paths.Settings, overwrite: true);
    }
}
