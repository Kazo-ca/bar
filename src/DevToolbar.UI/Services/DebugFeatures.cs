namespace DevToolbar.UI.Services;

/// <summary>
/// Whether developer-only toolbar features (the 🧪 Test Dropdown, future debug panels) are shown
/// (US3.5): always in a Debug build of the UI, and in a Release build only when the host turns
/// them on (for example the Web test host in its Development environment).
/// Every debug feature checks <see cref="Enabled"/> before rendering.
/// </summary>
public sealed class DebugFeatures
{
    public DebugFeatures(bool enabledByHost = false)
    {
        Enabled = IsDebugBuild || enabledByHost;
    }

    /// <summary>The UI library was compiled with the DEBUG symbol.</summary>
    public static bool IsDebugBuild =>
#if DEBUG
        true;
#else
        false;
#endif

    public bool Enabled { get; }
}
