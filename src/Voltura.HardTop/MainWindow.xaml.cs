using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;
using Voltura.HardTop.Presentation;
using Voltura.HardTop.Updates;

namespace Voltura.HardTop;

// View adapter: layout, focus, window chrome and forwarding UI events to presentation models.
public partial class MainWindow : Window
{
    private readonly App application;
    private readonly WindowPickerViewModel picker;
    private readonly PreferencesViewModel preferences;
    private readonly DispatcherTimer refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly DispatcherTimer statusTimeout = new() { Interval = TimeSpan.FromSeconds(6) };
    private bool loading = true;

    public MainWindow(App app) : this(app,
        new WindowPickerViewModel(app.Desktop, app.Pins),
        new PreferencesViewModel(app.Settings, new StartupService(), app.Hotkeys, app.ShowPicker, app.ToggleForeground))
    {
    }

    internal MainWindow(App app, WindowPickerViewModel picker, PreferencesViewModel preferences)
    {
        application = app;
        this.picker = picker;
        this.preferences = preferences;
        InitializeComponent();
        preferences.Feedback += ShowStatus;
        app.ApplyTheme();
        SourceInitialized += (_, _) => ApplyOpacity();
        WindowWorkAreaPlacement.ConstrainAndCenterOnFirstLoad(this);
        WindowWorkAreaPlacement.KeepVisibleAfterDisplayChanges(this);
        TooltipLifetime.Attach(this);

        RefreshPreferenceLabels();
        OpacityChoice.Value = preferences.Value.OpacityPercent;
        ApplyOpacity();
        StartupChoice.IsChecked = preferences.StartWithWindows;
        StartupChoice.IsEnabled = preferences.StartupAvailable;
        AutomaticChoice.IsChecked = preferences.Value.AutomaticUpdates;
        AutomaticChoice.Visibility = app.Updates.Eligible ? Visibility.Visible : Visibility.Collapsed;
        ManualUpdateNotice.Visibility = app.Updates.Eligible ? Visibility.Collapsed : Visibility.Visible;
        PickerKeys.Text = preferences.Value.PickerShortcut;
        ToggleKeys.Text = preferences.Value.ToggleShortcut;
        RefreshVersion();
        Intro.Visibility = preferences.Value.Introduced ? Visibility.Collapsed : Visibility.Visible;

        picker.PropertyChanged += (_, _) => RenderRows();
        preferences.Changed += PreferenceChanged;
        PickerKeys.TextChanged += RefreshShortcutButtons;
        ToggleKeys.TextChanged += RefreshShortcutButtons;
        statusTimeout.Tick += (_, _) => { Status.Visibility = Visibility.Collapsed; statusTimeout.Stop(); };
        refresh.Tick += (_, _) => RefreshWindows();
        IsVisibleChanged += (_, _) =>
        {
            UpdateRefreshSchedule();
            RefreshWindows();
        };
        StateChanged += (_, _) => { UpdateRefreshSchedule(); RefreshWindows(); };
        Closing += (_, args) =>
        {
            if (app.ExitAllowed)
                return;
            args.Cancel = true;
            Hide();
        };
        PreviewKeyDown += (_, args) =>
        {
            if (args.Key != Key.Escape)
                return;
            Hide();
            args.Handled = true;
        };
        loading = false;
        RenderRows();
        RefreshUpdate();
    }

    private void RefreshPreferenceLabels()
    {
        var wasLoading = loading;
        loading = true;
        LanguageChoice.ItemsSource = new[] { new LanguageDefinition("system", Strings.Current["FollowWindows"], "en-GB") }.Concat(LanguageCatalog.All).ToArray();
        LanguageChoice.SelectedValue = preferences.Value.Language;
        ThemeChoice.ItemsSource = new[] { ("system", "FollowWindows"), ("light", "Light"), ("dark", "Dark") }
            .Select(item => new { Id = item.Item1, Label = Strings.Current[item.Item2] }).ToArray();
        ThemeChoice.SelectedValue = preferences.Value.Theme;
        var longest = new[] { "Pin", "Unpin" }.Select(key =>
        {
            var text = new TextBlock { Text = Strings.Current[key], FontSize = (double)FindResource("BodySize") };
            text.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            return text.DesiredSize.Width;
        }).Max();
        Resources["PinButtonWidth"] = Math.Max(96, longest + 48);
        loading = wasLoading;
    }

