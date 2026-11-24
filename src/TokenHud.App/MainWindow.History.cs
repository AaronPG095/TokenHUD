using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Data.Sqlite;
using TokenHud.Core;

namespace TokenHud.App;

public partial class MainWindow
{
    private readonly MetricsHistoryStore? _history;
    private readonly SemaphoreSlim _historyWrites = new(1, 1);
    private long _historyRevision;
    private string? _historyChatId;

    private void ApplyHistorySettings()
    {
        HistoryRetentionComboBox.SelectedValue = _settings.HistoryRetention.ToString();
        ClearHistoryButton.IsEnabled = _history is not null;
        if (_demo)
        {
            HistorySettingsStatusLabel.Text = "Demo uses synthetic readings.";
        }
    }

    private async void HistoryRetentionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || HistoryRetentionComboBox?.SelectedValue is not string value ||
            !Enum.TryParse(value, out MetricsHistoryRetention retention))
        {
            return;
        }

        _settings.HistoryRetention = retention;
        ScheduleSave();
        RefreshHistory(_snapshot?.Tracked);
        if (_history is null)
        {
            return;
        }

        await _historyWrites.WaitAsync();
        try
        {
            if (retention == _settings.HistoryRetention)
            {
                await Task.Run(() => _history.ApplyRetentionAsync(retention));
                HistorySettingsStatusLabel.Text = retention == MetricsHistoryRetention.Off
                    ? "Recording paused. Existing history remains on this device."
                    : "Retention updated.";
            }
        }
        catch (Exception exception) when (IsHistoryFailure(exception))
        {
            HistorySettingsStatusLabel.Text = "Could not apply the retention setting.";
        }
        finally
        {
            _historyWrites.Release();
        }
    }

    private async void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_history is null || System.Windows.MessageBox.Show(this,
                "Permanently clear all Token HUD metrics history stored on this device?",
                "Clear Metrics History", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No)
            != MessageBoxResult.Yes)
        {
            return;
        }

        ClearHistoryButton.IsEnabled = false;
        await _historyWrites.WaitAsync();
        try
        {
            await Task.Run(() => _history.EraseAsync());
            HistorySettingsStatusLabel.Text = "Metrics history cleared.";
            RefreshHistory(_snapshot?.Tracked);
        }
        catch (Exception exception) when (IsHistoryFailure(exception))
        {
            HistorySettingsStatusLabel.Text = "Could not clear metrics history.";
        }
        finally
        {
            ClearHistoryButton.IsEnabled = true;
            _historyWrites.Release();
        }
    }

    private async Task RecordHistoryAsync(HudSnapshot snapshot)
    {
        var retention = _settings.HistoryRetention;
        if (_history is null || retention == MetricsHistoryRetention.Off)
        {
            return;
        }

        var chats = snapshot.Tracked is { } tracked ? snapshot.RecentChats.Append(tracked) : snapshot.RecentChats;
        var samples = chats.DistinctBy(chat => chat.Root.Id)
            .Select(MetricsHistorySample.FromChat).OfType<MetricsHistorySample>().ToArray();
        await _historyWrites.WaitAsync();
        try
        {
            if (retention == _settings.HistoryRetention && !_quitting)
            {
                await Task.Run(() => _history.RecordAsync(samples, retention));
                if (_snapshot?.RefreshedAt == snapshot.RefreshedAt)
                {
                    RefreshHistory(_snapshot?.Tracked);
                }
            }
        }
        catch (Exception exception) when (IsHistoryFailure(exception))
        {
            ShowHistoryStatus("Metrics history could not be updated.");
        }
        finally
        {
            _historyWrites.Release();
        }
    }

    private void RefreshHistory(ChatUsage? tracked)
    {
        var revision = ++_historyRevision;
        if (!_settings.Expanded)
        {
            return;
        }

        var previousId = _historyChatId;
        _historyChatId = tracked?.Root.Id;
        var retention = _settings.HistoryRetention;
        if (retention == MetricsHistoryRetention.Off)
        {
            ShowHistoryStatus("Metrics History is off.");
        }
        else if (tracked is null)
        {
            ShowHistoryStatus("Select a chat to see its history.");
        }
        else if (_demo && _history is null)
        {
            var now = DateTimeOffset.Now;
            var budget = tracked.Budget.EffectiveTokens;
            var policy = new BudgetPolicy(budget);
            ShowHistoryPoints(Enumerable.Range(0, 8).Select(index =>
            {
                var load = Math.Max(0, tracked.Analysis.Load - (7 - index) * 3_300);
                return new MetricsHistoryPoint(now.AddMinutes(index * 5 - 35), load, budget, policy.GetZone(load));
            }).ToArray());
        }
        else if (_history is not null)
        {
            if (previousId != _historyChatId)
            {
                ShowHistoryStatus("Loading recent readings…");
            }

            _ = ReadHistoryAsync(tracked.Root.Id, retention, revision);
        }
    }

    private async Task ReadHistoryAsync(string chatId, MetricsHistoryRetention retention, long revision)
    {
        try
        {
            var points = await Task.Run(() => _history!.ReadRecentAsync(chatId, retention, maximumPoints: 20));
            if (revision == _historyRevision && !_quitting)
            {
                ShowHistoryPoints(points);
            }
        }
        catch (Exception exception) when (IsHistoryFailure(exception))
        {
            if (revision == _historyRevision && !_quitting)
            {
                ShowHistoryStatus("Metrics history is unavailable.");
            }
        }
    }

    private void ShowHistoryStatus(string text)
    {
        HistoryChart.Visibility = Visibility.Collapsed;
        HistoryChart.ItemsSource = null;
        HistoryStatusLabel.Text = text;
    }

    private void ShowHistoryPoints(IReadOnlyList<MetricsHistoryPoint> points)
    {
        if (points.Count == 0)
        {
            ShowHistoryStatus("No stored readings for this chat.");
            return;
        }

        var maximum = Math.Max(1, points.Max(point => Math.Max(point.ContextLoad, point.EffectiveBudget)));
        var latest = points[^1];
        var summary = $"{points.Count} readings · Latest {latest.ContextLoad:N0} of {latest.EffectiveBudget:N0} tokens · {ZoneName(latest.Zone)}";
        HistoryStatusLabel.Text = summary;
        AutomationProperties.SetName(HistoryChart, summary);
        HistoryChart.ItemsSource = points.Select(point => new HistoryBar(
            Math.Max(1, 36d * point.ContextLoad / maximum),
            $"{point.ObservedAt.LocalDateTime:g} · {point.ContextLoad:N0} of {point.EffectiveBudget:N0} tokens · {ZoneName(point.Zone)}")).ToArray();
        HistoryChart.Visibility = Visibility.Visible;
    }

    private static bool IsHistoryFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException or SqliteException;

    private sealed record HistoryBar(double Height, string Description);
}
