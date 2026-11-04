using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Voltura.HardTop.Platform;

internal sealed class ThemeService(Application application)
{
    internal bool IsDark
    {
        get; private set;
    }

    internal void Apply(string choice)
    {
        using var accessibility = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Accessibility");
        var textScale = Math.Clamp((accessibility?.GetValue("TextScaleFactor") as int? ?? 100) / 100.0, 1, 2.25);
        application.Resources["BodySize"] = 14 * textScale;
        application.Resources["SecondarySize"] = 13 * textScale;
        application.Resources["HeadingSize"] = 18 * textScale;
        application.Resources["PageTitleSize"] = 20 * textScale;
#pragma warning disable WPF0001
        var mode = choice switch
        {
            "light" => ThemeMode.Light,
            "dark" => ThemeMode.Dark,
            _ => ThemeMode.System,
        };
        if (application.ThemeMode != mode)
            application.ThemeMode = mode;
#pragma warning restore WPF0001

        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        IsDark = choice == "dark" || choice == "system" && key?.GetValue("AppsUseLightTheme") is int value && value == 0;

        var colors = IsDark
            ? new[] { "#111827", "#1B2538", "#F1F5FC", "#AAB7CF", "#35435D", "#9BB1FF", "#18213A", "#263557" }
            : new[] { "#F5F7FC", "#FFFFFF", "#18213A", "#5D6A83", "#DCE2EE", "#3656E8", "#FFFFFF", "#EAF0FF" };
        var names = new[] { "BackgroundBrush", "SurfaceBrush", "TextBrush", "MutedBrush", "LineBrush", "AccentBrush", "AccentTextBrush", "AccentSoftBrush" };

        for (var i = 0; i < names.Length; i++)
        {
            var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
            brush.Freeze();
            application.Resources[names[i]] = brush;
        }
        application.Resources["ErrorBrush"] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(IsDark ? "#FFB4AB" : "#B42318"));

        if (!SystemParameters.HighContrast)
            return;
        application.Resources["BackgroundBrush"] = SystemColors.WindowBrush;
        application.Resources["SurfaceBrush"] = SystemColors.WindowBrush;
        application.Resources["TextBrush"] = SystemColors.WindowTextBrush;
        application.Resources["MutedBrush"] = SystemColors.WindowTextBrush;
        application.Resources["LineBrush"] = SystemColors.WindowTextBrush;
        application.Resources["AccentBrush"] = SystemColors.HighlightBrush;
        application.Resources["AccentTextBrush"] = SystemColors.HighlightTextBrush;
        application.Resources["AccentSoftBrush"] = SystemColors.ControlBrush;
        application.Resources["ErrorBrush"] = SystemColors.WindowTextBrush;
    }
}