    private void PreferenceChanged(PreferenceChange change)
    {
        if (change.HasFlag(PreferenceChange.Language))
        {
            Strings.Current.SetLanguage(preferences.Value.Language);
            loading = true;
            RefreshPreferenceLabels();
            loading = false;
            picker.RefreshLabels();
            RenderRows(); // Empty or unchanged rows still need localized counts and empty text.
            application.RebuildTray();
            RefreshUpdate();
            ApplyOpacity();
            RefreshVersion();
        }
        if (change.HasFlag(PreferenceChange.Appearance))
        {
            application.ApplyTheme();
            RefreshPreferenceLabels();
        }
        if (change.HasFlag(PreferenceChange.Opacity))
            ApplyOpacity();
        if (change.HasFlag(PreferenceChange.Updates))
            application.Updates.Start(preferences.Value.AutomaticUpdates);
        RefreshShortcutButtons();
    }

    public async void RefreshWindows()
    {
        if (!IsVisible || WindowState == WindowState.Minimized || PickerPanel.Visibility != Visibility.Visible)
            return;
        try
        {
            await picker.RefreshAsync();
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ShowStatus("ActionFailed");
        }
    }

    private void UpdateRefreshSchedule()
    {
        if (IsVisible && WindowState != WindowState.Minimized && PickerPanel.Visibility == Visibility.Visible)
            refresh.Start();
        else
            refresh.Stop();
    }

    private void RenderRows()
    {
        var selected = (WindowList.SelectedItem as WindowRow)?.Window.Identity;
        var scroll = FindVisual<ScrollViewer>(WindowList);
        var offset = scroll?.VerticalOffset ?? 0;
        WindowList.ItemsSource = picker.Rows;
        WindowList.SelectedItem = picker.Rows.FirstOrDefault(row => row.Window.Identity == selected);
        WindowList.UpdateLayout();
        scroll?.ScrollToVerticalOffset(offset);
        Empty.Visibility = picker.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        Empty.Text = Strings.Current[picker.PinnedOnly ? "NoPinnedWindows" : "NoWindows"];
        AllButton.Content = $"{Strings.Current["AllWindows"]} ({picker.MatchingCount.ToString(Strings.Current.Culture)})";
        PinnedButton.Content = $"{Strings.Current["PinnedWindows"]} ({picker.MatchingPinnedCount.ToString(Strings.Current.Culture)})";
        AllButton.IsChecked = !picker.PinnedOnly;
        PinnedButton.IsChecked = picker.PinnedOnly;
    }

