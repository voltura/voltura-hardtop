using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;

namespace Voltura.HardTop.Ui;

public partial class ShortcutEditor : System.Windows.Controls.UserControl
{
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(ShortcutEditor),
        new FrameworkPropertyMetadata("Ctrl+Alt+Z", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
            (owner, _) => ((ShortcutEditor)owner).RefreshText()));
    private ShortcutCapture? capture;
    public event Action? TextChanged;
    public bool IsCapturing
    {
        get; private set;
    }
    public string Text
    {
        get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value);
    }

    public ShortcutEditor()
    {
        InitializeComponent();
        RefreshText();
        Unloaded += (_, _) => EndCapture();
        IsVisibleChanged += (_, _) => { if (!IsVisible) EndCapture(); };
    }
    private void RefreshText()
    {
        if (Combination is null)
            return;
        Combination.Text = IsCapturing ? Strings.Current["PressShortcut"] : Text.Replace("+", " + ");
        TextChanged?.Invoke();
    }
    private void BeginCapture(object sender, RoutedEventArgs e)
    {
        EndCapture();
        var window = Window.GetWindow(this);
        if (window is null)
            return;
        IsCapturing = true;
        RefreshText();
        try
        {
            capture = new ShortcutCapture(new WindowInteropHelper(window).Handle, gesture =>
                Dispatcher.BeginInvoke(() => Complete(gesture)), action => Dispatcher.BeginInvoke(action));
        }
        catch (System.ComponentModel.Win32Exception) { EndCapture(); }
    }
    private void Complete(HotkeyGesture? gesture)
    {
        if (gesture is { } value)
        {
            var names = new List<string>();
            if ((value.Modifiers & 2) != 0)
                names.Add("Ctrl");
            if ((value.Modifiers & 1) != 0)
                names.Add("Alt");
            if ((value.Modifiers & 4) != 0)
                names.Add("Shift");
            if ((value.Modifiers & 8) != 0)
                names.Add("Win");
            names.Add(KeyInterop.KeyFromVirtualKey((int)value.Key).ToString());
            var candidate = string.Join('+', names);
            if (HotkeyGesture.TryParse(candidate, out _))
                SetCurrentValue(TextProperty, candidate);
        }
        IsCapturing = false;
        RefreshText();
    }
    private void CaptureLostFocus(object sender, KeyboardFocusChangedEventArgs e) => EndCapture();
    private void EndCapture()
    {
        capture?.Dispose();
        capture = null;
        IsCapturing = false;
        RefreshText();
    }
}
