using System.Windows;
using System.Windows.Controls;
using Voltura.HardTop.Localization;
using Voltura.HardTop.Platform;

namespace Voltura.HardTop;

internal sealed class SupportWindow : Window
{
    internal SupportWindow(Window owner)
    {
        Owner = owner;
        Topmost = true;
        Title = Strings.Current["SupportVoltura"];
        Width = 380;
        SizeToContent = SizeToContent.Height;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(StyleProperty, typeof(Window));
        var content = new StackPanel { Margin = new Thickness(24) };
        var heading = new TextBlock { Text = Title, Margin = new Thickness(0, 0, 0, 20) };
        heading.SetResourceReference(FontSizeProperty, "PageTitleSize");
        content.Children.Add(heading);
        foreach (var (name, url) in new[] { ("Ko-fi", "https://ko-fi.com/G2G74W5F8"), ("PayPal", "https://www.paypal.me/voltura") })
        {
            var button = new Button { Content = name, Margin = new Thickness(0, 0, 0, 12) };
            button.Click += (_, _) => App.Open(url);
            content.Children.Add(button);
        }
        var cancel = new Button { Content = Strings.Current["Cancel"], IsCancel = true, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        cancel.Click += (_, _) => Close();
        content.Children.Add(cancel);
        Content = content;
        WindowWorkAreaPlacement.ConstrainAndCenterOnFirstLoad(this);
    }
}
