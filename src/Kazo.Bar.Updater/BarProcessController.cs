using System.ComponentModel;
using System.Diagnostics;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Updater;

/// <summary>Closes and starts the toolbar (Kazo.Bar.Windows.exe) of one install folder.</summary>
internal sealed class BarProcessController
{
    private static readonly TimeSpan GracefulTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan KillTimeout = TimeSpan.FromSeconds(10);

    private readonly string _installFolder;

    public BarProcessController(string installFolder)
    {
        _installFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(installFolder));
    }

    /// <summary>
    /// Asks every toolbar running from the install folder to close normally (named event, then WM_CLOSE),
    /// and kills it only if it is still there after 20 seconds.
    /// </summary>
    /// <returns>True when at least one toolbar was running.</returns>
    public async Task<bool> CloseAsync(CancellationToken cancellationToken)
    {
        var running = FindRunning();
        if (running.Count == 0) return false;

        UpdaterLog.Write($"Closing {running.Count} toolbar process(es).");
        if (EventWaitHandle.TryOpenExisting(UpdateConstants.ShutdownEventName, out var shutdown))
        {
            using (shutdown) shutdown.Set();
        }
        else
        {
            foreach (var process in running) process.CloseMainWindow();
        }

        foreach (var process in running)
        {
            using (process)
            {
                if (await WaitForExitAsync(process, GracefulTimeout, cancellationToken)) continue;
                UpdaterLog.Write($"Toolbar {process.Id} did not close in {GracefulTimeout.TotalSeconds} s; ending it.");
                try { process.Kill(entireProcessTree: true); }
                catch (InvalidOperationException) { }
                if (!await WaitForExitAsync(process, KillTimeout, cancellationToken))
                    throw new InvalidOperationException($"Kazo Bar (process {process.Id}) could not be closed.");
            }
        }

        if (EventWaitHandle.TryOpenExisting(UpdateConstants.ShutdownEventName, out var leftover))
        {
            using (leftover) leftover.Reset();
        }
        return true;
    }

    public void Start()
    {
        var exe = Path.Combine(_installFolder, UpdateConstants.BarExecutableName);
        UpdaterLog.Write($"Starting {exe}.");
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true, WorkingDirectory = _installFolder })?.Dispose();
    }

    private List<Process> FindRunning()
    {
        var result = new List<Process>();
        foreach (var process in Process.GetProcessesByName(UpdateConstants.BarProcessName))
        {
            try
            {
                var folder = Path.GetDirectoryName(process.MainModule?.FileName);
                if (folder != null && string.Equals(Path.TrimEndingDirectorySeparator(folder), _installFolder, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(process);
                    continue;
                }
            }
            catch (Win32Exception)
            {
                // Another user's process (no access to its path): not ours.
            }
            catch (InvalidOperationException)
            {
                // Exited meanwhile.
            }
            process.Dispose();
        }
        return result;
    }

    private static async Task<bool> WaitForExitAsync(Process process, TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
