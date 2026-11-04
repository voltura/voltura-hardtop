namespace Voltura.HardTop.Platform;

public sealed class PinSession(IWindows windows)
{
    private readonly Dictionary<WindowIdentity, (bool Original, bool Last)> changes = [];
    public bool Toggle(WindowIdentity identity)
    {
        if (!windows.IsCurrent(identity))
            return false;
        var original = windows.IsTopmost(identity);
        if (!windows.SetTopmost(identity, !original) || windows.IsTopmost(identity) == original)
            return false;
        var baseline = changes.TryGetValue(identity, out var tracked) ? tracked.Original : original;
        if (!original == baseline)
            changes.Remove(identity);
        else
            changes[identity] = (baseline, !original);
        return true;
    }
    public int Restore()
    {
        var failures = 0;
        foreach (var (id, value) in changes.ToArray())
        {
            // Do not overwrite changes made independently by another application.
            if (windows.IsCurrent(id) && windows.IsTopmost(id) == value.Last && !windows.SetTopmost(id, value.Original))
                failures++;
        }
        changes.Clear();
        return failures;
    }
}
