using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using TokenHud.Core;
using ItemsControl = System.Windows.Controls.ItemsControl;

namespace TokenHud.App;

public partial class MainWindow : Window
{
    private readonly HudSettings _settings;
    private readonly SettingsStore _settingsStore;
    private readonly RollingDiagnostics _diagnostics;
    private readonly ThemeService _theme;
    private readonly bool _demo;
    private readonly HudMonitor? _monitor;
    private readonly DispatcherTimer _searchTimer;
    private readonly DispatcherTimer _saveTimer;
    private readonly DispatcherTimer _freshnessTimer;
    private readonly Dictionary<string, AlertState> _alertStates = new(StringComparer.Ordinal);
    private readonly ObservableCollection<RecentChatDisplayItem> _recentItems = [];
    private readonly ObservableCollection<ChatListItem> _chatItems = [];
    private readonly ObservableCollection<ThreadDisplayItem> _threadItems = [];
    private readonly System.Windows.Forms.NotifyIcon _trayIcon;
    private readonly System.Windows.Forms.ToolStripMenuItem _privacyMenu;
    private readonly System.Windows.Forms.ToolStripMenuItem _topmostMenu;
    private readonly System.Windows.Forms.ToolStripMenuItem _pauseMenu;
    private readonly System.Windows.Forms.ToolStripMenuItem _startupMenu;
    private readonly WindowsNotificationService? _notifications;
    private HudSnapshot? _snapshot;
    private string? _selectedThreadId;
    private string _searchText = string.Empty;
    private bool _updatingUi;
    private bool _applyingViewMode;
    private bool _showChatBrowser;
    private bool _alertsInitialized;
    private bool _quitting;

