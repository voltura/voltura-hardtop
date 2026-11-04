using System.Windows;
using System.Windows.Controls;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;

namespace Voltura.HardTop;

// Same default-cancel, topmost owner pattern as Earner's ConfirmationWindow.
internal sealed class ConfirmWindow : Window
{
    private ConfirmWindow(string message, string action, string? heading)
    {
        Title = "Voltura HardTop";
        Width = 430;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty, typeof(Window));
        var content = new StackPanel { Margin = new Thickness(24) };
        if (heading is not null)
        {
            var title = new TextBlock { Text = heading, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 12) };
            title.SetResourceReference(FontSizeProperty, "HeadingSize");
            content.Children.Add(title);
        }
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 22) });
        var buttons = new WrapPanel { HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        var cancel = new Button { Content = new TextBlock { Text = Strings.Current["Cancel"] }, IsCancel = true, IsDefault = true, MinWidth = 90, MaxWidth = 366 };
        var confirm = new Button { Content = new TextBlock { Text = action }, MinWidth = 90, MaxWidth = 366, Margin = new Thickness(0, 0, 8, 8) };
        confirm.SetResourceReference(BackgroundProperty, "AccentBrush");
        confirm.SetResourceReference(ForegroundProperty, "AccentTextBrush");
        cancel.Click += (_, _) => Close();
        confirm.Click += (_, _) => DialogResult = true;
        buttons.Children.Add(confirm);
        buttons.Children.Add(cancel);
        content.Children.Add(buttons);
        Content = content;
        WindowWorkAreaPlacement.ConstrainAndCenterOnFirstLoad(this);
        Loaded += (_, _) => { Activate(); cancel.Focus(); };
    }
    internal static bool Ask(Window owner, string message, string action, string? heading = null) => new ConfirmWindow(message, action, heading) { Owner = owner, Topmost = true }.ShowDialog() == true;
}
