using System.ComponentModel;
using System.Runtime.InteropServices;

namespace Voltura.HardTop.Platform;

// Consume capture keystrokes before Windows dispatches registered global shortcuts.
// The hook lives only while the capture field owns focus, including key releases.
internal sealed class ShortcutCapture : IDisposable
{
    private readonly Hook callback;
    private readonly Action<HotkeyGesture?> completed;
    private readonly nint owner;
    private nint handle;
    private uint modifiers;
    private bool finishing;
    private readonly HashSet<uint> held = [];
    private readonly Action<Action> defer;

    internal ShortcutCapture(nint owner, Action<HotkeyGesture?> completed, Action<Action> defer)
    {
        this.owner = owner;
        this.completed = completed;
        this.defer = defer;
        callback = OnKeyboard;
        handle = SetWindowsHookEx(13, callback, GetModuleHandle(null), 0);
        if (handle == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    private nint OnKeyboard(int code, nint message, nint data)
    {
        if (code < 0 || GetForegroundWindow() != owner)
            return CallNextHookEx(handle, code, message, data);
        var key = unchecked((uint)Marshal.ReadInt32(data));
        var down = message is 0x100 or 0x104;
        if (down)
            held.Add(key);
        else
            held.Remove(key);
        var modifier = key switch
        {
            0x10 or 0xA0 or 0xA1 => 4u,
            0x11 or 0xA2 or 0xA3 => 2u,
            0x12 or 0xA4 or 0xA5 => 1u,
            0x5B or 0x5C => 8u,
            _ => 0u
        };
        if (modifier != 0)
            modifiers = down ? modifiers | modifier : modifiers & ~modifier;
        if (finishing)
        {
            if (held.Count == 0)
                defer(Dispose);
            return 1;
        }
        if (!down || modifier != 0)
            return 1;
        if (key is 0x1B or 0x09)
        {
            finishing = true;
            completed(null);
            return 1;
        }
        if (modifiers == 0)
            return 1;
        finishing = true;
        completed(new HotkeyGesture(modifiers, key));
        return 1;
    }
    public void Dispose()
    {
        if (handle != 0)
            UnhookWindowsHookEx(handle);
        handle = 0;
        GC.KeepAlive(callback);
    }
    private delegate nint Hook(int code, nint message, nint data);
    [DllImport("user32.dll", SetLastError = true)] private static extern nint SetWindowsHookEx(int id, Hook callback, nint module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(nint hook);
    [DllImport("user32.dll")] private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern nint GetModuleHandle(string? name);
}
