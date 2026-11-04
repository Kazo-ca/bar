namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Service for executing scripts with output streaming.
/// </summary>
public interface IScriptService
{
    /// <summary>Execute a script and return the output.</summary>
    Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments = "");

    /// <summary>
    /// Execute a script and report each output line as it is written (US4.3).
    /// Cancelling <paramref name="cancellationToken"/> stops the script; the result then has
    /// <see cref="ScriptResult.WasCancelled"/> set.
    /// Hosts without streaming fall back to <see cref="ExecuteAsync(string, string, string)"/>
    /// and report the lines when the script ends.
    /// </summary>
    async Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments,
        Action<ScriptOutputLine>? onOutput, CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(interpreter, scriptPath, arguments);
        if (onOutput != null)
        {
            foreach (var line in ScriptOutputLine.Split(result.Output, isError: false))
                onOutput(line);
            foreach (var line in ScriptOutputLine.Split(result.Error, isError: true))
                onOutput(line);
        }
        return result;
    }
}

/// <summary>One line written by a script, on standard output or standard error.</summary>
public sealed record ScriptOutputLine(string Text, bool IsError)
{
    /// <summary>The lines of <paramref name="text"/>, without a trailing empty line.</summary>
    public static IEnumerable<ScriptOutputLine> Split(string? text, bool isError)
    {
        if (string.IsNullOrEmpty(text))
            yield break;
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var count = lines[^1].Length == 0 ? lines.Length - 1 : lines.Length;
        for (var i = 0; i < count; i++)
            yield return new ScriptOutputLine(lines[i], isError);
    }
}

/// <summary>
/// Result of a script execution.
/// </summary>
public class ScriptResult
{
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    /// <summary>True when the user stopped the script before it ended.</summary>
    public bool WasCancelled { get; set; }
    public bool Success => ExitCode == 0 && !WasCancelled;
}
