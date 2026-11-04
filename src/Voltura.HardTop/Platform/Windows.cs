using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Voltura.HardTop.Platform;

// Ported from HardTop's EnumWindows/GetWindowText/SetWindowPos implementation.
// Pointer-sized handles, process identity and no-activation flags replace legacy assumptions.
public sealed class Windows : IWindows, IWindowSource
{
    private readonly Dictionary<(uint Pid, long Started), string> applicationNames = [];

    private string ApplicationName(Process process, WindowIdentity identity)
    {
        var key = (identity.ProcessId, identity.ProcessStarted);
        if (applicationNames.TryGetValue(key, out var cached))
            return cached;
        var name = process.ProcessName;
        try
        {
            var description = process.MainModule?.FileVersionInfo.FileDescription;
            if (!string.IsNullOrWhiteSpace(description))
                name = description.Trim();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException) { }
        applicationNames[key] = name;
        return name;
    }
    public static WindowIdentity? Identify(nint handle)
    {
        if (!IsWindow(handle))
            return null;
        GetWindowThreadProcessId(handle, out var pid);
        try
        {
            using var process = Process.GetProcessById((int)pid);
            return new(handle, pid, process.StartTime.ToUniversalTime().Ticks);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return null; }
    }
    public bool IsCurrent(WindowIdentity identity) => Identify(identity.Handle) == identity;
    public bool IsTopmost(WindowIdentity identity) => IsCurrent(identity) && (GetWindowLongPtr(identity.Handle, -20).ToInt64() & 8) != 0;
    public bool SetTopmost(WindowIdentity identity, bool topmost) => IsCurrent(identity) && SetWindowPos(identity.Handle, topmost ? -1 : -2, 0, 0, 0, 0, 0x0010 | 0x0001 | 0x0002 | 0x0200);
    public static nint Foreground => GetForegroundWindow();
    public IReadOnlyList<DesktopWindow> Enumerate()
    {
        var result = new List<DesktopWindow>();
        EnumWindows((handle, _) =>
        {
            if (!IsWindowVisible(handle) || GetWindow(handle, 4) != 0 || (GetWindowLongPtr(handle, -20).ToInt64() & 0x80) != 0)
                return true;
            var title = new StringBuilder(2048);
            GetWindowText(handle, title, title.Capacity);
            if (title.Length == 0)
                return true;
            if (DwmGetWindowAttribute(handle, 14, out var cloaked, sizeof(int)) == 0 && cloaked != 0)
                return true;
            var identity = Identify(handle);
            if (identity is not { } id || id.ProcessId == Environment.ProcessId)
                return true;
            try
            {
                using var process = Process.GetProcessById((int)id.ProcessId);
                ImageSource? icon = null;
                // Class icon lookup never sends synchronous messages to another application.
                var iconHandle = GetClassLongPtr(handle, -14);
                if (iconHandle == 0)
                    iconHandle = GetClassLongPtr(handle, -34);
                if (iconHandle != 0)
                {
                    try
                    {
                        var image = Imaging.CreateBitmapSourceFromHIcon(iconHandle, System.Windows.Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32));
                        image.Freeze();
                        icon = image;
                    }
                    catch (ArgumentException) { }
                }
                result.Add(new(id, title.ToString(), ApplicationName(process, id), IsTopmost(id), icon));
            }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
            return true;
        }, 0);
        var active = result.Select(window => (window.Identity.ProcessId, window.Identity.ProcessStarted)).ToHashSet();
        foreach (var key in applicationNames.Keys.Where(key => !active.Contains(key)).ToArray())
            applicationNames.Remove(key);
        return result.OrderBy(x => x.Title, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }
    private delegate bool EnumProc(nint handle, nint parameter);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, nint parameter);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint handle);
    [DllImport("user32.dll")] private static extern nint GetWindow(nint handle, uint command);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern nint GetWindowLongPtr(nint handle, int index);
    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")] private static extern nint GetClassLongPtr(nint handle, int index);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint handle, out uint pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(nint handle, StringBuilder text, int maximum);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint handle, int attribute, out int value, int size);
}

