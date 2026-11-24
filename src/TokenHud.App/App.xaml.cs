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
        var window = new MainWindow(settings, settingsStore, diagnostics, _theme, demo);
        MainWindow = window;
        _singleInstance?.StartListening(() => Dispatcher.Invoke(window.ShowFromTray));
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        _theme?.Dispose();
        base.OnExit(e);
    }
}
