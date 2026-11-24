using System.IO;
using System.Security;
using Microsoft.Win32;

namespace TokenHud.App;

internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "TokenHUD";

    public static bool TryGetEnabled(out bool enabled)
    {
        enabled = false;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            enabled = key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }

    public static bool TrySetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                if (Environment.ProcessPath is not { } executable)
                {
                    return false;
                }

                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                key.SetValue(ValueName, $"\"{executable}\" --startup", RegistryValueKind.String);
            }
            else
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
                key?.DeleteValue(ValueName, throwOnMissingValue: false);
            }

            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or SecurityException)
        {
            return false;
        }
    }
}