    internal MainWindow(
        HudSettings settings,
        SettingsStore settingsStore,
        RollingDiagnostics diagnostics,
        ThemeService theme,
        bool demo)
    {
        _settings = settings;
        _settingsStore = settingsStore;
        _diagnostics = diagnostics;
        _theme = theme;
        _demo = demo;
        InitializeComponent();

        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 0,
            ResizeBorderThickness = new Thickness(6),
            CornerRadius = new CornerRadius(10),
            GlassFrameThickness = new Thickness(0),
            UseAeroCaptionButtons = false,
        });

        RecentChatList.ItemsSource = _recentItems;
        ChatList.ItemsSource = _chatItems;
        ThreadGrid.ItemsSource = _threadItems;
        _searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _searchTimer.Tick += SearchTimer_Tick;
        _saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _saveTimer.Tick += SaveTimer_Tick;
        _freshnessTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _freshnessTimer.Tick += (_, _) => UpdateMonitorStatus();

        _settings.StartWithWindows = !_demo && StartupRegistration.IsEnabled();
        foreach (var (threadId, value) in _settings.AlertStates)
        {
            if (Enum.IsDefined(typeof(AlertState), value))
            {
                _alertStates[threadId] = (AlertState)value;
            }
        }

        NormalizeGeometrySettings();
        ApplySettingsToControls();
        ApplyViewMode();
        RestorePosition();

        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Open Token HUD", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("Toggle compact / expanded", null, (_, _) => Dispatcher.Invoke(ToggleExpanded));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        _privacyMenu = new System.Windows.Forms.ToolStripMenuItem("Privacy Mode", null, (_, _) => Dispatcher.Invoke(TogglePrivacy)) { Checked = _settings.PrivacyMode, CheckOnClick = false };
        _topmostMenu = new System.Windows.Forms.ToolStripMenuItem("Always on top", null, (_, _) => Dispatcher.Invoke(ToggleTopmost)) { Checked = _settings.AlwaysOnTop, CheckOnClick = false };
        _pauseMenu = new System.Windows.Forms.ToolStripMenuItem("Pause monitoring", null, (_, _) => Dispatcher.Invoke(TogglePause)) { Checked = _settings.MonitoringPaused, CheckOnClick = false };
        _startupMenu = new System.Windows.Forms.ToolStripMenuItem("Start with Windows", null, (_, _) => Dispatcher.Invoke(ToggleStartup)) { Checked = _settings.StartWithWindows, CheckOnClick = false };
        menu.Items.Add(_privacyMenu);
        menu.Items.Add(_topmostMenu);
        menu.Items.Add(_pauseMenu);
        _startupMenu.Enabled = !_demo;
        menu.Items.Add(_startupMenu);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Open diagnostics", null, (_, _) => Dispatcher.Invoke(OpenDiagnostics));
        menu.Items.Add("Copy diagnostic summary", null, (_, _) => Dispatcher.Invoke(CopyDiagnosticSummary));
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, async (_, _) => await Dispatcher.InvokeAsync(QuitAsync));

        _trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Information,
            Text = "Token HUD",
            Visible = !_demo,
            ContextMenuStrip = menu,
        };
        _trayIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        _trayIcon.BalloonTipClicked += (_, _) => Dispatcher.Invoke(ShowFromTray);
        if (_demo)
        {
            Title = "Token HUD Demo";
            HideButton.ToolTip = "Close demo";
            AutomationProperties.SetName(HideButton, "Close demo");
        }
        else
        {
            _notifications = new WindowsNotificationService(() => Dispatcher.Invoke(ShowFromTray), diagnostics);
            var paths = new CodexPaths();
            var builder = new SnapshotBuilder(new CodexCatalog(paths), new UsageCache(new RolloutReader()));
            _monitor = new HudMonitor(builder, diagnostics, GetMonitorOptions, paths);
            _monitor.SnapshotChanged += Monitor_SnapshotChanged;
            _monitor.Start();
            _freshnessTimer.Start();
        }

        Loaded += MainWindow_Loaded;
        LocationChanged += Window_GeometryChanged;
        SizeChanged += Window_GeometryChanged;
        DpiChanged += (_, _) => _ = Dispatcher.InvokeAsync(EnsureWithinWorkArea, DispatcherPriority.Loaded);
    }

    public void ShowFromTray()
    {
        if (!IsVisible)
        {
            Show();
        }

        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Activate();
        Topmost = _settings.AlwaysOnTop;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        if (_demo)
        {
            RefreshNavigation();
        }

        UpdateMonitorStatus();
    }

    private MonitorOptions GetMonitorOptions() => new(
        _selectedThreadId ?? _settings.PinnedThreadId,
        _settings.ContextBudget,
        _settings.ShowArchived,
        _searchText,
        _settings.MonitoringPaused);

    private void Monitor_SnapshotChanged(object? sender, (MonitorOptions Options, HudSnapshot Snapshot) refresh) =>
        Dispatcher.BeginInvoke(() =>
        {
            if (refresh.Options != GetMonitorOptions())
            {
                return;
            }

            var snapshot = refresh.Snapshot;
            if (refresh.Options.TrackedThreadId is { } target &&
                snapshot.Tracked?.Threads.Any(thread => thread.Thread.Id == target) != true)
            {
                snapshot = snapshot with { Tracked = null };
            }

            UpdateSnapshot(snapshot);
        });

    private void RefreshNavigation()
    {
        var options = GetMonitorOptions();
        if (_demo)
        {
            UpdateSnapshot(DemoData.Create(options.TrackedThreadId, options.IncludeArchived, options.Search));
            return;
        }

        _monitor?.RequestRefresh();
    }

    private void RefreshInspection()
    {
        if (!_demo && _snapshot is { } snapshot)
        {
            var target = GetMonitorOptions().TrackedThreadId;
            var tracked = target is null ? snapshot.RecentChats.FirstOrDefault()
                : snapshot.Tracked?.Root.Id == target ? snapshot.Tracked
                : snapshot.RecentChats.FirstOrDefault(chat => chat.Root.Id == target);
            UpdateSnapshot(snapshot with { Tracked = tracked });
        }

        RefreshNavigation();
    }

    private void UpdateSnapshot(HudSnapshot snapshot)
    {
        _snapshot = snapshot;
        var tracked = snapshot.Tracked;
        _updatingUi = true;
        try
        {
            UpdateNavigationState(tracked);
            if (tracked is null)
            {
                TrackedTitle.Text = GetMonitorOptions().TrackedThreadId is null
                    ? "No local Codex chats found"
                    : "Selected chat detail unavailable";
                UpdateContextMeter(null);
                OriginStatusLabel.Text = "Context origin unavailable";
                _threadItems.Clear();
                UpdateThreadStatus();
                UpdateBreakdown(null);
            }
            else
            {
                TrackedTitle.Text = DisplayTitle(tracked.Root);
                UpdateContextMeter(tracked.Analysis);
                OriginStatusLabel.Text = tracked.Analysis.Zone == BudgetZone.Unavailable
                    ? "Context measurement unavailable"
                    : "Experimental Codex adapter · Context origin unavailable";

                UpdateBreakdown(tracked);
                UpdateThreadGrid(tracked);
            }

            UpdateQuota(snapshot.Quota);
            UpdateRecentList(snapshot);
            UpdateChatList(snapshot);
            CompactBudgetLabel.Text = $"CONTEXT LOAD / {FormatTokens(_settings.ContextBudget)}";
            UpdateMonitorStatus();
            CheckAlerts(snapshot);
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private void UpdateNavigationState(ChatUsage? tracked)
    {
        var target = _selectedThreadId ?? tracked?.Root.Id;
        var isPinned = IsPinnedChat(target, tracked);
        ModeLabel.Text = isPinned ? "Pinned chat"
            : _selectedThreadId is not null ? "Selected chat"
            : _settings.PinnedThreadId is not null ? "Pinned chat unavailable"
            : "Following latest chat";
        PinButton.IsEnabled = target is not null;
        PinButton.Content = isPinned ? "Unpin chat" : "Pin chat";
        PinButton.Foreground = FindBrush(isPinned ? "PrimaryBrush" : "MutedBrush");
        var pinLabel = isPinned ? "Unpin chat and follow latest" : "Pin current chat";
        PinButton.ToolTip = pinLabel;
        AutomationProperties.SetName(PinButton, pinLabel);
        FollowLatestButton.IsEnabled = GetMonitorOptions().TrackedThreadId is not null;
    }

    private bool IsPinnedChat(string? id, ChatUsage? tracked) =>
        _settings.PinnedThreadId is { } pinned &&
        (id == pinned || (id is not null && id == tracked?.Root.Id &&
            tracked.Threads.Any(thread => thread.Thread.Id == pinned)));

    private void UpdateContextMeter(ContextAnalysis? analysis)
    {
        var zone = analysis?.Zone ?? BudgetZone.Unavailable;
        var zoneBrush = FindBrush(ZoneBrushName(zone));
        ContextValue.Text = FormatContext(analysis);
        ContextValue.ToolTip = zone == BudgetZone.Unavailable ? null : $"{analysis!.Load:N0} context tokens";
        BudgetValue.Text = $" / {FormatTokens(_settings.ContextBudget)}";
        ContextMeter.Visibility = zone == BudgetZone.Unavailable ? Visibility.Hidden : Visibility.Visible;
        ContextMeter.Value = Math.Clamp(analysis?.BudgetPercent ?? 0, 0, 100);
        ContextMeter.Foreground = zoneBrush;
        ZoneLabel.Text = ZoneName(zone);
        ZoneLabel.Foreground = zoneBrush;
        ZoneBadge.BorderBrush = zoneBrush;
        AutomationProperties.SetName(ContextMeter, zone == BudgetZone.Unavailable
            ? "Context measurement unavailable"
            : $"Context budget {analysis!.BudgetPercent:0.#} percent used, {ZoneName(zone)}");
    }

    private void UpdateMonitorStatus()
    {
        var snapshot = _snapshot;
        var paused = !_demo && _settings.MonitoringPaused;
        // Allow three missed polling intervals before marking a running monitor stale.
        var stale = !_demo && snapshot is not null &&
            (paused || DateTimeOffset.UtcNow - snapshot.RefreshedAt >= TimeSpan.FromSeconds(45));
        MonitorStatusLabel.Text = stale ? "Stale" : paused ? "Paused" : string.Empty;
        MonitorStatusLabel.Visibility = stale || paused ? Visibility.Visible : Visibility.Collapsed;

        var status = stale ? (paused ? "Monitoring paused · Data stale" : "Data stale")
            : paused ? "Monitoring paused"
            : snapshot?.SourceStatus ?? "Waiting for a local refresh";
        SourceHealthLabel.Text = $"Experimental Codex adapter · {status}";
        RefreshLabel.Text = snapshot is null ? "—" : $"Updated {snapshot.RefreshedAt:HH:mm:ss}";
        HealthDot.Fill = FindBrush(stale || paused ? "CautionBrush" : snapshot?.StateDatabaseAvailable switch
        {
            true => "HealthyBrush",
            false => "CautionBrush",
            null => "MutedBrush",
        });
        HealthDot.ToolTip = SourceHealthLabel.Text;
        MonitorStatusLabel.ToolTip = SourceHealthLabel.Text;
        AutomationProperties.SetName(HealthDot, SourceHealthLabel.Text);
        _trayIcon.Text = MakeTrayText(snapshot?.Tracked, stale, paused);
    }

    private void UpdateBreakdown(ChatUsage? tracked)
    {
        var flow = tracked?.Latest is { HasContext: true } latest ? latest.Context : TokenBreakdown.Empty;
        var newInput = flow.NewInputTokens;
        var visibleOutput = flow.VisibleOutputTokens;

        InputValue.Text = FormatMeasuredTokens(flow.InputTokens, flow.Reports(TokenFields.Input));
        CachedValue.Text = FormatMeasuredTokens(flow.CachedInputTokens, flow.Reports(TokenFields.CachedInput));
        CacheWriteValue.Text = FormatMeasuredTokens(flow.CacheWriteInputTokens, flow.Reports(TokenFields.CacheWrite));
        NewInputValue.Text = FormatMeasuredTokens(newInput.GetValueOrDefault(), newInput.HasValue);
        OutputValue.Text = FormatMeasuredTokens(flow.OutputTokens, flow.Reports(TokenFields.Output));
        ReasoningValue.Text = FormatMeasuredTokens(flow.ReasoningOutputTokens, flow.Reports(TokenFields.Reasoning));
        VisibleOutputValue.Text = FormatMeasuredTokens(visibleOutput.GetValueOrDefault(), visibleOutput.HasValue);

        TokenFlowStatusLabel.Text = flow.ReportedFields == TokenFields.None ? "Latest exchange unavailable"
            : newInput is null || visibleOutput is null ? "Latest exchange · unreported values shown as —"
            : "Latest exchange";
        TokenFlowStatusLabel.ToolTip = tracked?.Latest is { HasSessionTotal: true } session
            ? $"Session totals: {FormatMeasuredTokens(session.SessionTotal.InputTokens, session.SessionTotal.Reports(TokenFields.Input))} in · {FormatMeasuredTokens(session.SessionTotal.OutputTokens, session.SessionTotal.Reports(TokenFields.Output))} out"
            : null;
        var showInputSplit = newInput.HasValue;
        SetFlowColumns(InputCachedColumn, InputWriteColumn, InputNewColumn,
            showInputSplit ? flow.CachedInputTokens : 0, showInputSplit ? flow.CacheWriteInputTokens : 0, newInput.GetValueOrDefault());
        SetFlowColumns(OutputReasoningColumn, OutputVisibleColumn,
            visibleOutput.HasValue ? flow.ReasoningOutputTokens : 0, visibleOutput.GetValueOrDefault());
    }

    private static void SetFlowColumns(
        System.Windows.Controls.ColumnDefinition first,
        System.Windows.Controls.ColumnDefinition second,
        long firstValue,
        long secondValue)
    {
        first.Width = new GridLength(Math.Max(0, firstValue), GridUnitType.Star);
        second.Width = new GridLength(Math.Max(0, secondValue), GridUnitType.Star);
    }

    private static void SetFlowColumns(
        System.Windows.Controls.ColumnDefinition first,
        System.Windows.Controls.ColumnDefinition second,
        System.Windows.Controls.ColumnDefinition third,
        long firstValue,
        long secondValue,
        long thirdValue)
    {
        first.Width = new GridLength(Math.Max(0, firstValue), GridUnitType.Star);
        second.Width = new GridLength(Math.Max(0, secondValue), GridUnitType.Star);
        third.Width = new GridLength(Math.Max(0, thirdValue), GridUnitType.Star);
    }

    private void UpdateThreadGrid(ChatUsage tracked)
    {
        ReconcileRows(ThreadGrid, _threadItems, tracked.Threads.Select(item =>
        {
            var name = _settings.PrivacyMode
                ? (item.Depth == 0 ? "Top-level chat" : $"Subagent {item.Depth}")
                : DisplayTitle(item.Thread);
            var indent = item.Depth == 0 ? "●" : new string('·', Math.Min(item.Depth, 3));
            var hasTotal = item.Latest?.HasSessionTotal == true;
            var total = item.Latest?.SessionTotal ?? TokenBreakdown.Empty;
            var values = new ThreadRowValues(
                indent,
                name,
                FormatContext(item.Analysis),
                ZoneName(item.Analysis.Zone),
                FormatMeasuredTokens(total.InputTokens, hasTotal && total.Reports(TokenFields.Input)),
                FormatMeasuredTokens(total.CachedInputTokens, hasTotal && total.Reports(TokenFields.CachedInput)),
                FormatMeasuredTokens(total.OutputTokens, hasTotal && total.Reports(TokenFields.Output)),
                FormatMeasuredTokens(total.ReasoningOutputTokens, hasTotal && total.Reports(TokenFields.Reasoning)),
                FormatMeasuredTokens(total.TotalTokens, hasTotal));
            return (item.Thread.Id, values);
        }), static (id, values) => new ThreadDisplayItem(id, values));
        UpdateThreadStatus();
    }

    private void UpdateThreadStatus() =>
        ThreadStatusLabel.Visibility = _threadItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateRecentList(HudSnapshot snapshot)
    {
        var itemsById = _recentItems.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var index = 0;
        foreach (var chat in snapshot.RecentChats.Take(6))
        {
            var title = DisplayTitle(chat.Root);
            var context = FormatContext(chat.Analysis);
            var zone = ZoneName(chat.Analysis.Zone);
            var item = itemsById.GetValueOrDefault(chat.Root.Id) ?? new RecentChatDisplayItem(chat.Root.Id);
            item.Update(title, context, zone, FindBrush(ZoneBrushName(chat.Analysis.Zone)),
                chat.Analysis.Zone == BudgetZone.Unavailable
                    ? $"{title}, context load unavailable"
                    : $"{title}, {context} context load, {zone}");

            var previousIndex = _recentItems.IndexOf(item);
            if (previousIndex < 0)
            {
                _recentItems.Insert(index, item);
            }
            else if (previousIndex != index)
            {
                _recentItems.Move(previousIndex, index);
            }

            index++;
        }

        while (_recentItems.Count > index)
        {
            _recentItems.RemoveAt(_recentItems.Count - 1);
        }

        EmptyChatsLabel.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyChatsLabel.Text = "No local Codex chats found";
    }

    private void UpdateChatList(HudSnapshot snapshot)
    {
        var usageById = snapshot.RecentChats.ToDictionary(chat => chat.Root.Id, StringComparer.Ordinal);
        if (snapshot.Tracked is { } tracked)
        {
            usageById[tracked.Root.Id] = tracked;
        }

        ReconcileRows(ChatList, _chatItems, snapshot.SearchableChats
            .Where(chat => _settings.ShowArchived || !chat.Archived)
            .Select(chat =>
            {
                usageById.TryGetValue(chat.Id, out var usage);
                var zone = usage?.Analysis.Zone ?? BudgetZone.Unavailable;
                var values = new ChatRowValues(
                    DisplayTitle(chat),
                    chat.Archived ? "Archived" : $"Updated {chat.UpdatedAt.LocalDateTime:g}",
                    FormatContext(usage?.Analysis),
                    usage is null ? "Not sampled" : ZoneName(zone),
                    FindBrush(ZoneBrushName(zone)),
                    chat.Archived,
                    IsPinnedChat(chat.Id, snapshot.Tracked));
                return (chat.Id, values);
            }), static (id, values) => new ChatListItem(id, values));

        var selectedId = _selectedThreadId ?? snapshot.Tracked?.Root.Id;
        ChatList.SelectedItem = _chatItems.FirstOrDefault(item => item.Id == selectedId);
        BrowserEmptyLabel.Visibility = _chatItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        BrowserEmptyLabel.Text = string.IsNullOrWhiteSpace(SearchBox.Text) ? "No chats available" : "No matching chats";
    }

    private static void ReconcileRows<TItem, TValues>(
        ItemsControl control,
        ObservableCollection<TItem> items,
        IEnumerable<(string Id, TValues Values)> rows,
        Func<string, TValues, TItem> create)
        where TItem : DisplayRow<TValues>
    {
        var desiredRows = rows.ToArray();
        var desiredIds = desiredRows.Select(row => row.Id).ToHashSet(StringComparer.Ordinal);
        var focusedElement = Keyboard.FocusedElement as DependencyObject;
        var focusedContainer = focusedElement is null ? null : ItemsControl.ContainerFromElement(control, focusedElement);
        var focusedRow = focusedContainer is null ? null : control.ItemContainerGenerator.ItemFromContainer(focusedContainer) as TItem;
        var needsFocusRestore = false;

        for (var index = items.Count - 1; index >= 0; index--)
        {
            if (!desiredIds.Contains(items[index].Id))
            {
                items.RemoveAt(index);
            }
        }

        var existingRows = items.ToDictionary(item => item.Id, StringComparer.Ordinal);
        for (var index = 0; index < desiredRows.Length; index++)
        {
            var (id, values) = desiredRows[index];
            if (!existingRows.TryGetValue(id, out var item))
            {
                items.Insert(index, create(id, values));
                continue;
            }

            item.Update(values);
            var previousIndex = items.IndexOf(item);
            if (previousIndex != index)
            {
                items.Move(previousIndex, index);
                needsFocusRestore |= ReferenceEquals(item, focusedRow);
            }
        }

        if (needsFocusRestore && focusedRow is not null)
        {
            control.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
            {
                if (Keyboard.FocusedElement is null || Keyboard.FocusedElement == control)
                {
                    (control.ItemContainerGenerator.ContainerFromItem(focusedRow) as UIElement)?.Focus();
                }
            });
        }
    }

    private void UpdateQuota(AccountQuota? quota)
    {
        var fiveHour = quota?.FiveHour;
        var weekly = quota?.Weekly;
        var fiveHourText = fiveHour is null ? "—" : $"{fiveHour.RemainingPercent:0}% left";
        var weeklyText = weekly is null ? "—" : $"{weekly.RemainingPercent:0}% left";

        QuotaSummaryLabel.Text = $"Account quota   5h {fiveHourText}   ·   Weekly {weeklyText}";
        QuotaSummaryLabel.ToolTip = string.Join(Environment.NewLine, new[]
        {
            FormatQuotaReset("Five-hour", fiveHour),
            FormatQuotaReset("Weekly", weekly),
        });
        AutomationProperties.SetName(QuotaSummaryLabel, $"Account quota. Five-hour {fiveHourText}. Weekly {weeklyText}.");
        ExpandedQuotaLabel.Text = $"Five-hour {fiveHourText}   ·   Weekly {weeklyText}";
        ExpandedQuotaLabel.ToolTip = QuotaSummaryLabel.ToolTip;
        AutomationProperties.SetName(ExpandedQuotaLabel, AutomationProperties.GetName(QuotaSummaryLabel));
    }

    private static string FormatQuotaReset(string name, RateLimitWindow? window)
    {
        if (window is null)
        {
            return $"{name}: not reported";
        }

        return window.ResetsAt is null
            ? $"{name}: reset time not reported"
            : $"{name}: resets {window.ResetsAt.Value.LocalDateTime:g}";
    }

    private void CheckAlerts(HudSnapshot snapshot)
    {
        if (_demo || !_settings.NotificationsEnabled)
        {
            return;
        }

        var candidates = snapshot.RecentChats
                     .Where(chat => !chat.Root.Archived)
                     .SelectMany(chat => chat.Threads)
                     .Where(item => item.Depth == 0 || item.Thread.IsActivelyRunning(snapshot.RefreshedAt))
                     .DistinctBy(item => item.Thread.Id)
                     .ToArray();
        var stateChanged = false;
        var candidateIds = candidates.Select(item => item.Thread.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var threadId in _alertStates.Keys.Where(id => !candidateIds.Contains(id)).ToArray())
        {
            _alertStates.Remove(threadId);
            _settings.AlertStates.Remove(threadId);
            stateChanged = true;
        }

        foreach (var item in candidates)
        {
            if (item.Thread.Archived || item.Analysis.Zone == BudgetZone.Unavailable)
            {
                continue;
            }

            if (item.Analysis.BudgetPercent < 75)
            {
                if (_alertStates.Remove(item.Thread.Id))
                {
                    _settings.AlertStates.Remove(item.Thread.Id);
                    stateChanged = true;
                }

                continue;
            }

            _alertStates.TryGetValue(item.Thread.Id, out var current);
            var initialObservation = !_alertsInitialized && current == AlertState.None;
            if (item.Analysis.Zone == BudgetZone.OverBudget && current < AlertState.OverBudget)
            {
                if (!initialObservation || item.Thread.Id == snapshot.Tracked?.Root.Id)
                {
                    ShowAlert(item, overBudget: true);
                }

                _alertStates[item.Thread.Id] = AlertState.OverBudget;
                _settings.AlertStates[item.Thread.Id] = (int)AlertState.OverBudget;
                stateChanged = true;
            }
            else if (item.Analysis.Zone == BudgetZone.Critical && current < AlertState.Critical)
            {
                if (!initialObservation || item.Thread.Id == snapshot.Tracked?.Root.Id)
                {
                    ShowAlert(item, overBudget: false);
                }

                _alertStates[item.Thread.Id] = AlertState.Critical;
                _settings.AlertStates[item.Thread.Id] = (int)AlertState.Critical;
                stateChanged = true;
            }
        }

        _alertsInitialized = true;
        if (stateChanged)
        {
            ScheduleSave();
        }
    }

    private void ShowAlert(ThreadBreakdown item, bool overBudget)
    {
        var chatName = _settings.PrivacyMode ? (item.Thread.IsSubagent ? "A subagent" : "A Codex chat") : DisplayTitle(item.Thread);
        var title = overBudget ? "Token HUD · Context budget reached" : "Token HUD · Critical context";
        var message = $"{chatName} is at {FormatTokens(item.Analysis.Load)} of {FormatTokens(_settings.ContextBudget)}. Open Token HUD to review it.";
        if (_notifications?.TryShow(title, message, item.Thread.Id) != true)
        {
            _trayIcon.ShowBalloonTip(7_000, title, message, System.Windows.Forms.ToolTipIcon.Warning);
        }
    }

    private void ApplySettingsToControls()
    {
        _updatingUi = true;
        try
        {
            Topmost = _settings.AlwaysOnTop;
            TopmostCheckBox.IsChecked = _settings.AlwaysOnTop;
            NotifyCheckBox.IsChecked = _settings.NotificationsEnabled;
            ArchivedCheckBox.IsChecked = _settings.ShowArchived;
            BudgetBox.Text = _settings.ContextBudget.ToString("N0", CultureInfo.CurrentCulture);
            PauseButton.Content = _settings.MonitoringPaused ? "Resume monitoring" : "Pause monitoring";
            PrivacyButton.Foreground = FindBrush(_settings.PrivacyMode ? "PrimaryBrush" : "MutedBrush");
            PrivacyButton.ToolTip = _settings.PrivacyMode ? "Show chat names" : "Hide chat names";
            AutomationProperties.SetName(PrivacyButton, _settings.PrivacyMode ? "Show chat names" : "Hide chat names");
        }
        finally
        {
            _updatingUi = false;
        }
    }

    private void RestorePosition()
    {
        if (_settings.Left is { } left && _settings.Top is { } top)
        {
            var workArea = SystemParameters.WorkArea;
            Left = Math.Clamp(left, workArea.Left, Math.Max(workArea.Left, workArea.Right - 120));
            Top = Math.Clamp(top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - 80));
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            var workArea = SystemParameters.WorkArea;
            Left = workArea.Right - Width - 24;
            Top = workArea.Bottom - Height - 24;
        }
    }

    private void ApplyViewMode()
    {
        _applyingViewMode = true;
        try
        {
            if (_settings.Expanded)
            {
                CompactPanel.Visibility = Visibility.Collapsed;
                ExpandedPanel.Visibility = Visibility.Visible;
                StatusBar.Visibility = Visibility.Visible;
                MinWidth = 480;
                MinHeight = 400;
                Width = Math.Max(_settings.ExpandedWidth, MinWidth);
                Height = Math.Max(_settings.ExpandedHeight, MinHeight);
                ExpandButton.ToolTip = "Show compact view";
                AutomationProperties.SetName(ExpandButton, "Show compact view");
            }
            else
            {
                CompactPanel.Visibility = Visibility.Visible;
                ExpandedPanel.Visibility = Visibility.Collapsed;
                SettingsPopup.IsOpen = false;
                StatusBar.Visibility = Visibility.Collapsed;
                MinWidth = 360;
                MinHeight = 360;
                Width = Math.Max(_settings.CompactWidth, MinWidth);
                Height = Math.Max(_settings.CompactHeight, MinHeight);
                ExpandButton.ToolTip = "Show all chats and details";
                AutomationProperties.SetName(ExpandButton, "Show all chats and details");
            }
        }
        finally
        {
            _applyingViewMode = false;
        }

        EnsureWithinWorkArea();
    }

    private void NormalizeGeometrySettings()
    {
        if (!double.IsFinite(_settings.CompactWidth) || _settings.CompactWidth is < 360 or > 640)
        {
            _settings.CompactWidth = 392;
        }

        if (!double.IsFinite(_settings.CompactHeight) || _settings.CompactHeight is < 360 or > 520)
        {
            _settings.CompactHeight = 390;
        }

        if (!double.IsFinite(_settings.ExpandedWidth) || _settings.ExpandedWidth is < 480 or > 1_800)
        {
            _settings.ExpandedWidth = 860;
        }

        if (!double.IsFinite(_settings.ExpandedHeight) || _settings.ExpandedHeight is < 400 or > 1_200)
        {
            _settings.ExpandedHeight = 720;
        }
    }

    private void EnsureWithinWorkArea()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (_demo || handle == 0 || !GetWindowRect(handle, out var bounds))
        {
            return;
        }

        var workArea = System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var dpi = VisualTreeHelper.GetDpi(this);
        var width = Math.Min(workArea.Width, bounds.Right - bounds.Left);
        var height = Math.Min(workArea.Height, bounds.Bottom - bounds.Top);
        var left = Math.Clamp(bounds.Left, workArea.Left, workArea.Right - width);
        var top = Math.Clamp(bounds.Top, workArea.Top, workArea.Bottom - height);
        MinWidth = Math.Min(_settings.Expanded ? 480 : 360, workArea.Width / dpi.DpiScaleX);
        MinHeight = Math.Min(_settings.Expanded ? 400 : 360, workArea.Height / dpi.DpiScaleY);
        if (left != bounds.Left || top != bounds.Top || width != bounds.Right - bounds.Left || height != bounds.Bottom - bounds.Top)
        {
            const uint noZOrderOrActivation = 0x0014;
            _ = SetWindowPos(handle, 0, left, top, width, height, noZOrderOrActivation);
        }
    }

    private void ExpandedPanel_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateInspectorLayout();

    private void BrowserToggleButton_Click(object sender, RoutedEventArgs e)
    {
        _showChatBrowser = !_showChatBrowser;
        UpdateInspectorLayout();
        if (_showChatBrowser)
        {
            SearchBox.Focus();
        }
    }

    private void UpdateInspectorLayout()
    {
        var narrow = ExpandedPanel.ActualWidth is > 0 and < 650;
        BrowserToggleButton.Visibility = narrow ? Visibility.Visible : Visibility.Collapsed;
        BrowserToggleButton.Content = _showChatBrowser ? "Show details" : "Browse chats";
        BrowserPanel.Visibility = !narrow || _showChatBrowser ? Visibility.Visible : Visibility.Collapsed;
        DetailPanel.Visibility = !narrow || !_showChatBrowser ? Visibility.Visible : Visibility.Collapsed;
        BrowserColumn.Width = narrow
            ? new GridLength(_showChatBrowser ? 1 : 0, GridUnitType.Star)
            : new GridLength(245);
        BrowserGapColumn.Width = new GridLength(narrow ? 0 : 12);
    }

    private void ToggleExpanded()
    {
        RememberCurrentSize();
        _settings.Expanded = !_settings.Expanded;
        ApplyViewMode();
        ScheduleSave();
    }

    private void TogglePrivacy()
    {
        _settings.PrivacyMode = !_settings.PrivacyMode;
        _privacyMenu.Checked = _settings.PrivacyMode;
        PrivacyButton.Foreground = FindBrush(_settings.PrivacyMode ? "PrimaryBrush" : "MutedBrush");
        PrivacyButton.ToolTip = _settings.PrivacyMode ? "Show chat names" : "Hide chat names";
        AutomationProperties.SetName(PrivacyButton, _settings.PrivacyMode ? "Show chat names" : "Hide chat names");
        if (_snapshot is not null)
        {
            UpdateSnapshot(_snapshot);
        }

        ScheduleSave();
    }

    private void ToggleTopmost()
    {
        _settings.AlwaysOnTop = !_settings.AlwaysOnTop;
        Topmost = _settings.AlwaysOnTop;
        TopmostCheckBox.IsChecked = _settings.AlwaysOnTop;
        _topmostMenu.Checked = _settings.AlwaysOnTop;
        ScheduleSave();
    }

    private void TogglePause()
    {
        _settings.MonitoringPaused = !_settings.MonitoringPaused;
        PauseButton.Content = _settings.MonitoringPaused ? "Resume monitoring" : "Pause monitoring";
        _pauseMenu.Checked = _settings.MonitoringPaused;
        UpdateMonitorStatus();
        if (!_settings.MonitoringPaused)
        {
            _monitor?.RequestRefresh();
        }

        ScheduleSave();
    }

    private void ToggleStartup()
    {
        try
        {
            _settings.StartWithWindows = !_settings.StartWithWindows;
            StartupRegistration.SetEnabled(_settings.StartWithWindows);
            _startupMenu.Checked = _settings.StartWithWindows;
            ScheduleSave();
        }
        catch
        {
            _settings.StartWithWindows = !_demo && StartupRegistration.IsEnabled();
            _startupMenu.Checked = _settings.StartWithWindows;
            _ = _diagnostics.WriteAsync("startup_registration_failed", DiagnosticLevel.Warning);
        }
    }

    private void PinButton_Click(object sender, RoutedEventArgs e)
    {
        var tracked = _snapshot?.Tracked;
        var target = _selectedThreadId ?? tracked?.Root.Id;
        if (target is null)
        {
            return;
        }

        _settings.PinnedThreadId = IsPinnedChat(target, tracked) ? null : target;
        _selectedThreadId = null;
        RefreshInspection();
        ScheduleSave();
    }

    private void FollowLatestButton_Click(object sender, RoutedEventArgs e)
    {
        var hadPin = _settings.PinnedThreadId is not null;
        _settings.PinnedThreadId = null;
        _selectedThreadId = null;
        RefreshInspection();
        if (hadPin)
        {
            ScheduleSave();
        }
    }

    private void PrivacyButton_Click(object sender, RoutedEventArgs e) => TogglePrivacy();
    private void ExpandButton_Click(object sender, RoutedEventArgs e) => ToggleExpanded();
    private async void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (_demo)
        {
            await QuitAsync();
        }
        else
        {
            Hide();
        }
    }

    private void PauseButton_Click(object sender, RoutedEventArgs e) => TogglePause();

    private void TopmostCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _settings.AlwaysOnTop = TopmostCheckBox.IsChecked == true;
        Topmost = _settings.AlwaysOnTop;
        _topmostMenu.Checked = _settings.AlwaysOnTop;
        ScheduleSave();
    }

    private void NotifyCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _settings.NotificationsEnabled = NotifyCheckBox.IsChecked == true;
        ScheduleSave();
    }

    private void ArchivedCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _settings.ShowArchived = ArchivedCheckBox.IsChecked == true;
        if (!_demo && _snapshot is not null && !_settings.ShowArchived)
        {
            UpdateSnapshot(_snapshot);
        }

        RefreshNavigation();
        ScheduleSave();
    }

    private void ChatList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updatingUi || ChatList.SelectedItem is not ChatListItem selected)
        {
            return;
        }

        _selectedThreadId = selected.Id;
        RefreshInspection();
    }

    private void RecentChatButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string threadId } || string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        _selectedThreadId = threadId;
        if (!_settings.Expanded)
        {
            RememberCurrentSize();
        }

        _settings.Expanded = true;
        ApplyViewMode();
        RefreshInspection();
        ScheduleSave();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;

    private void CloseSettingsButton_Click(object sender, RoutedEventArgs e) => SettingsPopup.IsOpen = false;

    private void SettingsPopup_Opened(object? sender, EventArgs e) =>
        _ = Dispatcher.InvokeAsync(() => BudgetBox.Focus(), DispatcherPriority.Input);

    private void SettingsPopup_Closed(object? sender, EventArgs e)
    {
        if (ExpandedPanel.IsVisible && IsActive)
        {
            SettingsButton.Focus();
        }
    }

    private void SettingsSurface_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            SettingsPopup.IsOpen = false;
        }
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingUi)
        {
            return;
        }

        _searchTimer.Stop();
        _searchText = SearchBox.Text;
        _searchTimer.Start();
    }

    private void SearchTimer_Tick(object? sender, EventArgs e)
    {
        _searchTimer.Stop();
        RefreshNavigation();
    }

    private void BudgetBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyBudgetText();
            Keyboard.ClearFocus();
        }
    }

    private void BudgetBox_LostFocus(object sender, RoutedEventArgs e) => ApplyBudgetText();

    private void ApplyBudgetText()
    {
        var text = BudgetBox.Text.Trim().Replace(",", string.Empty, StringComparison.Ordinal).Replace(" ", string.Empty, StringComparison.Ordinal);
        long multiplier = 1;
        if (text.EndsWith("k", StringComparison.OrdinalIgnoreCase))
        {
            multiplier = 1_000;
            text = text[..^1];
        }

        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var parsed))
        {
            _settings.ContextBudget = Math.Clamp((long)Math.Round(parsed * multiplier), 10_000, 1_000_000);
            BudgetBox.Text = _settings.ContextBudget.ToString("N0", CultureInfo.CurrentCulture);
            _monitor?.RequestRefresh();
            ScheduleSave();
        }
        else
        {
            BudgetBox.Text = _settings.ContextBudget.ToString("N0", CultureInfo.CurrentCulture);
        }
    }

    private void TitleStrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (OriginatesFromButton(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount == 2)
        {
            ToggleExpanded();
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape)
        {
            return;
        }

        if (SettingsPopup.IsOpen)
        {
            SettingsPopup.IsOpen = false;
            e.Handled = true;
        }
    }

    private static bool OriginatesFromButton(DependencyObject? element)
    {
        while (element is not null)
        {
            if (element is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return false;
    }

    private void Window_GeometryChanged(object? sender, EventArgs e)
    {
        if (!IsLoaded || WindowState != WindowState.Normal || _applyingViewMode)
        {
            return;
        }

        _settings.Left = Left;
        _settings.Top = Top;
        RememberCurrentSize();
        ScheduleSave();
    }

    private void RememberCurrentSize()
    {
        if (_settings.Expanded)
        {
            _settings.ExpandedWidth = Width;
            _settings.ExpandedHeight = Height;
        }
        else
        {
            _settings.CompactWidth = Width;
            _settings.CompactHeight = Height;
        }
    }

    private void ScheduleSave()
    {
        if (_demo)
        {
            return;
        }

        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private async void SaveTimer_Tick(object? sender, EventArgs e)
    {
        _saveTimer.Stop();
        try
        {
            await _settingsStore.SaveAsync(_settings);
        }
        catch
        {
            await _diagnostics.WriteAsync("settings_save_failed", DiagnosticLevel.Warning);
        }
    }

    private void OpenDiagnostics()
    {
        var path = new AppDataPaths().Diagnostics;
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo("explorer.exe", path) { UseShellExecute = true });
    }

    private void CopyDiagnosticSummary()
    {
        System.Windows.Clipboard.SetText(_diagnostics.BuildSummary(_snapshot));
    }

    private async Task QuitAsync()
    {
        if (_quitting)
        {
            return;
        }

        _quitting = true;
        _freshnessTimer.Stop();
        _trayIcon.Visible = false;
        _notifications?.Dispose();
        _trayIcon.Dispose();
        if (_monitor is not null)
        {
            await _monitor.DisposeAsync();
        }

        if (!_demo)
        {
            await _settingsStore.SaveAsync(_settings);
        }

        System.Windows.Application.Current.Shutdown();
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (_demo && !_quitting)
        {
            e.Cancel = true;
            _ = Dispatcher.InvokeAsync(QuitAsync, DispatcherPriority.Background);
            return;
        }

        if (_quitting)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (WindowState == WindowState.Minimized)
        {
            Hide();
            WindowState = WindowState.Normal;
        }
    }

    private void Window_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var corner = 2;
        _ = DwmSetWindowAttribute(handle, 33, ref corner, sizeof(int));
        ApplyDarkWindowFrame(handle);
        _ = Dispatcher.InvokeAsync(EnsureWithinWorkArea, DispatcherPriority.Loaded);
    }

    private void ApplyDarkWindowFrame(nint handle)
    {
        var dark = _theme.IsDark ? 1 : 0;
        if (DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)) != 0)
        {
            _ = DwmSetWindowAttribute(handle, 19, ref dark, sizeof(int));
        }
    }

    private string DisplayTitle(ThreadRecord thread)
    {
        if (_settings.PrivacyMode)
        {
            return thread.IsSubagent ? "Subagent" : "Codex chat";
        }

        if (!string.IsNullOrWhiteSpace(thread.Title))
        {
            return string.Join(' ', thread.Title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        }

        var project = string.IsNullOrWhiteSpace(thread.Cwd)
            ? "Codex"
            : Path.GetFileName(thread.Cwd.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var shortId = thread.Id.Length > 8 ? thread.Id[..8] : thread.Id;
        return $"{project} · {shortId}";
    }

    private static string FormatVelocity(ContextAnalysis analysis)
    {
        if (analysis.MedianPositiveDelta <= 0)
        {
            return "Waiting for usage samples";
        }

        var estimate = analysis.EventsToNextZone is null
            ? string.Empty
            : $" · ~{analysis.EventsToNextZone} event{(analysis.EventsToNextZone == 1 ? string.Empty : "s")} to {analysis.NextZoneName}";
        var rate = analysis.TokensPerMinute > 0 ? $" · {FormatTokens((long)analysis.TokensPerMinute)}/min" : string.Empty;
        return $"↑ {FormatTokens(analysis.MedianPositiveDelta)}/event{rate}{estimate}";
    }

    private static string FormatContext(ContextAnalysis? analysis) =>
        analysis is { Zone: not BudgetZone.Unavailable } ? FormatTokens(analysis.Load) : "—";

    private static string FormatMeasuredTokens(long value, bool available) => available ? FormatTokens(value) : "—";

    private static string FormatTokens(long value)
    {
        var absolute = Math.Abs(value);
        return absolute switch
        {
            >= 1_000_000 => $"{value / 1_000_000d:0.##}M",
            >= 1_000 => $"{value / 1_000d:0.#}k",
            _ => value.ToString("N0", CultureInfo.CurrentCulture),
        };
    }

    private static string ZoneName(BudgetZone zone) => zone switch
    {
        BudgetZone.Healthy => "Healthy",
        BudgetZone.Caution => "Caution",
        BudgetZone.Critical => "Critical",
        BudgetZone.OverBudget => "Over budget",
        BudgetZone.Unavailable => "Unavailable",
        _ => "Unknown",
    };

    private static string ZoneBrushName(BudgetZone zone) => zone switch
    {
        BudgetZone.Healthy => "HealthyBrush",
        BudgetZone.Caution => "CautionBrush",
        BudgetZone.Critical => "CriticalBrush",
        BudgetZone.OverBudget => "OverBudgetBrush",
        _ => "MutedBrush",
    };

    private static System.Windows.Media.Brush FindBrush(string key) => (System.Windows.Media.Brush)System.Windows.Application.Current.Resources[key];

    private static string MakeTrayText(ChatUsage? tracked, bool stale, bool paused)
    {
        var value = tracked?.Analysis.Zone switch
        {
            null => "Waiting for Codex",
            BudgetZone.Unavailable => "Context unavailable",
            _ => $"{FormatContext(tracked.Analysis)} context · {ZoneName(tracked.Analysis.Zone)}",
        };
        var status = stale ? "Stale · " : paused ? "Paused · " : string.Empty;
        var text = $"Token HUD · {status}{value}";
        return text.Length <= 63 ? text : text[..63];
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int valueSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowBounds
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(nint window, out WindowBounds bounds);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(nint window, nint insertAfter, int x, int y, int width, int height, uint flags);

    private enum AlertState
    {
        None,
        Critical,
        OverBudget,
    }
}
