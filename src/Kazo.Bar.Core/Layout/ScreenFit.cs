using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Core.Layout;

/// <summary>A rectangle in screen physical pixels (right/bottom exclusive, like Win32 RECT).</summary>
public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

/// <summary>
/// Host-neutral math to keep the toolbar window the right size and on screen when it
/// changes monitor, in particular between monitors with a different scale (DPI).
/// Sizes in DIPs / CSS px are converted with the window's scale (DPI / 96).
/// </summary>
public static class ScreenFit
{
    /// <summary>Physical size of a logical surface at the given scale.</summary>
    public static (int Width, int Height) ToPixels(SurfaceSize size, double scale)
    {
        if (!(scale > 0)) scale = 1;
        return ((int)Math.Round(size.Width * scale), (int)Math.Round(size.Height * scale));
    }

    /// <summary>
    /// True when the window's physical size does not match the surface at this scale
    /// (one pixel of rounding is tolerated).
    /// </summary>
    public static bool NeedsResize(PixelRect window, SurfaceSize surface, double scale)
    {
        var (width, height) = ToPixels(surface, scale);
        return Math.Abs(window.Width - width) > 1 || Math.Abs(window.Height - height) > 1;
    }

    /// <summary>
    /// New window rectangle for a new physical size: stays centered horizontally and keeps
    /// its anchored edge (top, or bottom when docked at the bottom of the screen).
    /// </summary>
    public static PixelRect Resize(PixelRect window, int width, int height, bool anchorBottom)
    {
        int left = window.Left + (window.Width - width) / 2;
        int top = anchorBottom ? window.Bottom - height : window.Top;
        return new PixelRect(left, top, left + width, top + height);
    }

    /// <summary>
    /// Converts a rectangle given in DIPs / CSS px, relative to the window's top-left,
    /// to screen physical pixels (rounded outward).
    /// </summary>
    public static PixelRect ToScreen(PixelRect window, double left, double top, double right, double bottom, double scale)
    {
        if (!(scale > 0)) scale = 1;
        return new PixelRect(
            window.Left + (int)Math.Floor(left * scale),
            window.Top + (int)Math.Floor(top * scale),
            window.Left + (int)Math.Ceiling(right * scale),
            window.Top + (int)Math.Ceiling(bottom * scale));
    }

    /// <summary>
    /// Offset that brings <paramref name="inner"/> fully inside <paramref name="work"/>.
    /// When it is larger than the work area, its left / top edge wins, so the start of the
    /// pill stays reachable.
    /// </summary>
    public static (int Dx, int Dy) OffsetIntoWorkArea(PixelRect inner, PixelRect work) =>
        (Axis(inner.Left, inner.Right, work.Left, work.Right), Axis(inner.Top, inner.Bottom, work.Top, work.Bottom));

    private static int Axis(int start, int end, int min, int max)
    {
        if (start < min) return min - start;
        if (end > max) return Math.Max(max - end, min - start);
        return 0;
    }
}
