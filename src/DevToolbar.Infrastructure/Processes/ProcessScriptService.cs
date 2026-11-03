namespace DevToolbar.Infrastructure.Processes;

using System.Diagnostics;
using System.Text;
using DevToolbar.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Runs a script action with its interpreter (US4.3) and reports each line of standard output and
/// standard error while the script runs. Stopping kills the whole process tree.
/// </summary>
public sealed class ProcessScriptService : IScriptService
{
    private readonly ILogger _logger;

    public ProcessScriptService(ILogger<ProcessScriptService>? logger = null)
    {
        _logger = logger ?? NullLogger<ProcessScriptService>.Instance;
    }

    public Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments = "") =>
        ExecuteAsync(interpreter, scriptPath, arguments, null, CancellationToken.None);

    public async Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments,
        Action<ScriptOutputLine>? onOutput, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(interpreter);
        ArgumentException.ThrowIfNullOrWhiteSpace(scriptPath);

        using var process = new Process { StartInfo = CreateStartInfo(interpreter, scriptPath, arguments) };
        var output = new StringBuilder();
        var error = new StringBuilder();
        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Script {Interpreter} {Script} could not start", interpreter, scriptPath);
            var message = $"Failed to execute: {ex.Message}";
            onOutput?.Invoke(new ScriptOutputLine(message, IsError: true));
            return new ScriptResult { ExitCode = -1, Error = message };
        }

        var stdout = PumpAsync(process.StandardOutput, output, isError: false, onOutput);
        var stderr = PumpAsync(process.StandardError, error, isError: true, onOutput);
        var cancelled = false;
        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            cancelled = true;
            Stop(process);
            await process.WaitForExitAsync(CancellationToken.None);
        }
        await Task.WhenAll(stdout, stderr);

        return new ScriptResult
        {
            ExitCode = process.ExitCode,
            Output = output.ToString(),
            Error = error.ToString(),
            WasCancelled = cancelled,
        };
    }

    /// <summary>Start info for <c>{interpreter} "{scriptPath}" {arguments}</c>, with no window and UTF-8 pipes.</summary>
    internal static ProcessStartInfo CreateStartInfo(string interpreter, string scriptPath, string arguments) => new()
    {
        FileName = interpreter,
        Arguments = $"{Quote(scriptPath)} {arguments}".Trim(),
        UseShellExecute = false,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        RedirectStandardInput = false,
        CreateNoWindow = true,
        StandardOutputEncoding = Encoding.UTF8,
        StandardErrorEncoding = Encoding.UTF8,
    };

    /// <summary>Quotes a path that contains spaces or quotes (Windows command-line rules).</summary>
    internal static string Quote(string path) =>
        path.Length > 0 && path.IndexOfAny([' ', '\t', '"']) < 0
            ? path
            : "\"" + path.Replace("\"", "\\\"") + "\"";

    private static async Task PumpAsync(StreamReader reader, StringBuilder buffer, bool isError, Action<ScriptOutputLine>? onOutput)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            buffer.Append(line).Append('\n');
            onOutput?.Invoke(new ScriptOutputLine(line, isError));
        }
    }

    private void Stop(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Already exited.
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            _logger.LogWarning(ex, "Could not stop script process {Pid}", process.Id);
        }
    }
}
