using System.ComponentModel;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;

namespace Voltura.HardTop.Presentation;

public sealed record WindowRow(DesktopWindow Window, string ActionLabel)
{
    public string AccessibleAction => $"{ActionLabel}: {Window.Title}";
}

public sealed class WindowPickerViewModel(IWindowSource source, PinSession pins) : INotifyPropertyChanged
{
    private IReadOnlyList<DesktopWindow> windows = [];
    private IReadOnlyList<WindowRow> rows = [];
    private string searchText = string.Empty;
    private bool pinnedOnly;
    private Task? refreshTask;

    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<WindowRow> Rows => rows;
    public int MatchingCount
    {
        get; private set;
    }
    public int MatchingPinnedCount
    {
        get; private set;
    }

    public string SearchText
    {
        get => searchText;
        set
        {
            if (searchText == value)
                return;
            searchText = value;
            Filter();
        }
    }

    public bool PinnedOnly
    {
        get => pinnedOnly;
        set
        {
            if (pinnedOnly == value)
                return;
            pinnedOnly = value;
            Filter();
        }
    }

    public Task RefreshAsync()
    {
        if (refreshTask is { IsCompleted: false })
            return refreshTask;
        return refreshTask = RefreshCoreAsync();
    }

    private async Task RefreshCoreAsync()
    {
        var latest = await Task.Run(source.Enumerate);
        // Keep surviving rows in place when titles or native z-order change.
        var byIdentity = latest.ToDictionary(window => window.Identity);
        var ordered = new List<DesktopWindow>(latest.Count);
        foreach (var previous in windows)
            if (byIdentity.Remove(previous.Identity, out var current))
                ordered.Add(current);
        foreach (var current in latest)
            if (byIdentity.Remove(current.Identity))
                ordered.Add(current);
        windows = ordered;
        Filter();
    }

    public async Task<bool> ToggleAsync(WindowIdentity identity)
    {
        var earlierRefresh = refreshTask;
        var changed = pins.Toggle(identity);
        // An older scan may already have captured the pre-toggle state. Wait for it
        // before requesting the scan that acknowledges this action.
        if (earlierRefresh is { IsCompleted: false })
            await earlierRefresh;
        await RefreshAsync();
        return changed;
    }

    public void RefreshLabels() => Filter();

    private void Filter()
    {
        var query = searchText.Trim();
        var next = new List<WindowRow>(windows.Count);
        var matchingCount = 0;
        var pinnedCount = 0;
        var changed = false;
        var pinLabel = Strings.Current["Pin"];
        var unpinLabel = Strings.Current["Unpin"];
        foreach (var window in windows)
        {
            if (!window.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                && !window.Application.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                continue;
            matchingCount++;
            if (window.Pinned)
                pinnedCount++;
            if (pinnedOnly && !window.Pinned)
                continue;
            var label = window.Pinned ? unpinLabel : pinLabel;
            var previous = next.Count < rows.Count ? rows[next.Count] : null;
            if (previous is not null && previous.Window == window && previous.ActionLabel == label)
                next.Add(previous);
            else
            {
                next.Add(new(window, label));
                changed = true;
            }
        }
        var countsChanged = MatchingCount != matchingCount || MatchingPinnedCount != pinnedCount;
        MatchingCount = matchingCount;
        MatchingPinnedCount = pinnedCount;
        if (!changed && rows.Count == next.Count && !countsChanged)
            return;
        rows = next;
        PropertyChanged?.Invoke(this, new(nameof(Rows)));
    }
}
