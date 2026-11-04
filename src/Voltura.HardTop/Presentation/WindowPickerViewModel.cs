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
    private bool refreshing;

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

    public async Task RefreshAsync()
    {
        if (refreshing)
            return;
        refreshing = true;

        try
        {
            var latest = await Task.Run(source.Enumerate);
            // Keep surviving rows in place when titles or native z-order change.
            var byIdentity = latest.ToDictionary(window => window.Identity);
            windows = windows.Where(window => byIdentity.ContainsKey(window.Identity))
                .Select(window => byIdentity[window.Identity])
                .Concat(latest.Where(window => !windows.Any(old => old.Identity == window.Identity)))
                .ToArray();
            Filter();
        }
        finally
        {
            refreshing = false;
        }
    }

    public async Task<bool> ToggleAsync(WindowIdentity identity)
    {
        var changed = pins.Toggle(identity);
        await RefreshAsync();
        return changed;
    }

    public void RefreshLabels() => Filter();

    private void Filter()
    {
        var query = searchText.Trim();
        var matching = windows
            .Where(window => window.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase)
                || window.Application.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            .ToArray();
        var countsChanged = MatchingCount != matching.Length || MatchingPinnedCount != matching.Count(window => window.Pinned);
        MatchingCount = matching.Length;
        MatchingPinnedCount = matching.Count(window => window.Pinned);
        var next = matching.Where(window => !pinnedOnly || window.Pinned)
            .Select(window => new WindowRow(window, Strings.Current[window.Pinned ? "Unpin" : "Pin"]))
            .ToArray();

        var previousState = rows.Select(row => (row.Window.Identity, row.Window.Title, row.Window.Application, row.Window.Pinned, row.ActionLabel));
        var nextState = next.Select(row => (row.Window.Identity, row.Window.Title, row.Window.Application, row.Window.Pinned, row.ActionLabel));

        if (previousState.SequenceEqual(nextState) && !countsChanged)
            return;
        rows = next;
        PropertyChanged?.Invoke(this, new(nameof(Rows)));
    }
}
