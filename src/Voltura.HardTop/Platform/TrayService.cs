using System.Windows.Threading;
using Voltura.HardTop.Localization;

namespace Voltura.HardTop.Platform;

internal sealed record TrayActions(Action Open, Action Preferences, Action CheckUpdates, Action InstallUpdate, Action Support, Action Exit);

internal sealed class TrayService : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon icon;
    private readonly TrayIconVisibilityPromoter promoter;
    private readonly TrayActions actions;
    private bool disposed;

    internal TrayService(Dispatcher dispatcher, TrayActions actions)
    {
        this.actions = actions;
        using var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/App.ico")).Stream;
        icon = new()
        {
            Icon = new System.Drawing.Icon(stream),
            Text = "Voltura HardTop",
            Visible = true,
        };
        icon.MouseClick += (_, args) =>
        {
            if (args.Button == System.Windows.Forms.MouseButtons.Left)
                actions.Open();
        };
        promoter = new(dispatcher, RefreshRegistration);
        promoter.Start();
    }

    internal void Rebuild(bool dark, bool updateReady)
    {
        if (disposed)
            return;
        var renderer = new TrayMenuRenderer(dark);
        var menu = new System.Windows.Forms.ContextMenuStrip
        {
            Renderer = renderer,
            BackColor = renderer.Surface,
            ForeColor = renderer.Ink,
        };

        void Add(string key, Action action)
        {
            var item = menu.Items.Add(Strings.Current[key], null, (_, _) => action());
            item.Padding = new System.Windows.Forms.Padding(10, 6, 10, 6);
        }

        Add("OpenApp", actions.Open);
        Add("Preferences", actions.Preferences);
        Add("CheckForUpdates", actions.CheckUpdates);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        Add("SupportVoltura", actions.Support);
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        Add("ExitApp", actions.Exit);

        var previous = icon.ContextMenuStrip;
        icon.ContextMenuStrip = menu;
        previous?.Dispose();
    }

    internal void Warn(string key)
    {
        if (!disposed)
            icon.ShowBalloonTip(3500, "Voltura HardTop", Strings.Current[key], System.Windows.Forms.ToolTipIcon.Warning);
    }

    internal void RestartPromotion()
    {
        if (!disposed)
            promoter.Start();
    }

    private void RefreshRegistration()
    {
        if (disposed)
            return;
        icon.Visible = false;
        icon.Visible = true;
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        promoter.Dispose();
        icon.Visible = false;
        icon.ContextMenuStrip?.Dispose();
        icon.Icon?.Dispose();
        icon.Dispose();
    }
}
