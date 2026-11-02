namespace DevToolbar.Web.Mocks;

using DevToolbar.Core.Interfaces;

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
        "Windows PowerShell"
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

    public bool FocusWindowByTitle(string titleRegex)
    {
        _logger.LogInformation("[Mock] FocusWindow: {TitleRegex}", titleRegex);
        try
        {
            var regex = new System.Text.RegularExpressions.Regex(titleRegex,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100));
            return SimulatedWindowTitles.Any(regex.IsMatch);
        }
        catch (ArgumentException)
        {
            _logger.LogWarning("[Mock] Invalid window title regex: {TitleRegex}", titleRegex);
            return false;
        }
    }
}
