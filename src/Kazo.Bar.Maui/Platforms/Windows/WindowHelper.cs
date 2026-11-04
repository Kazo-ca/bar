using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;

namespace Kazo.Bar.Maui.Platforms.Windows;

/// <summary>
/// Windows-specific window configuration for Kazo Bar.
/// Uses SetWindowRgn to clip the window to the visible toolbar pill shape.
/// Areas outside the region are truly invisible AND click-through — no
/// transparency hacks required.
/// </summary>
public static class WindowHelper
{
    private static nint _hWnd;
    private static AppWindow? _appWindow;

    /// <summary>Toolbar pill height in CSS pixels (including shell padding).</summary>
    private const int ToolbarHeightCss = 66;
    /// <summary>Pill corner radius in CSS pixels.</summary>
    private const int CornerRadiusCss = 28;
    /// <summary>Default toolbar width in CSS pixels.</summary>
    private const int ToolbarWidthCss = 960;

    /// <summary>The native window handle for external consumers.</summary>
    public static nint Hwnd => _hWnd;

    public static void ConfigureToolbarWindow(Microsoft.UI.Xaml.Window nativeWindow, bool topMost = true)
    {
        _hWnd = WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);
        if (_appWindow == null) return;

        // 1. Borderless, always on top
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = topMost;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
        }

        // 2. Remove system backdrop (Mica / Acrylic)
        nativeWindow.SystemBackdrop = null;

        // 3. DPI-aware physical pixel calculations
        double scale = GetDpiScale();
        int physWidth = (int)(ToolbarWidthCss * scale);
        int physTotalHeight = (int)(500 * scale); // tall enough for any dropdown/panel
        int physToolbarH = (int)(ToolbarHeightCss * scale);
        int physRadius = (int)(CornerRadiusCss * scale);

        // 4. Size the window — tall for content capacity, region clips it
        _appWindow.Resize(new global::Windows.Graphics.SizeInt32(physWidth, physTotalHeight));

        // 5. Center horizontally at top of work area
        var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        int x = (displayArea.WorkArea.Width - physWidth) / 2;
        _appWindow.Move(new global::Windows.Graphics.PointInt32(x, 0));

        // 6. Clip the window to a rounded rectangle (only the toolbar strip is visible)
        //    Everything outside this region is invisible AND click-through.
        ApplyToolbarRegion(physWidth, physToolbarH, physRadius);
    }

    /// <summary>
    /// Set the visible window region to the toolbar pill shape.
    /// </summary>
    public static void ApplyToolbarRegion(int physWidth, int physHeight, int physRadius)
    {
        if (_hWnd == 0) return;
        nint hRgn = CreateRoundRectRgn(0, 0, physWidth + 1, physHeight + 1, physRadius, physRadius);
        SetWindowRgn(_hWnd, hRgn, true);
        // Note: Windows takes ownership of the region — do NOT delete hRgn.
    }

    /// <summary>
    /// Expand the visible region to show content below the toolbar (e.g. settings panel).
    /// Uses a rounded top (toolbar) + rectangular bottom (panel) combined region.
    /// </summary>
    public static void ExpandRegion(int totalHeightCss)
    {
        if (_hWnd == 0 || _appWindow == null) return;
        double scale = GetDpiScale();
        int w = _appWindow.Size.Width;
        int toolbarH = (int)(ToolbarHeightCss * scale);
        int totalH = (int)(totalHeightCss * scale);
        int radius = (int)(CornerRadiusCss * scale);

        // Top: rounded rect for the toolbar pill
        nint topRgn = CreateRoundRectRgn(0, 0, w + 1, toolbarH + 1, radius, radius);
        // Bottom: rectangle for the expanded panel
        nint bottomRgn = CreateRectRgn(8, toolbarH / 2, w - 8, totalH + 1);
        CombineRgn(topRgn, topRgn, bottomRgn, RGN_OR);
        SetWindowRgn(_hWnd, topRgn, true);
        DeleteObject(bottomRgn);
    }

    /// <summary>
    /// Collapse the region back to the toolbar pill only.
    /// </summary>
    public static void CollapseRegion()
    {
        if (_hWnd == 0 || _appWindow == null) return;
        double scale = GetDpiScale();
        ApplyToolbarRegion(
            _appWindow.Size.Width,
            (int)(ToolbarHeightCss * scale),
            (int)(CornerRadiusCss * scale));
    }

    private static double GetDpiScale()
    {
        uint dpi = GetDpiForWindow(_hWnd);
        return dpi / 96.0;
    }

    #region P/Invoke

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("gdi32.dll")]
    private static extern nint CreateRectRgn(int x1, int y1, int x2, int y2);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(nint hrgnDest, nint hrgnSrc1, nint hrgnSrc2, int fnCombineMode);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(nint hObject);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hWnd, nint hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    private const int RGN_OR = 2;

    #endregion
}
