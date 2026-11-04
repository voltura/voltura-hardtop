using System.Runtime.InteropServices;
using System.Windows.Input;

namespace Voltura.HardTop.Platform;

public readonly record struct HotkeyGesture(uint Modifiers, uint Key)
{
    public static bool TryParse(string? text, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        if (parts.Length < 2)
            return false;
        uint modifiers = 0;
        foreach (var part in parts[..^1])
        {
            uint value = part.ToLowerInvariant() switch
            {
                "ctrl" or "control" => 2,
                "alt" => 1,
                "shift" => 4,
                "win" => 8,
                _ => 0
            };
            if (value == 0 || (modifiers & value) != 0)
                return false;
            modifiers |= value;
        }
        if (!Enum.TryParse<Key>(parts[^1], true, out var key) || !Enum.IsDefined(key) || key == System.Windows.Input.Key.None)
            return false;
        var code = KeyInterop.VirtualKeyFromKey(key);
        if (code == 0 || key is System.Windows.Input.Key.LeftAlt or System.Windows.Input.Key.RightAlt or System.Windows.Input.Key.LeftCtrl or System.Windows.Input.Key.RightCtrl or System.Windows.Input.Key.LeftShift or System.Windows.Input.Key.RightShift or System.Windows.Input.Key.LWin or System.Windows.Input.Key.RWin)
            return false;
        gesture = new(modifiers, (uint)code);
        return true;
    }
}

public interface IHotkeyRegistrar
{
    bool Replace(string slot, string text, Action action);
}

public sealed class Hotkeys : System.Windows.Forms.NativeWindow, IHotkeyRegistrar, IDisposable
{
    private int sequence;
    private readonly uint taskbarMessage = RegisterWindowMessage("TaskbarCreated");
    public event Action? TaskbarRecreated;
    private readonly Dictionary<int, Action> actions = [];
    private readonly Dictionary<string, (int Id, HotkeyGesture Gesture)> slots = [];
    public Hotkeys() => CreateHandle(new System.Windows.Forms.CreateParams());
    public bool Replace(string slot, string text, Action action)
    {
        if (!HotkeyGesture.TryParse(text, out var gesture))
            return false;
        if (slots.TryGetValue(slot, out var old) && old.Gesture == gesture)
            return true;
        if (slots.Any(x => x.Key != slot && x.Value.Gesture == gesture))
            return false;
        var id = ++sequence;
        // Register the new shortcut before releasing the old one.
        if (!RegisterHotKey(Handle, id, gesture.Modifiers | 0x4000, gesture.Key))
            return false;
        actions[id] = action;
        if (old.Id != 0)
        {
            UnregisterHotKey(Handle, old.Id);
            actions.Remove(old.Id);
        }
        slots[slot] = (id, gesture);
        return true;
    }
    protected override void WndProc(ref System.Windows.Forms.Message message)
    {
        if (message.Msg == 0x0312 && actions.TryGetValue((int)message.WParam, out var action))
            action();
        if (message.Msg == taskbarMessage)
            TaskbarRecreated?.Invoke();
        base.WndProc(ref message);
    }
    public void Dispose()
    {
        foreach (var id in actions.Keys)
            UnregisterHotKey(Handle, id);
        actions.Clear();
        DestroyHandle();
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(nint window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(nint window, int id);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern uint RegisterWindowMessage(string name);
}
