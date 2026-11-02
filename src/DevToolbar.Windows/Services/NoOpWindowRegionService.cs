using DevToolbar.Core.Interfaces;

namespace DevToolbar.Windows.Services;

/// <summary>
/// The WPF host never clips the window with SetWindowRgn: per-pixel alpha already
/// decides what is visible and what takes input. Expanding/collapsing is a no-op.
/// </summary>
public sealed class NoOpWindowRegionService : IWindowRegionService
{
    public void ExpandRegion(int totalHeightCss) { }
    public void CollapseRegion() { }
}
