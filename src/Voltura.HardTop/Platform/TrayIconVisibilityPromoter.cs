using System.Windows.Threading;
using Microsoft.Win32;

namespace Voltura.HardTop.Platform;

// Registry matching follows Voltura Air's TrayIconVisibilityPromoter.
internal sealed class TrayIconVisibilityPromoter : IDisposable
{
    private const string NotifyIconSettingsSubKey = @"Control Panel\NotifyIconSettings";
    private readonly DispatcherTimer _timer;
    private readonly Action _refreshIcon;
    private readonly TryPromote _promote;
    private readonly TrayIconPlacement placement = new(new RegistryTrayIconPlacementStore(), Environment.ProcessPath);
    private int settling;
    private bool _visibilityFinished;
    private int _attempts;
    private bool _disposed;

    internal TrayIconVisibilityPromoter(
        Dispatcher dispatcher,
        Action refreshIcon,
        TryPromote? promote = null
    )
    {
        _refreshIcon = refreshIcon;
        _promote = promote ?? TryPromoteCurrentProcess;
        _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(250),
        };

        _timer.Tick += OnTick;
    }

    internal void Start()
    {
        if (_disposed)
        {
            return;
        }

        _attempts = 0;
        _visibilityFinished = false;
        settling = 0;
        placement.Start();
        _timer.Start();
    }

    private void OnTick(object? sender, EventArgs args)
    {
        if (Advance())
        {
            _timer.Stop();
        }
    }

    internal bool Advance()
    {
        _attempts++;

        if (!_visibilityFinished && _promote(out var changed))
        {
            _visibilityFinished = true;

            if (changed)
            {
                _refreshIcon();
            }
        }

        if (_visibilityFinished && placement.TryPlace() && ++settling >= 8)
        {
            return true;
        }

        // Explorer can create or rewrite its entry after the first icon refresh.
        if (_visibilityFinished && settling < 8 && _promote(out var rewritten) && rewritten)
            _refreshIcon();

        if (_attempts >= 120)
        {
            placement.Stop();
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        placement.Stop();
        _timer.Tick -= OnTick;
    }

    private static bool TryPromoteCurrentProcess(out bool changed)
    {
        changed = false;

        if (Environment.ProcessPath is not { Length: > 0 } executablePath)
        {
            return true;
        }

        try
        {
            using var root = Registry.CurrentUser.OpenSubKey(
                NotifyIconSettingsSubKey,
                writable: true
            );

            if (root is null)
            {
                return false;
            }

            return TryPromoteEntries(root, executablePath, out changed);
        }
        catch (Exception error)
            when (error
                    is IOException
                        or UnauthorizedAccessException
                        or System.Security.SecurityException
            )
        {
            return true;
        }
    }

    internal static bool TryPromoteEntries(
        RegistryKey root,
        string executablePath,
        out bool changed
    )
    {
        changed = false;

        var matchedEntry = false;
        var normalizedExecutablePath = NormalizePath(executablePath);

        foreach (var subKeyName in root.GetSubKeyNames())
        {
            using var entry = root.OpenSubKey(subKeyName, writable: true);
            var entryExecutablePath = entry?.GetValue("ExecutablePath") as string;

            if (!PathsEqual(normalizedExecutablePath, entryExecutablePath))
            {
                continue;
            }

            matchedEntry = true;

            if (!Equals(entry!.GetValue("IsPromoted"), 1))
            {
                entry.SetValue("IsPromoted", 1, RegistryValueKind.DWord);
                changed = true;
            }
        }

        return matchedEntry;
    }

    internal delegate bool TryPromote(out bool changed);

    internal static bool PathsEqual(string path, string? candidate)
    {
        return candidate is { Length: > 0 }
            && string.Equals(NormalizePath(path), NormalizePath(candidate), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return path;
        }
        catch (NotSupportedException)
        {
            return path;
        }
        catch (PathTooLongException)
        {
            return path;
        }
    }
}
