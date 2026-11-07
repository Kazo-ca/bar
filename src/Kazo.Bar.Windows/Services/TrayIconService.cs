using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using Kazo.Bar.Core.Dialogs;
using Kazo.Bar.UI.Dialogs;
using WinForms = System.Windows.Forms;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// Notification-area (tray) icon for the WPF host: the always-available way to
/// show, hide or quit the toolbar, even when the toolbar itself is hidden,
/// docked as an app bar or rendered fully transparent.
///
/// Left click toggles the toolbar; right click opens the menu.
/// Uses the WinForms NotifyIcon (UseWindowsForms is already on for System.Drawing).
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private const string AppName = "Kazo Bar";
    private const int IconSize = 32;

    private readonly ToolbarHost _host;
    private readonly IDialogService _dialogs;
    private WinForms.NotifyIcon? _notifyIcon;
    private Icon? _icon;

    public TrayIconService(ToolbarHost host, IDialogService dialogs)
    {
        _host = host;
        _dialogs = dialogs;
    }

    /// <summary>Creates the tray icon. Must run on the UI thread, after the window exists.</summary>
    public void Show()
    {
        if (_notifyIcon != null) return;

        var toggleItem = new WinForms.ToolStripMenuItem("Hide toolbar", null, (_, _) => ToggleToolbar());
        var menu = new WinForms.ContextMenuStrip();
        menu.Items.Add(toggleItem);
        menu.Items.Add(new WinForms.ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings()));
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(new WinForms.ToolStripMenuItem("Quit Kazo Bar", null, (_, _) => Quit()));
        menu.Opening += (_, _) => toggleItem.Text = IsToolbarVisible ? "Hide toolbar" : "Show toolbar";

        _icon = CreateIcon();
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = _icon,
            Text = AppName,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _notifyIcon.MouseClick += (_, e) =>
        {
            if (e.Button == WinForms.MouseButtons.Left) ToggleToolbar();
        };
        Program.Log("Tray icon shown.");
    }

    private bool IsToolbarVisible => _host.Window?.IsVisible == true;

    private void ToggleToolbar()
    {
        var window = _host.Window;
        if (window == null) return;
        if (window.IsVisible)
        {
            window.Hide();
            return;
        }
        window.Show();
        window.Activate();
    }

    /// <summary>Settings open in their own window, even while the toolbar is hidden.</summary>
    private async void OpenSettings()
    {
        try
        {
            await _dialogs.OpenSettingsAsync();
        }
        catch (Exception ex)
        {
            Program.Log($"Could not open the Settings window: {ex}");
        }
    }

    private void Quit()
    {
        Program.Log("Exit requested from the tray icon.");
        // Remove the icon first so it never lingers in the notification area.
        Dispose();
        System.Windows.Application.Current?.Shutdown();
    }

    public void Dispose()
    {
        if (_notifyIcon != null)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.ContextMenuStrip?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
        _icon?.Dispose();
        _icon = null;
    }

    /// <summary>Draws the tray icon (rounded blue square with "DT") so no .ico asset is needed.</summary>
    private static Icon CreateIcon()
    {
        using var bitmap = new Bitmap(IconSize, IconSize);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.Clear(Color.Transparent);

            using var path = RoundedRect(new Rectangle(1, 1, IconSize - 2, IconSize - 2), 7);
            using var fill = new SolidBrush(Color.FromArgb(255, 0, 120, 212));
            g.FillPath(fill, path);

            using var font = new Font("Segoe UI", 13f, System.Drawing.FontStyle.Bold, GraphicsUnit.Pixel);
            using var text = new SolidBrush(Color.White);
            using var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("DT", font, text, new RectangleF(0, 0, IconSize, IconSize), format);
        }

        var hIcon = bitmap.GetHicon();
        try
        {
            // Clone so the icon owns its own handle; the handle from GetHicon is freed below.
            using var temp = Icon.FromHandle(hIcon);
            return (Icon)temp.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hIcon);
        }
    }

    private static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var d = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, d, d, 180, 90);
        path.AddArc(bounds.Right - d, bounds.Top, d, d, 270, 90);
        path.AddArc(bounds.Right - d, bounds.Bottom - d, d, d, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }
}
