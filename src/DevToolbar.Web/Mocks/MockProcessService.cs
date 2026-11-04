namespace DevToolbar.Web.Mocks;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;

/// <summary>
/// Mock process service for web testing. Logs actions without executing them.
/// Simulates a few open windows so Smart Focus (US4.2) can be exercised, and
/// fails for paths containing "missing" so error toasts (US4.1) can be tested.
/// </summary>
public class MockProcessService : IProcessService
{
    /// <summary>Paths containing this marker simulate an executable that does not exist.</summary>
    public const string MissingPathMarker = "missing";

    /// <summary>Window titles the mock pretends are open on the desktop.</summary>
    private static readonly string[] SimulatedWindowTitles =
    {
        "MyWebAPI - Microsoft Visual Studio",
        "Windows PowerShell",
        // Two matches for ".*Azure.*" so the window chooser (US4.2) can be exercised.
        "Azure Portal - Microsoft Edge",
        "Azure DevOps Boards - Microsoft Edge",
    };

    private readonly ILogger<MockProcessService> _logger;

    public MockProcessService(ILogger<MockProcessService> logger)
    {
        _logger = logger;
    }

    public Task<int> StartProcessAsync(string path, string arguments = "")
    {
        _logger.LogInformation("[Mock] StartProcess: {Path} {Arguments}", path, arguments);
        if (string.IsNullOrWhiteSpace(path) || path.Contains(MissingPathMarker, StringComparison.OrdinalIgnoreCase))
        {
            throw new FileNotFoundException($"Executable not found: {path}");
        }
        return Task.FromResult(0);
    }

    public IReadOnlyList<WindowMatch> FindWindowsByTitle(string titleRegex)
    {
        _logger.LogInformation("[Mock] FindWindows: {TitleRegex}", titleRegex);
        try
        {
            var regex = new System.Text.RegularExpressions.Regex(titleRegex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            return SimulatedWindowTitles
                .Select((title, index) => new WindowMatch(index + 1, title, "mock"))
                .Where(window => regex.IsMatch(window.Title))
                .ToList();
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("[Mock] Invalid window title regex: {TitleRegex}", titleRegex);
            return [];
        }
    }

    public bool FocusWindow(WindowMatch window)
    {
        _logger.LogInformation("[Mock] FocusWindow: {Title}", window.Title);
        return SimulatedWindowTitles.Contains(window.Title);
    }
}
