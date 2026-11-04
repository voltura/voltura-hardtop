using System.Windows.Media;

namespace Voltura.HardTop.Platform;

public readonly record struct WindowIdentity(nint Handle, uint ProcessId, long ProcessStarted);
public sealed record DesktopWindow(WindowIdentity Identity, string Title, string Application, bool Pinned, ImageSource? Icon = null);
public interface IWindows
{
    bool IsCurrent(WindowIdentity identity);
    bool IsTopmost(WindowIdentity identity);
    bool SetTopmost(WindowIdentity identity, bool topmost);
}
public interface IWindowSource
{
    IReadOnlyList<DesktopWindow> Enumerate();
}

