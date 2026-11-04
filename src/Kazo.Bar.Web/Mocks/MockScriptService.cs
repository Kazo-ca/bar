namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;

/// <summary>
/// Mock script service for web testing. Streams a few lines with a short delay (US4.3)
/// so the terminal can be seen updating and stopped while it runs.
/// </summary>
public class MockScriptService : IScriptService
{
    /// <summary>Delay between two simulated lines.</summary>
    public static readonly TimeSpan LineDelay = TimeSpan.FromMilliseconds(300);

    public Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments = "") =>
        ExecuteAsync(interpreter, scriptPath, arguments, null, CancellationToken.None);

    public async Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments,
        Action<ScriptOutputLine>? onOutput, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"[MockScript] {interpreter} {scriptPath} {arguments}");
        string[] lines =
        [
            $"[Mock] Executed: {interpreter} {scriptPath} {arguments}".TrimEnd(),
            "Restoring packages...",
            "Build successful.",
            "3 tests passed.",
        ];
        var output = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            try
            {
                await Task.Delay(LineDelay, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return new ScriptResult { ExitCode = -1, Output = output.ToString(), WasCancelled = true };
            }
            output.Append(line).Append('\n');
            onOutput?.Invoke(new ScriptOutputLine(line, IsError: false));
        }
        return new ScriptResult { ExitCode = 0, Output = output.ToString() };
    }
}
