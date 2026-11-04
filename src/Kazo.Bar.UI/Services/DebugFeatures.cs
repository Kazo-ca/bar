namespace Kazo.Bar.UI.Services;

/// <summary>
/// US3.5: developer-only UI (the 🧪 "Test Dropdown" button, and any future debug tool) is rendered
/// only when Kazo.Bar.UI is compiled in the Debug configuration. Check <see cref="Enabled"/>
/// instead of writing <c>#if DEBUG</c> in each component.
/// </summary>
public static class DebugFeatures
{
#if DEBUG
    public static bool Enabled { get; } = true;
#else
    public static bool Enabled { get; } = false;
#endif
}
