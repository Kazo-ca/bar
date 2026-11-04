namespace DevToolbar.Core.Interfaces;

using DevToolbar.Core.Models;

/// <summary>
/// Abstracts process and window management operations.
/// </summary>
public interface IProcessService
{
    /// <summary>Start a process and return its PID.</summary>
    Task<int> StartProcessAsync(string path, string arguments = "");

    /// <summary>
    /// Visible top-level windows whose title matches <paramref name="titleRegex"/> (case-insensitive),
    /// in desktop order (US4.2). An invalid pattern returns no window.
    /// </summary>
    IReadOnlyList<WindowMatch> FindWindowsByTitle(string titleRegex);

    /// <summary>Restores (if minimized) and activates <paramref name="window"/>. False when it is gone.</summary>
    bool FocusWindow(WindowMatch window);

    /// <summary>Focus the first window whose title matches. Returns true if found.</summary>
    bool FocusWindowByTitle(string titleRegex)
    {
        var windows = FindWindowsByTitle(titleRegex);
        return windows.Count > 0 && FocusWindow(windows[0]);
    }
}
