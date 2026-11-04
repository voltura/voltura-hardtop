namespace Voltura.HardTop.Platform;

internal sealed class ShortcutKeyState
{
    private readonly HashSet<uint> held = [];
    internal bool AnyHeld => held.Count != 0;
    internal uint Modifiers
    {
        get
        {
            uint modifiers = 0;
            foreach (var key in held) modifiers |= ModifierFor(key);
            return modifiers;
        }
    }
    internal void Set(uint key, bool down)
    {
        if (down) held.Add(key);
        else held.Remove(key);
    }
    internal static uint ModifierFor(uint key) => key switch
    {
        0x10 or 0xA0 or 0xA1 => 4u,
        0x11 or 0xA2 or 0xA3 => 2u,
        0x12 or 0xA4 or 0xA5 => 1u,
        0x5B or 0x5C => 8u,
        _ => 0u,
    };
}
