namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Service to expand/collapse the visible window region.
/// Used by UI components (dropdowns, panels) that overflow the toolbar pill.
/// </summary>
public interface IWindowRegionService
{
    /// <summary>Expand the visible region to the given total height in CSS pixels.</summary>
    void ExpandRegion(int totalHeightCss);

    /// <summary>Collapse the region back to the toolbar pill only.</summary>
    void CollapseRegion();
}
