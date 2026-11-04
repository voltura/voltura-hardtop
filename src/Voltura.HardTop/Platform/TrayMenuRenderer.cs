using System.Drawing.Drawing2D;
using System.Windows.Forms;
using DrawingColor = System.Drawing.Color;
using DrawingBrush = System.Drawing.SolidBrush;
using DrawingPen = System.Drawing.Pen;

namespace Voltura.HardTop.Platform;

// Earner/Air flat renderer with NetOn's explicit text rendering. No system hover gradients.
internal sealed class TrayMenuRenderer(bool dark) : ToolStripProfessionalRenderer
{
    internal DrawingColor Surface { get; } = SystemInformation.HighContrast ? System.Drawing.SystemColors.Menu : System.Drawing.ColorTranslator.FromHtml(dark ? "#1B2538" : "#FFFFFF");
    internal DrawingColor Ink { get; } = SystemInformation.HighContrast ? System.Drawing.SystemColors.MenuText : System.Drawing.ColorTranslator.FromHtml(dark ? "#F1F5FC" : "#18213A");
    private readonly DrawingColor hover = SystemInformation.HighContrast ? System.Drawing.SystemColors.Highlight : System.Drawing.ColorTranslator.FromHtml(dark ? "#263557" : "#EAF0FF");
    private readonly DrawingColor border = SystemInformation.HighContrast ? System.Drawing.SystemColors.MenuText : System.Drawing.ColorTranslator.FromHtml(dark ? "#35435D" : "#DCE2EE");
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
    {
        using var brush = new DrawingBrush(Surface);
        e.Graphics.FillRectangle(brush, e.AffectedBounds);
    }
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) => OnRenderToolStripBackground(e);
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
    {
        using var brush = new DrawingBrush(e.Item.Selected && e.Item.Enabled ? hover : Surface);
        e.Graphics.FillRectangle(brush, new System.Drawing.Rectangle(System.Drawing.Point.Empty, e.Item.Size));
    }
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
    {
        e.TextColor = SystemInformation.HighContrast && e.Item.Selected ? System.Drawing.SystemColors.HighlightText : Ink;
        base.OnRenderItemText(e);
    }
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
    {
        using var pen = new DrawingPen(border);
        e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
    }
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
    {
        using var pen = new DrawingPen(border);
        e.Graphics.DrawLine(pen, 12, e.Item.Height / 2, e.Item.Width - 12, e.Item.Height / 2);
    }
}
