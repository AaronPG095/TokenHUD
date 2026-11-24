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
    private string _searchText = string.Empty;
    private bool _updatingUi;
    private bool _applyingViewMode;
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
            UpdateSnapshot(DemoData.Create());
        }

        UpdateMonitorStatus();
    }

    private MonitorOptions GetMonitorOptions() => new(
        _settings.PinnedThreadId,
        _settings.ContextBudget,
        _settings.ShowArchived,
        _searchText,
        _settings.MonitoringPaused);

    private void Monitor_SnapshotChanged(object? sender, HudSnapshot snapshot) =>
        Dispatcher.BeginInvoke(() => UpdateSnapshot(snapshot));

    private void UpdateSnapshot(HudSnapshot snapshot)
    {
        _snapshot = snapshot;
        var tracked = snapshot.Tracked;
        _updatingUi = true;
        try
        {
            if (tracked is null)
            {
                TrackedTitle.Text = "No local Codex chats found";
                ModeLabel.Text = "Waiting for a chat";
                UpdateContextMeter(null);
                OriginStatusLabel.Text = "Context origin unavailable";
                _threadItems.Clear();
                UpdateBreakdown(null);
            }
            else
            {
                TrackedTitle.Text = DisplayTitle(tracked.Root);
                var isPinned = _settings.PinnedThreadId is not null;
                ModeLabel.Text = isPinned ? "Selected chat" : "Most recent chat";
                PinButton.Foreground = FindBrush(isPinned ? "PrimaryBrush" : "MutedBrush");
                PinButton.ToolTip = isPinned ? "Follow latest chat" : "Pin current chat";
                AutomationProperties.SetName(PinButton, isPinned ? "Follow latest chat" : "Pin current chat");
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

    private void UpdateContextMeter(ContextAnalysis? analysis)
    {
        var zone = analysis?.Zone ?? BudgetZone.Unavailable;
        var zoneBrush = FindBrush(ZoneBrushName(zone));
        ContextValue.Text = FormatContext(analysis);
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
        var hasContext = tracked is { Analysis.Zone: not BudgetZone.Unavailable, Latest.HasContext: true };
        var flow = hasContext ? tracked!.Latest!.Context : TokenBreakdown.Empty;
        var newInput = Math.Max(0, flow.InputTokens - flow.CachedInputTokens - flow.CacheWriteInputTokens);
        var visibleOutput = Math.Max(0, flow.OutputTokens - flow.ReasoningOutputTokens);

        InputValue.Text = FormatMeasuredTokens(flow.InputTokens, hasContext);
        CachedValue.Text = FormatMeasuredTokens(flow.CachedInputTokens, hasContext);
        CacheWriteValue.Text = FormatMeasuredTokens(flow.CacheWriteInputTokens, hasContext);
        NewInputValue.Text = FormatMeasuredTokens(newInput, hasContext);
        OutputValue.Text = FormatMeasuredTokens(flow.OutputTokens, hasContext);
        ReasoningValue.Text = FormatMeasuredTokens(flow.ReasoningOutputTokens, hasContext);
        VisibleOutputValue.Text = FormatMeasuredTokens(visibleOutput, hasContext);

        SetFlowColumns(InputCachedColumn, InputWriteColumn, InputNewColumn,
            flow.CachedInputTokens, flow.CacheWriteInputTokens, newInput);
        SetFlowColumns(OutputReasoningColumn, OutputVisibleColumn,
            flow.ReasoningOutputTokens, visibleOutput);
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
        _threadItems.Clear();
        foreach (var item in tracked.Threads)
        {
            var name = _settings.PrivacyMode
                ? (item.Depth == 0 ? "Top-level chat" : $"Subagent {item.Depth}")
                : DisplayTitle(item.Thread);
            var indent = item.Depth == 0 ? "●" : new string('·', Math.Min(item.Depth, 3));
            var hasTotal = item.Latest?.HasSessionTotal == true;
            var total = item.Latest?.SessionTotal ?? TokenBreakdown.Empty;
            _threadItems.Add(new ThreadDisplayItem(
                indent,
                name,
                FormatContext(item.Analysis),
                ZoneName(item.Analysis.Zone),
                FormatMeasuredTokens(total.InputTokens, hasTotal),
                FormatMeasuredTokens(total.CachedInputTokens, hasTotal),
                FormatMeasuredTokens(total.OutputTokens, hasTotal),
                FormatMeasuredTokens(total.ReasoningOutputTokens, hasTotal),
                FormatMeasuredTokens(total.TotalTokens, hasTotal)));
        }
    }

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
        var selectedId = (ChatList.SelectedItem as ChatListItem)?.Id;
        var usageById = snapshot.RecentChats.ToDictionary(chat => chat.Root.Id, StringComparer.Ordinal);
        _chatItems.Clear();

        if (!string.IsNullOrWhiteSpace(SearchBox.Text))
        {
            foreach (var chat in snapshot.SearchableChats)
            {
                usageById.TryGetValue(chat.Id, out var usage);
                var context = FormatContext(usage?.Analysis);
                var zone = usage?.Analysis.Zone ?? BudgetZone.Unavailable;
                _chatItems.Add(new ChatListItem(
                    chat.Id,
                    DisplayTitle(chat),
                    chat.Archived ? "Archived" : $"Updated {chat.UpdatedAt.LocalDateTime:g}",
                    context,
                    usage is null ? "Not sampled" : ZoneName(zone),
                    usage is null ? FindBrush("MutedBrush") : FindBrush(ZoneBrushName(zone)),
                    chat.Archived,
                    chat.Id == _settings.PinnedThreadId));
            }
        }
        else
        {
            foreach (var chat in snapshot.RecentChats)
            {
                _chatItems.Add(new ChatListItem(
                    chat.Root.Id,
                    DisplayTitle(chat.Root),
                    ZoneName(chat.Analysis.Zone),
                    FormatContext(chat.Analysis),
                    ZoneName(chat.Analysis.Zone),
                    FindBrush(ZoneBrushName(chat.Analysis.Zone)),
                    chat.Root.Archived,
                    chat.Root.Id == _settings.PinnedThreadId));
            }
        }

        ChatList.SelectedItem = _chatItems.FirstOrDefault(item => item.Id == snapshot.Tracked?.Root.Id)
            ?? _chatItems.FirstOrDefault(item => item.Id == selectedId);
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
                MinWidth = 720;
                MinHeight = 680;
                Width = Math.Max(_settings.ExpandedWidth, MinWidth);
                Height = Math.Max(_settings.ExpandedHeight, MinHeight);
                ExpandButton.ToolTip = "Show compact view";
                AutomationProperties.SetName(ExpandButton, "Show compact view");
            }
            else
            {
                CompactPanel.Visibility = Visibility.Visible;
                ExpandedPanel.Visibility = Visibility.Collapsed;
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

        if (!double.IsFinite(_settings.ExpandedWidth) || _settings.ExpandedWidth is < 720 or > 1_800)
        {
            _settings.ExpandedWidth = 860;
        }

        if (!double.IsFinite(_settings.ExpandedHeight) || _settings.ExpandedHeight is < 680 or > 1_200)
        {
            _settings.ExpandedHeight = 720;
        }
    }

    private void EnsureWithinWorkArea()
    {
        if (double.IsNaN(Left) || double.IsNaN(Top))
        {
            return;
        }

        var workArea = SystemParameters.WorkArea;
        Left = Math.Clamp(Left, workArea.Left, Math.Max(workArea.Left, workArea.Right - Width));
        Top = Math.Clamp(Top, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - Height));
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
        if (_snapshot?.Tracked is null)
        {
            return;
        }

        _settings.PinnedThreadId = _settings.PinnedThreadId is null ? _snapshot.Tracked.Root.Id : null;
        _monitor?.RequestRefresh();
        ScheduleSave();
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
        _monitor?.RequestRefresh();
        ScheduleSave();
    }

    private void ChatList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_updatingUi || ChatList.SelectedItem is not ChatListItem selected || selected.Id == _snapshot?.Tracked?.Root.Id)
        {
            return;
        }

        _settings.PinnedThreadId = selected.Id;
        _monitor?.RequestRefresh();
        ScheduleSave();
    }

    private void RecentChatButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button { Tag: string threadId } || string.IsNullOrWhiteSpace(threadId))
        {
            return;
        }

        _settings.PinnedThreadId = threadId;
        _settings.Expanded = true;
        ApplyViewMode();
        _monitor?.RequestRefresh();
        ScheduleSave();
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e) =>
        SettingsPopup.IsOpen = !SettingsPopup.IsOpen;

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (_updatingUi || _monitor is null)
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
        _monitor?.RequestRefresh();
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

    private enum AlertState
    {
        None,
        Critical,
        OverBudget,
    }
}
