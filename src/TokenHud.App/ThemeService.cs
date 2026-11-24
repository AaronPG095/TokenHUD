using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace TokenHud.App;

internal sealed class ThemeService : IDisposable
{
    public ThemeService()
    {
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        Apply();
    }

    public bool IsDark { get; private set; }

    public void Apply()
    {
        var highContrast = SystemParameters.HighContrast;
        ApplyPalette(highContrast, !highContrast && ReadDarkMode());
    }

    internal void ApplyPalette(bool highContrast, bool isDark)
    {
        if (highContrast)
        {
            Set("CanvasBrush", System.Windows.SystemColors.WindowColor);
            Set("SurfaceBrush", System.Windows.SystemColors.WindowColor);
            Set("SurfaceStrongBrush", System.Windows.SystemColors.WindowColor);
            Set("InkBrush", System.Windows.SystemColors.WindowTextColor);
            Set("MutedBrush", System.Windows.SystemColors.WindowTextColor);
            Set("PrimaryBrush", System.Windows.SystemColors.HighlightColor);
            Set("AccentBrush", System.Windows.SystemColors.HighlightColor);
            Set("OnPrimaryBrush", System.Windows.SystemColors.HighlightTextColor);
            Set("DividerBrush", System.Windows.SystemColors.WindowTextColor);
            Set("HealthyBrush", System.Windows.SystemColors.WindowTextColor);
            Set("CautionBrush", System.Windows.SystemColors.WindowTextColor);
            Set("CriticalBrush", System.Windows.SystemColors.WindowTextColor);
            Set("OverBudgetBrush", System.Windows.SystemColors.WindowTextColor);
            IsDark = false;
            return;
        }

        IsDark = isDark;
        var palette = IsDark
            ? new Dictionary<string, string>
            {
                ["CanvasBrush"] = "#09090B",
                ["SurfaceBrush"] = "#18181B",
                ["SurfaceStrongBrush"] = "#27272A",
                ["InkBrush"] = "#FAFAFA",
                ["MutedBrush"] = "#A1A1AA",
                ["PrimaryBrush"] = "#65B8FF",
                ["AccentBrush"] = "#78C2FF",
                ["OnPrimaryBrush"] = "#09090B",
                ["DividerBrush"] = "#27272A",
                ["HealthyBrush"] = "#86EFAC",
                ["CautionBrush"] = "#FCD34D",
                ["CriticalBrush"] = "#FDBA74",
                ["OverBudgetBrush"] = "#FCA5A5",
            }
            : new Dictionary<string, string>
            {
                ["CanvasBrush"] = "#FAFAFA",
                ["SurfaceBrush"] = "#F4F4F5",
                ["SurfaceStrongBrush"] = "#E4E4E7",
                ["InkBrush"] = "#09090B",
                ["MutedBrush"] = "#52525B",
                ["PrimaryBrush"] = "#005FA8",
                ["AccentBrush"] = "#0067B5",
                ["OnPrimaryBrush"] = "#FAFAFA",
                ["DividerBrush"] = "#E4E4E7",
                ["HealthyBrush"] = "#166534",
                ["CautionBrush"] = "#854D0E",
                ["CriticalBrush"] = "#9A3412",
                ["OverBudgetBrush"] = "#B91C1C",
            };

        foreach (var (key, value) in palette)
        {
            Set(key, (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(value));
        }
    }

    public void Dispose() => SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) =>
        System.Windows.Application.Current?.Dispatcher.Invoke(Apply);

    private static bool ReadDarkMode()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static void Set(string key, System.Windows.Media.Color color)
    {
        var resources = System.Windows.Application.Current.Resources;
        if (resources[key] is SolidColorBrush { IsFrozen: false } brush)
        {
            // Code-behind and existing chat rows can hold references to this brush.
            brush.Color = color;
        }
        else
        {
            resources[key] = new SolidColorBrush(color);
        }
    }
}
