namespace DevToolbar.Core.Models;

/// <summary>A desktop window found by Smart Focus (US4.2).</summary>
/// <param name="Handle">Native window handle (HWND on Windows; any unique id in mocks).</param>
/// <param name="Title">Window title as shown in the taskbar.</param>
/// <param name="ProcessName">Name of the owning process, without extension (may be empty).</param>
public sealed record WindowMatch(nint Handle, string Title, string ProcessName);