    private static T? FindVisual<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T match)
            return match;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            if (FindVisual<T>(VisualTreeHelper.GetChild(node, i)) is { } child)
                return child;
        return null;
    }

    internal void ShowStatus(string key)
    {
        Status.Text = Strings.Current[key];
        Status.Visibility = Visibility.Visible;
        statusTimeout.Stop();
        statusTimeout.Start();
    }

    private void RefreshVersion() => VersionLabel.Text = string.Format(Strings.Current.Culture,
        Strings.Current["VersionFormat"], Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)) + " · MIT · © 2026 Voltura AB";

    internal void RaisePicker()
    {
        Topmost = false;
        Topmost = true;
        ApplyOpacity();
    }

    public void ShowWindows()
    {
        PickerPanel.Visibility = Visibility.Visible;
        SettingsPanel.Visibility = Visibility.Collapsed;
        PreferencesTitle.Visibility = BackButton.Visibility = Visibility.Collapsed;
        PreferencesButton.Visibility = Visibility.Visible;
        Intro.Visibility = preferences.Value.Introduced ? Visibility.Collapsed : Visibility.Visible;
        UpdateRefreshSchedule();
        RefreshWindows();
        Search.Focus();
    }

    public void ShowSettings()
    {
        PickerPanel.Visibility = Visibility.Collapsed;
        SettingsPanel.Visibility = Visibility.Visible;
        PreferencesTitle.Visibility = BackButton.Visibility = Visibility.Visible;
        PreferencesButton.Visibility = Intro.Visibility = Visibility.Collapsed;
        UpdateRefreshSchedule();
        RefreshUpdate();
    }

    private async void Toggle(WindowRow row)
    {
        try
        {
            ShowStatus(await picker.ToggleAsync(row.Window.Identity) ? "PinChanged" : "PinFailed");
        }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ShowStatus("ActionFailed");
        }
        RaisePicker();
    }

    private void ApplyOpacity()
    {
        WindowTransparency.Apply(this, preferences.Value.OpacityPercent);
        OpacityValue.Text = (preferences.Value.OpacityPercent / 100.0).ToString("P0", Strings.Current.Culture);
    }

    private void SettingsClick(object sender, RoutedEventArgs e) => ShowSettings();
    private void BackClick(object sender, RoutedEventArgs e) => ShowWindows();
    private void IntroClick(object sender, RoutedEventArgs e)
    {
        preferences.CompleteIntroduction();
        Intro.Visibility = Visibility.Collapsed;
    }
    private void SearchChanged(object sender, TextChangedEventArgs e)
    {
        if (!loading)
            picker.SearchText = Search.Text;
    }
    private void ClearSearchClick(object sender, RoutedEventArgs e)
    {
        Search.Clear();
        Search.Focus();
    }
    private void AllClick(object sender, RoutedEventArgs e)
    {
        picker.PinnedOnly = false;
        RenderRows();
    }
    private void PinnedClick(object sender, RoutedEventArgs e)
    {
        picker.PinnedOnly = true;
        RenderRows();
    }
    private void PinClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (((Button)sender).DataContext is WindowRow row)
            Toggle(row);
    }

    private void WindowDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source || FindButton(source))
            return;
        if (ItemsControl.ContainerFromElement(WindowList, source) is ListBoxItem { DataContext: WindowRow row })
            Toggle(row);
    }

    private static bool FindButton(DependencyObject source)
    {
        for (var node = source; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            if (node is Button)
                return true;
        }
        return false;
    }

    private void WindowKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Space) || WindowList.SelectedItem is not WindowRow row)
            return;
        e.Handled = true;
        Toggle(row);
    }

    private void LanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && LanguageChoice.SelectedValue is string value)
            preferences.SetLanguage(value);
    }
    private void ThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!loading && ThemeChoice.SelectedValue is string value)
            preferences.SetTheme(value);
    }
    private void OpacityChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!loading)
            preferences.SetOpacity((int)Math.Round(OpacityChoice.Value));
    }
    private void StartupChanged(object sender, RoutedEventArgs e)
    {
        preferences.SetStartup(StartupChoice.IsChecked == true);
        StartupChoice.IsChecked = preferences.StartWithWindows;
        StartupChoice.IsEnabled = preferences.StartupAvailable;
    }
    private void RefreshShortcutButtons()
    {
        ApplyPickerButton.IsEnabled = !PickerKeys.IsCapturing && (preferences.HasUnsavedChanges || ShortcutDiffers(PickerKeys.Text, preferences.Value.PickerShortcut));
        ApplyToggleButton.IsEnabled = !ToggleKeys.IsCapturing && (preferences.HasUnsavedChanges || ShortcutDiffers(ToggleKeys.Text, preferences.Value.ToggleShortcut));
        PickerError.Visibility = ToggleError.Visibility = Visibility.Collapsed;
    }
    private static bool ShortcutDiffers(string candidate, string saved) =>
        !HotkeyGesture.TryParse(candidate, out var next) || !HotkeyGesture.TryParse(saved, out var previous) || next != previous;
    private void ApplyPicker(object sender, RoutedEventArgs e)
    {
        var result = preferences.ApplyPickerShortcut(PickerKeys.Text);
        RefreshShortcutButtons();
        PickerError.Text = Strings.Current[result == ShortcutApplyResult.RegistrationFailed ? "ShortcutInUse" : "SaveFailed"];
        PickerError.Visibility = result == ShortcutApplyResult.Applied ? Visibility.Collapsed : Visibility.Visible;
    }
    private void ApplyToggle(object sender, RoutedEventArgs e)
    {
        var result = preferences.ApplyToggleShortcut(ToggleKeys.Text);
        RefreshShortcutButtons();
        ToggleError.Text = Strings.Current[result == ShortcutApplyResult.RegistrationFailed ? "ShortcutInUse" : "SaveFailed"];
        ToggleError.Visibility = result == ShortcutApplyResult.Applied ? Visibility.Collapsed : Visibility.Visible;
    }
    private void AutomaticChanged(object sender, RoutedEventArgs e) => preferences.SetAutomaticUpdates(AutomaticChoice.IsChecked == true);

    private void TitlebarMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindButton(source))
            return;
        if (e.ClickCount == 2)
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        else if (e.LeftButton == MouseButtonState.Pressed)
            DragMove();
    }
    private void MinimizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void MaximizeClick(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void HideClick(object sender, RoutedEventArgs e) => Hide();

    internal void RefreshUpdate()
    {
        var state = application.Updates.State;
        var key = UpdatePresentation.StatusKey(state.Status);
        UpdateLabel.Text = Strings.Current[key];
        CheckUpdateButton.IsEnabled = !state.Busy;
        PrivacyLabel.Text = Strings.Current[application.Updates.Eligible ? "PrivacyNote" : "ManualPrivacyNote"];
        DownloadPageButton.Visibility = !application.Updates.Eligible || state.Status == UpdateStatus.Available ? Visibility.Visible : Visibility.Collapsed;
        UpdateLabel.Visibility = state.Status == UpdateStatus.ManualUpdates ? Visibility.Collapsed : Visibility.Visible;
        InstallButton.Visibility = state.Ready ? Visibility.Visible : Visibility.Collapsed;
        CancelUpdateButton.Visibility = state.Status is UpdateStatus.Checking or UpdateStatus.Downloading ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Visibility = state.Status == UpdateStatus.Downloading ? Visibility.Visible : Visibility.Collapsed;
        UpdateProgress.Value = application.Updates.Progress;
    }

    private async void CheckUpdates(object sender, RoutedEventArgs e)
    {
        await application.Updates.CheckAsync();
    }
    private void DownloadPageClick(object sender, RoutedEventArgs e) => App.Open(UpdateService.ProjectUrl + "/releases/latest");
    private void CancelUpdate(object sender, RoutedEventArgs e) => application.Updates.CancelCheck();
    private async void InstallUpdate(object sender, RoutedEventArgs e)
    {
        if (ConfirmWindow.Ask(this, Strings.Current["InstallPrompt"], Strings.Current["InstallAndRestart"]))
            await application.Updates.InstallAsync();
    }
    private void WebsiteClick(object sender, RoutedEventArgs e) => App.Open("https://hardtop.voltura.se/");
    private void HelpClick(object sender, RoutedEventArgs e) => App.Open(UpdateService.ProjectUrl + "/issues");
    private void LicenseClick(object sender, RoutedEventArgs e) => App.Open(UpdateService.ProjectUrl + "/blob/main/LICENSE");
    private void SponsorClick(object sender, RoutedEventArgs e) => application.ShowSupport();
}
