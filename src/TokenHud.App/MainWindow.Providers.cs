using System.Globalization;
using System.Windows;
using TokenHud.Core;

namespace TokenHud.App;

public partial class MainWindow
{
    internal ProviderTelemetryStore? ProviderFeed { get; init; }
    private bool _loadingProviderSources;
    private DateTimeOffset _providerSourcesNextRefresh;

    private static readonly (string Id, string Name, string Coverage)[] ProviderDescriptions =
    [
        ("claude-code", "Claude Code", "Context and account quota when reported by the local status line."),
        ("antigravity", "Google Antigravity CLI", "Provider-reported context and quota when available."),
        ("cursor", "Cursor", "Pre-compaction context snapshots and imported usage."),
        ("openai-api", "OpenAI API", "Reported usage from connected applications. Regular ChatGPT chats have no live context feed."),
        ("gemini-api", "Gemini API", "Reported usage from connected applications. Gemini Apps chats have no live context feed."),
    ];

    private void ProviderSources_Expanded(object sender, RoutedEventArgs e)
    {
        _providerSourcesNextRefresh = DateTimeOffset.MinValue;
        RefreshProviderSources();
    }

    private void RefreshProviderSources()
    {
        var now = DateTimeOffset.UtcNow;
        if (!_settings.Expanded || !ProviderSources.IsVisible || _loadingProviderSources || now < _providerSourcesNextRefresh)
        {
            return;
        }

        _loadingProviderSources = true;
        _providerSourcesNextRefresh = now.AddSeconds(15);
        _ = LoadProviderSourcesAsync(now);
    }

    private async Task LoadProviderSourcesAsync(DateTimeOffset now)
    {
        try
        {
            IReadOnlyList<ProviderReading> readings = ProviderFeed is { } store
                ? await Task.Run(() => store.LoadAsync(now))
                : [];
            if (!_quitting)
            {
                ProviderSources.ItemsSource = BuildProviderRows(readings, now);
            }
        }
        finally
        {
            _loadingProviderSources = false;
        }
    }

    private static ProviderRow[] BuildProviderRows(IReadOnlyList<ProviderReading> readings, DateTimeOffset now) =>
        ProviderDescriptions.Select(provider =>
        {
            var sessions = readings.Where(reading => reading.SourceId == provider.Id)
                .OrderByDescending(reading => reading.ObservedAt).Take(3).ToArray();
            var detail = sessions.Length == 0
                ? $"No local feed. {provider.Coverage}"
                : string.Join(Environment.NewLine, sessions.Select((reading, index) => FormatProviderReading(reading, now, index + 1)));
            return new ProviderRow(provider.Name, detail);
        }).ToArray();

    private static string FormatProviderReading(ProviderReading reading, DateTimeOffset now, int sessionNumber)
    {
        var freshness = reading.GetFreshness(now).ToString();
        var values = new List<string> { $"Session {sessionNumber} · {freshness} · {reading.ObservedAt.LocalDateTime:g}" };
        if (reading.CurrentContext is { } current)
        {
            values.Add($"Context {FormatProviderContext(current)}");
        }
        if (reading.HistoricalContext is { } historical)
        {
            values.Add($"Historical context {FormatProviderContext(historical)}");
        }
        if (reading.LatestExchange is { } exchange)
        {
            var detail = $"Latest exchange {Metric(exchange.InputTokens)} input · {Metric(exchange.OutputTokens)} output";
            if (exchange.CachedInputTokens is { } cached)
            {
                detail += $" · {Metric(cached)} cached input";
            }
            if (exchange.ReasoningOutputTokens is { } reasoning)
            {
                detail += $" · {Metric(reasoning)} reasoning output";
            }
            values.Add(detail);
        }
        foreach (var quota in reading.AccountQuotas.Where(quota => quota.ResetsAt is null || quota.ResetsAt > now))
        {
            var label = quota.Kind switch
            {
                "five-hour" => "5h quota",
                "daily" => "Daily quota",
                "weekly" => "Weekly quota",
                "monthly" => "Monthly quota",
                _ => "Account quota",
            };
            var remaining = quota.RemainingPercent is { } percent
                ? $"{percent:0.#}% left"
                : $"{quota.RemainingAmount:0.##} {quota.Unit} left";
            values.Add($"{label} {remaining}");
        }

        return string.Join(Environment.NewLine, values);
    }

    private static string FormatProviderContext(ProviderContext context)
    {
        var ratio = context.UsedPercent is { } percent ? $" ({percent:0.#}%)" : string.Empty;
        if (context.Tokens is null)
        {
            return context.UsedPercent is { } reported ? $"{reported:0.#}% used"
                : context.CapacityTokens is { } capacityOnly ? $"usage unavailable · capacity {Metric(capacityOnly)} tokens"
                : "unavailable";
        }

        return context.CapacityTokens is { } capacity
            ? $"{Metric(context.Tokens)} / {Metric(capacity)} tokens{ratio}"
            : $"{Metric(context.Tokens)} tokens{ratio}";
    }

    private static string Metric(long? value) => value?.ToString("N0", CultureInfo.CurrentCulture) ?? "—";

    private sealed record ProviderRow(string Title, string Detail);
}
