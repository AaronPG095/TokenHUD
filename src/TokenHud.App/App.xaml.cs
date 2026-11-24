using System.Windows;
using TokenHud.Core;

namespace TokenHud.App;

public partial class App : System.Windows.Application
{
    private SingleInstanceCoordinator? _singleInstance;
    private ThemeService? _theme;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Length == 1 && e.Args[0].Equals("--ingest-provider", StringComparison.OrdinalIgnoreCase))
        {
            using var input = Console.OpenStandardInput();
            var store = new ProviderTelemetryStore(new AppDataPaths().ProviderTelemetry);
            var result = store.ImportAsync(input, DateTimeOffset.UtcNow).GetAwaiter().GetResult();
            Shutdown(result == ProviderImportResult.Imported ? 0 : result == ProviderImportResult.Invalid ? 2 : 1);
            return;
        }

        var demo = e.Args.Contains("--demo", StringComparer.OrdinalIgnoreCase);
        if (!demo)
        {
            _singleInstance = new SingleInstanceCoordinator();
            if (!_singleInstance.IsPrimary)
            {
                _singleInstance.SignalPrimary();
                Shutdown();
                return;
            }
        }

        _theme = new ThemeService();
        var appData = demo
            ? new AppDataPaths(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"TokenHUD-demo-{Environment.ProcessId}"))
            : new AppDataPaths();
        var settingsStore = new SettingsStore(appData);
        var settings = demo
            ? new HudSettings { NotificationsEnabled = false, StartWithWindows = false }
            : settingsStore.LoadAsync().GetAwaiter().GetResult();
        var diagnostics = new RollingDiagnostics(appData);
        var history = demo ? null : new MetricsHistoryStore(appData.MetricsHistory);
        var feed = new ProviderTelemetryStore(appData.ProviderTelemetry);
        var window = new MainWindow(settings, settingsStore, diagnostics, _theme, demo, history) { ProviderFeed = demo ? null : feed };
        MainWindow = window;
        _singleInstance?.StartListening(() => Dispatcher.BeginInvoke(window.ShowFromTray));
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _theme?.Dispose();
        base.OnExit(e);
    }
}
