using Kazo.Bar.Core.Interfaces;

namespace Kazo.Bar.Maui.Platforms.Windows.Services;

/// <summary>
/// Windows implementation — delegates to WindowHelper for Win32 region manipulation.
/// </summary>
public class WindowsRegionService : IWindowRegionService
{
    public void ExpandRegion(int totalHeightCss)
        => WindowHelper.ExpandRegion(totalHeightCss);

    public void CollapseRegion()
        => WindowHelper.CollapseRegion();
}
