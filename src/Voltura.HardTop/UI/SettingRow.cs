using System.Windows;
using System.Windows.Controls;

namespace Voltura.HardTop.Ui;

// Stack labels above controls when the available logical width or text size needs it.
public sealed class SettingRow : System.Windows.Controls.Panel
{
    private bool stacked;
    private double labelWidth;
    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count != 2)
            return default;
        Children[0].Measure(new Size(double.PositiveInfinity, availableSize.Height));
        labelWidth = Children[0].DesiredSize.Width;
        stacked = availableSize.Width < 450 || labelWidth > availableSize.Width * .42;
        var controlWidth = stacked ? availableSize.Width : availableSize.Width * .58;
        Children[0].Measure(new Size(stacked ? availableSize.Width : availableSize.Width * .42 - 16, double.PositiveInfinity));
        Children[1].Measure(new Size(controlWidth, double.PositiveInfinity));
        return new Size(availableSize.Width, stacked
            ? Children[0].DesiredSize.Height + 8 + Children[1].DesiredSize.Height
            : Math.Max(Children[0].DesiredSize.Height, Children[1].DesiredSize.Height));
    }
    protected override Size ArrangeOverride(Size size)
    {
        var controlX = stacked ? 0 : size.Width * .42;
        Children[0].Arrange(new Rect(0, stacked ? 0 : (size.Height - Children[0].DesiredSize.Height) / 2,
            stacked ? size.Width : controlX - 16, Children[0].DesiredSize.Height));
        Children[1].Arrange(new Rect(controlX, stacked ? Children[0].DesiredSize.Height + 8 : 0,
            size.Width - controlX, stacked ? Children[1].DesiredSize.Height : size.Height));
        return size;
    }
}
