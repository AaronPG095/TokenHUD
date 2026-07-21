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
        if (SystemParameters.HighContrast)
        {
            Set("CanvasBrush", System.Windows.SystemColors.WindowColor);
            Set("SurfaceBrush", System.Windows.SystemColors.ControlColor);
            Set("SurfaceStrongBrush", System.Windows.SystemColors.ControlDarkColor);
            Set("InkBrush", System.Windows.SystemColors.WindowTextColor);
            Set("MutedBrush", System.Windows.SystemColors.GrayTextColor);
            Set("PrimaryBrush", System.Windows.SystemColors.HighlightColor);
            Set("AccentBrush", System.Windows.SystemColors.HighlightColor);
            Set("HealthyBrush", System.Windows.SystemColors.WindowTextColor);
            Set("CautionBrush", System.Windows.SystemColors.WindowTextColor);
            Set("CriticalBrush", System.Windows.SystemColors.HotTrackColor);
            Set("OverBudgetBrush", System.Windows.SystemColors.HotTrackColor);
            IsDark = false;
            return;
        }

        IsDark = ReadDarkMode();
        var palette = IsDark
            ? new Dictionary<string, string>
            {
                ["CanvasBrush"] = "#030303",
                ["SurfaceBrush"] = "#0C0D11",
                ["SurfaceStrongBrush"] = "#191A20",
                ["InkBrush"] = "#EAEBF2",
                ["MutedBrush"] = "#9C9DAB",
                ["PrimaryBrush"] = "#9499FA",
                ["AccentBrush"] = "#59C8E8",
                ["HealthyBrush"] = "#57BC80",
                ["CautionBrush"] = "#F3B94C",
                ["CriticalBrush"] = "#FF7C63",
                ["OverBudgetBrush"] = "#FC5855",
            }
            : new Dictionary<string, string>
            {
                ["CanvasBrush"] = "#FFFFFF",
                ["SurfaceBrush"] = "#F2F3F7",
                ["SurfaceStrongBrush"] = "#E4E5EE",
                ["InkBrush"] = "#181926",
                ["MutedBrush"] = "#5A5C6C",
                ["PrimaryBrush"] = "#5552BB",
                ["AccentBrush"] = "#2FB5D8",
                ["HealthyBrush"] = "#1C985A",
                ["CautionBrush"] = "#D59800",
                ["CriticalBrush"] = "#E5462C",
                ["OverBudgetBrush"] = "#CD001D",
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
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        System.Windows.Application.Current.Resources[key] = brush;
    }
}
