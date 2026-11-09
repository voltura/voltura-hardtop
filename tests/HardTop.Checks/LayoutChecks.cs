using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Voltura.HardTop;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;
using Voltura.HardTop.Presentation;
using Voltura.HardTop.Updates;

namespace HardTop.Checks;

internal static class LayoutChecks
{
    internal static void Run()
    {
        // WPF queues Application startup in its constructor. Keep the layout host
        // isolated when a later regression check pumps the dispatcher.
        var startupOperations = new List<System.Windows.Threading.DispatcherOperation>();
        void CaptureStartup(object? sender, System.Windows.Threading.DispatcherHookEventArgs e) => startupOperations.Add(e.Operation);
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        dispatcher.Hooks.OperationPosted += CaptureStartup;
        App app;
        try { app = new App(); }
        finally { dispatcher.Hooks.OperationPosted -= CaptureStartup; }
        foreach (var operation in startupOperations) operation.Abort();
        app.InitializeComponent();
        var directory = Path.Combine(Path.GetTempPath(), "VolturaHardTop.Layout-" + Guid.NewGuid().ToString("N"));
        var settings = new SettingsStore(directory);
        settings.Value.Introduced = true;
        settings.Value.Theme = "dark";
        typeof(App).GetProperty("Settings", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, settings);
        var updates = new UpdateService(new(directory, true, true), eligible: false);
        typeof(App).GetProperty("Updates", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, updates);
        foreach (var mode in new[] { "light", "dark" })
        {
            new ThemeService(app).Apply(mode);
            foreach (var (foreground, background) in new[] { ("TextBrush", "SurfaceBrush"), ("MutedBrush", "SurfaceBrush"), ("MutedBrush", "AccentSoftBrush"), ("AccentTextBrush", "AccentBrush"), ("ErrorBrush", "SurfaceBrush") })
            {
                static double Luminance(Color color)
                {
                    static double Linear(byte channel) { var value = channel / 255.0; return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4); }
                    return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
                }
                var first = Luminance(((SolidColorBrush)app.Resources[foreground]).Color);
                var second = Luminance(((SolidColorBrush)app.Resources[background]).Color);
                if ((Math.Max(first, second) + .05) / (Math.Min(first, second) + .05) < 4.5) throw new Exception($"Insufficient text contrast: {mode}/{foreground}/{background}");
            }
        }
        Console.WriteLine("PASS light/dark normal-text palette contrast is at least 4.5:1.");
        var source = new SampleWindows();
        var picker = new WindowPickerViewModel(source, new PinSession(source));
        picker.RefreshAsync().GetAwaiter().GetResult();
        var window = new MainWindow(app, picker, new PreferencesViewModel(settings, new NoStartup(), new NoRegistration(), () => { }, () => { }));
        var content = (FrameworkElement)window.Content;
        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/layout"));
        Directory.CreateDirectory(output);
        void Layout(Size size) { content.Measure(size); content.Arrange(new Rect(size)); content.UpdateLayout(); }
        T Control<T>(string name) where T : class => (T)window.FindName(name);
        window.ShowSettings(); Layout(new(700, 760));
        var languages = Control<ComboBox>("LanguageChoice");
        var themes = Control<ComboBox>("ThemeChoice");
        if (!Descendants<TextBlock>(languages).Any(t => t.Text == "Follow Windows")) throw new Exception("Language picker displays backing object");
        if (!Descendants<TextBlock>(themes).Any(t => t.Text == "Dark")) throw new Exception("Theme label is incorrect");
        var checkbox = Control<CheckBox>("StartupChoice"); checkbox.ApplyTemplate();
        if (checkbox.Template.FindName("Box", checkbox) is not Border) throw new Exception("Checkbox is unthemed");
        if (Control<CheckBox>("AutomaticChoice").Visibility != Visibility.Collapsed || Control<TextBlock>("ManualUpdateNotice").Visibility != Visibility.Visible)
            throw new Exception("Manual build shows automatic update setting");
        if (!Control<Button>("CheckUpdateButton").IsEnabled) throw new Exception("Manual build cannot check for updates");
        if (Control<Button>("ApplyPickerButton").IsEnabled) throw new Exception("Unchanged shortcut enables Apply");
        Control<Voltura.HardTop.Ui.ShortcutEditor>("PickerKeys").Text = "Ctrl+Shift+F10";
        if (!Control<Button>("ApplyPickerButton").IsEnabled) throw new Exception("Changed shortcut does not enable Apply");
        if (Control<Button>("BackButton").IsDescendantOf(Control<ScrollViewer>("SettingsPanel"))) throw new Exception("Navigation scrolls with settings");
        Console.WriteLine("PASS navigation, manual update capability, shortcut change tracking, labels and checkbox.");
        languages.SelectedValue = "en";
        picker.SearchText = "No such window";
        languages.SelectedValue = "sv";
        if (!Control<RadioButton>("AllButton").Content.ToString()!.StartsWith(Strings.Current["AllWindows"], StringComparison.Ordinal)
            || Control<TextBlock>("Empty").Text != Strings.Current["NoWindows"])
            throw new Exception("Empty picker labels do not follow a language change");
        picker.SearchText = "";
        languages.SelectedValue = "en";
        var windowList = Control<ListBox>("WindowList");
        windowList.SelectedItem = picker.Rows[0];
        windowList.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
        {
            RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent,
            Source = windowList,
        });
        if (source.ToggleCalls != 0) throw new Exception("Double-clicking blank list space toggles the selected window");
        Console.WriteLine("PASS empty picker relocalizes and blank list double-click leaves pins unchanged.");
        foreach (var language in LanguageCatalog.All)
        {
            languages.SelectedValue = language.Id;
            foreach (var scale in new[] { 1.0, 1.5, 2.25 })
            {
                app.Resources["BodySize"] = 14 * scale; app.Resources["SecondarySize"] = 13 * scale;
                app.Resources["HeadingSize"] = 18 * scale; app.Resources["PageTitleSize"] = 20 * scale;
                foreach (var size in new[] { new Size(510, 540), new Size(700, 760) })
                {
                    window.ShowSettings(); Layout(size);
                    if (language.Id == "en" && scale == 2.25 && size.Width == 510) Save(content, output, "preferences-large-text-minimum.png", 1);
                    var scroll = Control<ScrollViewer>("SettingsPanel");
                    scroll.ScrollToEnd(); Layout(size);
                    var last = ((StackPanel)scroll.Content).Children[^1] as FrameworkElement;
                    var bottom = last!.TranslatePoint(new Point(0, last.ActualHeight), scroll).Y;
                    if (bottom > scroll.ActualHeight + 1) throw new Exception($"Final preferences card unreachable: {language.Id}/{scale}/{size}: {bottom}/{scroll.ActualHeight}");
                    foreach (var bar in Descendants<ScrollBar>(content))
                    {
                        bar.ApplyTemplate();
                        if (bar.Template.FindName("PART_Track", bar) is not Track track) throw new Exception("Missing scrollbar track");
                        track.Thumb.ApplyTemplate();
                        if (track.Thumb.Template.FindName("Grip", track.Thumb) is not Border) throw new Exception("Unthemed scrollbar");
                    }
                    window.ShowWindows(); Layout(size);
                    if (language.Id == "en" && scale == 2.25 && size.Width == 510) Save(content, output, "picker-large-text-minimum.png", 1);
                    var list = Control<ListBox>("WindowList");
                    if (list.ActualHeight < 30) throw new Exception($"Window list has no usable height: {language.Id}/{scale}/{size}");
                    var listScroll = Descendants<ScrollViewer>(list).First();
                    listScroll.ScrollToEnd(); Layout(size);
                    if (listScroll.ScrollableHeight - listScroll.VerticalOffset > 1) throw new Exception("Last list item unreachable");
                }
            }
        }
        languages.SelectedValue = "en"; themes.SelectedValue = "dark";
        app.Resources["BodySize"] = 14.0; app.Resources["SecondarySize"] = 13.0; app.Resources["HeadingSize"] = 18.0; app.Resources["PageTitleSize"] = 20.0;
        picker.SearchText = "No such window"; window.ShowWindows(); Layout(new(700, 760));
        if (Control<TextBlock>("Empty").Visibility != Visibility.Visible) throw new Exception("No-results state missing");
        picker.SearchText = "";
        foreach (var scale in new[] { 1.0, 1.5, 2.0 })
        {
            window.ShowWindows(); Layout(new(700, 760));
            Save(content, output, $"picker-{scale * 100:0}.png", scale);
            window.ShowSettings(); Control<ScrollViewer>("SettingsPanel").ScrollToTop(); Layout(new(700, 760));
            Save(content, output, $"preferences-{scale * 100:0}.png", scale);
            Control<ScrollViewer>("SettingsPanel").ScrollToEnd(); Layout(new(700, 760));
            Save(content, output, $"updates-{scale * 100:0}.png", scale);
        }
        // Exercise async-void UI error handling with the same dispatcher context as the app.
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(window.Dispatcher));
        source.FailEnumeration = true;
        var frame = new System.Windows.Threading.DispatcherFrame();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(10) };
        timer.Tick += (_, _) =>
        {
            if (Control<TextBlock>("Status").Visibility == Visibility.Visible || DateTime.UtcNow >= deadline)
                frame.Continue = false;
        };
        try
        {
            typeof(MainWindow).GetMethod("Toggle", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(window, [picker.Rows[0]]);
            timer.Start();
            System.Windows.Threading.Dispatcher.PushFrame(frame);
            if (Control<TextBlock>("Status").Text != Strings.Current["ActionFailed"])
                throw new Exception("Failed pin refresh did not produce UI feedback");
        }
        finally
        {
            timer.Stop();
            source.FailEnumeration = false;
            Control<TextBlock>("Status").Visibility = Visibility.Collapsed;
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
        Console.WriteLine("PASS pin refresh failure reports feedback without terminating the UI.");
        foreach (var bar in Descendants<ScrollBar>(content))
        {
            bar.ApplyTemplate();
            if (bar.Template.FindName("PART_Track", bar) is not Track track) throw new Exception("Missing scrollbar after dispatcher processing");
            track.Thumb.ApplyTemplate();
            if (track.Thumb.Template.FindName("Grip", track.Thumb) is not Border) throw new Exception("Unthemed scrollbar after dispatcher processing");
        }
        var handoffWindows = new HandoffWindows();
        var handoffPins = new PinSession(handoffWindows);
        handoffPins.Toggle(new(44, 55, 66));
        typeof(App).GetProperty("Pins", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(app, handoffPins);
        try
        {
            typeof(App).GetMethod("InstallPreparedUpdate", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(app, [Path.Combine(output, "missing-installer-" + Guid.NewGuid().ToString("N") + ".exe"), CancellationToken.None]);
            throw new Exception("Missing installer unexpectedly started");
        }
        catch (TargetInvocationException error) when (error.InnerException is System.ComponentModel.Win32Exception) { }
        if (!handoffWindows.Topmost) throw new Exception("Failed installer handoff removed an active pin");
        handoffPins.Restore();
        Console.WriteLine("PASS failed installer launch preserves active pins.");
        var refreshTimer=(System.Windows.Threading.DispatcherTimer)typeof(MainWindow).GetField("refresh",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
        window.ShowWindows();
        window.Show();
        if(!refreshTimer.IsEnabled) throw new Exception("Visible picker is not scheduled for refresh");
        var search = Control<TextBox>("Search");
        search.Text = picker.Rows[0].Window.Title;
        windowList.UpdateLayout();
        var result = (ListBoxItem)windowList.ItemContainerGenerator.ContainerFromIndex(0);
        if (!result.Focus() || !result.IsKeyboardFocused) throw new Exception("Picker result did not receive focus");
        var find = window.InputBindings.OfType<KeyBinding>().Single(binding => binding.Key == Key.F && binding.Modifiers == ModifierKeys.Control);
        if (find.Command is not RoutedCommand command || !command.CanExecute(null, result))
            throw new Exception("Ctrl+F command is unavailable from a picker result");
        command.Execute(null, result);
        if (!search.IsKeyboardFocused || search.Text.Length == 0 || search.SelectedText != search.Text)
            throw new Exception("Ctrl+F did not focus the search box and select the existing filter");
        Console.WriteLine("PASS Ctrl+F command moves focus from a picker result and selects the filter.");
        window.WindowState=WindowState.Minimized;
        if(refreshTimer.IsEnabled) throw new Exception("Minimized picker continues polling windows");
        window.WindowState=WindowState.Normal;
        if(!refreshTimer.IsEnabled) throw new Exception("Restored picker does not resume refresh");
        window.ShowSettings();
        if (command.CanExecute(null, Control<Button>("BackButton"))) throw new Exception("Picker search shortcut is enabled in Preferences");
        if(refreshTimer.IsEnabled) throw new Exception("Preferences continue scheduling unnecessary picker polling");
        window.Hide();
        Console.WriteLine("PASS minimized picker and preferences stop polling; restoring picker resumes it.");
        app.Resources["PageTitleSize"]=45.0;
        var support=new SupportWindow(window);
        var supportHeading=((StackPanel)support.Content).Children[0] as TextBlock;
        if(supportHeading?.FontSize != 45.0) throw new Exception("Support heading ignores Windows text scaling");
        app.Resources["PageTitleSize"]=20.0;
        if(supportHeading.FontSize != 20.0) throw new Exception("Support heading does not follow dynamic text scaling");
        support.Close();
        Console.WriteLine("PASS support heading follows dynamic Windows text scaling.");
        updates.DisposeAsync().AsTask().GetAwaiter().GetResult();
        Console.WriteLine("PASS 21 languages, minimum/normal size, 100/150/225% text, themed scrollbars and reachable final content.");
        Console.WriteLine("PASS 100/150/200% WPF raster samples saved (not a physical mixed-monitor test).");
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject node) where T : DependencyObject
    {
        if (node is T item) yield return item;
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(node, i))) yield return child;
    }
    private static void Save(FrameworkElement content, string directory, string name, double scale)
    {
        var bitmap = new RenderTargetBitmap((int)(content.ActualWidth * scale), (int)(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(content);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var file = File.Create(Path.Combine(directory, name)); encoder.Save(file);
    }
    private sealed class NoRegistration : IHotkeyRegistrar { public bool Replace(string slot, string text, Action action) => false; }
    private sealed class NoStartup : IStartupService { public bool Enabled { get; set; } }
    private sealed class SampleWindows : IWindowSource, IWindows
    {
        public int ToggleCalls { get; private set; }
        public bool FailEnumeration { get; set; }
        public IReadOnlyList<DesktopWindow> Enumerate() => FailEnumeration ? throw new InvalidOperationException("Fixture enumeration failure") : Enumerable.Range(0, 80).Select(i => new DesktopWindow(new(i + 10, 1, 1),
            i switch { 0 => "Project notes — planning and research", 1 => "Design references — a very long window title which must end in an ellipsis", 2 => "日本語 · 中文 · 한국어", _ => $"Sample window {i}" },
            i < 2 ? "Google Chrome" : "Notepad", i < 2)).ToArray();
        public bool IsCurrent(WindowIdentity identity) => true;
        public bool IsTopmost(WindowIdentity identity) => false;
        public bool SetTopmost(WindowIdentity identity, bool value) { ToggleCalls++; return true; }
    }
    private sealed class HandoffWindows : IWindows
    {
        public bool Topmost { get; private set; }
        public bool IsCurrent(WindowIdentity identity) => true;
        public bool IsTopmost(WindowIdentity identity) => Topmost;
        public bool SetTopmost(WindowIdentity identity, bool value) { Topmost = value; return true; }
    }
}
