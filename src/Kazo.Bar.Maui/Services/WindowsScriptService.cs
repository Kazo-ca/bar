using System.Diagnostics;
using Kazo.Bar.Core.Interfaces;

namespace Kazo.Bar.Maui.Services;

/// <summary>
/// Real script service for desktop, executing scripts via process.
/// </summary>
public class WindowsScriptService : IScriptService
{
    public async Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments = "")
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = interpreter,
            Arguments = $"{scriptPath} {arguments}".Trim(),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await process.WaitForExitAsync();

            return new ScriptResult
            {
                ExitCode = process.ExitCode,
                Output = await outputTask,
                Error = await errorTask
            };
        }
        catch (Exception ex)
        {
            return new ScriptResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"Failed to execute: {ex.Message}"
            };
        }
    }
}
