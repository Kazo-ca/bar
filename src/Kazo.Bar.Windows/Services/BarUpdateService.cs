using System.Diagnostics;
using System.Net.Http;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Windows.Services;

/// <summary>
/// Silent update check every 30 minutes (first one a minute after start). Offline, GitHub down or
/// rate limited: one log line, nothing shown, the toolbar keeps running. When a newer release exists the
/// tray icon offers to install it, which starts Kazo.Bar.Updater.exe (it closes the toolbar, installs, restarts).
/// </summary>
public sealed class BarUpdateService : IAsyncDisposable
{
    private readonly PeriodicUpdateCheck _check;

    public BarUpdateService(HttpClient http)
    {
        CurrentVersion = InstalledVersion.FromAssembly(typeof(BarUpdateService).Assembly);
        var checker = new UpdateChecker(new GitHubReleaseClient(http));
        _check = new PeriodicUpdateCheck(ct => checker.FindUpdateAsync(CurrentVersion, includePreReleases: true, ct), Program.Log);
        _check.UpdateAvailable += update => UpdateAvailable?.Invoke(update);
    }

    /// <summary>Version of this toolbar (MSBuild <c>Version</c>).</summary>
    public ReleaseVersion? CurrentVersion { get; }

    /// <summary>Newest release found by the background check, or null.</summary>
    public AvailableUpdate? Latest => _check.Latest;

    /// <summary>Raised on a pool thread, once per new release.</summary>
    public event Action<AvailableUpdate>? UpdateAvailable;

    public static string UpdaterPath => Path.Combine(AppContext.BaseDirectory, UpdateConstants.UpdaterExecutableName);

    public static bool IsUpdaterInstalled => File.Exists(UpdaterPath);

    public void Start()
    {
        UpdateInstaller.CleanupBackups(AppContext.BaseDirectory, Program.Log);
#if DEBUG
        // A Debug build runs from bin\: never offer to overwrite it with a release.
        Program.Log($"Update check disabled in Debug builds (version {CurrentVersion}).");
#else
        Program.Log($"Update check every {UpdateConstants.CheckInterval.TotalMinutes} min (version {CurrentVersion}).");
        _check.Start();
#endif
    }

    /// <summary>Starts the updater for <paramref name="tag"/> (null = let it look for the newest release).</summary>
    /// <returns>False when the updater is missing or could not start (logged).</returns>
    public bool LaunchUpdater(string? tag)
    {
        if (!IsUpdaterInstalled)
        {
            Program.Log($"Updater not found: {UpdaterPath}.");
            return false;
        }
        try
        {
            var start = new ProcessStartInfo(UpdaterPath) { UseShellExecute = false, WorkingDirectory = AppContext.BaseDirectory };
            start.ArgumentList.Add("--install-dir");
            start.ArgumentList.Add(Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory));
            if (tag != null)
            {
                start.ArgumentList.Add("--tag");
                start.ArgumentList.Add(tag);
            }
            using var process = Process.Start(start);
            Program.Log($"Updater started (tag {tag ?? "newest"}).");
            return process != null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Program.Log($"Could not start the updater: {ex}");
            return false;
        }
    }

    public ValueTask DisposeAsync() => _check.DisposeAsync();
}
