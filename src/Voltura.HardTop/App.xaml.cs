using System.Diagnostics;
using System.Windows;
using Microsoft.Win32;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;
using Voltura.HardTop.Updates;

namespace Voltura.HardTop;

// Composition root and application lifetime only. Views do not build native services.
public partial class App : Application
{
    internal SettingsStore Settings { get; private set; } = null!;
    internal Platform.Windows Desktop { get; } = new();
    internal PinSession Pins { get; private set; } = null!;
    internal Hotkeys Hotkeys { get; private set; } = null!;
    internal UpdateService Updates { get; private set; } = null!;
    internal bool ExitAllowed
    {
        get; private set;
    }

    private SingleInstance? instance;
    private TrayService? tray;
    private MainWindow? picker;
    private ThemeService? theme;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        instance = new SingleInstance();
        if (!instance.IsPrimary)
        {
            instance.RequestActivation();
            Shutdown();
            return;
        }

        Settings = new SettingsStore();
        Strings.Current.SetLanguage(Settings.Value.Language);
        theme = new ThemeService(this);
        ApplyTheme();
        SystemEvents.UserPreferenceChanged += SystemThemeChanged;
        Pins = new PinSession(Desktop);
        Hotkeys = new Hotkeys();
        var pickerRegistered = Hotkeys.Replace("picker", Settings.Value.PickerShortcut, ShowPicker);
        var toggleRegistered = Hotkeys.Replace("toggle", Settings.Value.ToggleShortcut, ToggleForeground);

        Updates = new UpdateService(
            new AppPaths(Settings.DirectoryPath, Settings.Portable, false),
            install: (path, token) => Dispatcher.InvokeAsync(() => InstallPreparedUpdate(path, token)).Task);
        tray = new TrayService(Dispatcher, new TrayActions(
            ShowPicker,
            ShowPreferences,
            () => { ShowPreferences(); _ = Updates.CheckAsync(); },
            ShowPreferences,
            ShowSupport,
            RequestExit));
        RebuildTray();

        Hotkeys.TaskbarRecreated += tray.RestartPromotion;
        instance.Listen(() => Dispatcher.BeginInvoke(ShowPicker));
        instance.ListenForExit(() => Dispatcher.BeginInvoke(ExitApplication));
        SessionEnding += (_, _) => ExitApplication();
        Updates.Changed += () => Dispatcher.BeginInvoke(() =>
        {
            if (ExitAllowed)
                return;
            picker?.RefreshUpdate();
            RebuildTray();
        });
        Updates.Start(Settings.Value.AutomaticUpdates);

        if (!Settings.Value.Introduced || !e.Args.Contains("--startup"))
            ShowPicker();
        if (!pickerRegistered || !toggleRegistered)
        {
            ShowPicker();
            picker?.ShowStatus("ShortcutConflict");
        }
        if (Settings.Recovered)
        {
            ShowPicker();
            picker?.ShowStatus("SettingsRecovered");
        }
    }

    internal void ShowPicker()
    {
        if (ExitAllowed)
            return;
        picker ??= new MainWindow(this);
        picker.RaisePicker();
        picker.Show();
        if (picker.WindowState == WindowState.Minimized)
            picker.WindowState = WindowState.Normal;
        WindowWorkAreaPlacement.EnsureVisibleOnCurrentMonitor(picker);
        picker.Activate();
        picker.ShowWindows();
    }

    private void ShowPreferences()
    {
        ShowPicker();
        picker?.ShowSettings();
    }

    internal void ShowSupport()
    {
        ShowPicker();
        new SupportWindow(picker!).ShowDialog();
    }

    internal void ToggleForeground()
    {
        var target = Platform.Windows.Identify(Platform.Windows.Foreground);
        if (target is not { } identity || identity.ProcessId == Environment.ProcessId || !Pins.Toggle(identity))
        {
            tray?.Warn("PinFailed");
        }
        picker?.RefreshWindows();
    }

    internal void ApplyTheme()
    {
        theme ??= new ThemeService(this);
        theme.Apply(Settings.Value.Theme);
        RebuildTray();
    }

    internal void RebuildTray() => tray?.Rebuild(theme?.IsDark == true, Updates?.State.Ready == true);
    private void SystemThemeChanged(object sender, UserPreferenceChangedEventArgs e) => Dispatcher.BeginInvoke(ApplyTheme);

    internal static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });

    internal void RequestExit()
    {
        if (picker?.IsVisible != true || picker.WindowState == WindowState.Minimized)
            ShowPicker();
        if (ConfirmWindow.Ask(picker!, Strings.Current["ExitPrompt"], Strings.Current["ExitApp"], Strings.Current["ExitHeading"]))
            ExitApplication();
    }

    private void InstallPreparedUpdate(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Pins.Restore();
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        ExitApplication();
    }

    internal async void ExitApplication()
    {
        if (ExitAllowed)
            return;
        ExitAllowed = true;
        Pins.Restore();
        Hotkeys.Dispose();
        tray?.Dispose();
        SystemEvents.UserPreferenceChanged -= SystemThemeChanged;
        instance?.Dispose();
        await Updates.DisposeAsync();
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (!ExitAllowed && instance?.IsPrimary == true)
            Pins?.Restore();
        instance?.Dispose();
        base.OnExit(e);
    }
}
