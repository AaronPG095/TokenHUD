using CommunityToolkit.WinUI.Notifications;
using TokenHud.Core;

namespace TokenHud.App;

internal sealed class WindowsNotificationService : IDisposable
{
    private readonly RollingDiagnostics _diagnostics;
    private bool _registered;

    public WindowsNotificationService(Action activated, RollingDiagnostics diagnostics)
    {
        _diagnostics = diagnostics;
        try
        {
            ToastNotificationManagerCompat.OnActivated += _ => activated();
            _ = ToastNotificationManagerCompat.CreateToastNotifier();
            _registered = true;
        }
        catch (Exception exception)
        {
            _ = _diagnostics.WriteAsync(
                $"native_notification_registration_failed_{exception.GetType().Name}",
                DiagnosticLevel.Warning,
                new Dictionary<string, long> { ["hresult"] = exception.HResult });
        }
    }

    public bool TryShow(string title, string message, string threadId)
    {
        if (!_registered)
        {
            return false;
        }

        try
        {
            new ToastContentBuilder()
                .AddArgument("thread", threadId)
                .AddText(title)
                .AddText(message)
                .AddAudio(new ToastAudio { Silent = true })
                .Show(toast => toast.ExpirationTime = DateTimeOffset.Now.AddHours(8));
            return true;
        }
        catch (Exception exception)
        {
            _ = _diagnostics.WriteAsync(
                $"native_notification_show_failed_{exception.GetType().Name}",
                DiagnosticLevel.Warning,
                new Dictionary<string, long> { ["hresult"] = exception.HResult });
            return false;
        }
    }

    public void Dispose()
    {
        _registered = false;
    }
}
