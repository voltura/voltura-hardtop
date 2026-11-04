using System.Windows;

namespace Voltura.HardTop.Platform;

internal static class WindowTransparency
{
    internal static void Apply(Window window, int percent)
    {
        if (!window.AllowsTransparency)
        {
            throw new InvalidOperationException("Window transparency requires a transparent WPF host.");
        }

        window.Opacity = Math.Clamp(percent, 60, 100) / 100.0;
    }
}
