using System.Net.Http;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Updater;

/// <summary>What the window shows. <paramref name="Fraction"/> null = indeterminate progress.</summary>
internal sealed record UpdateStatus(string Headline, string Detail, double? Fraction = null, bool Finished = false, bool Failed = false);

/// <summary>
/// Check → download and verify → extract → close the toolbar → install (rollback on failure) → start the toolbar.
/// The toolbar keeps running until the new version is downloaded and checked, so a slow or failed
/// download never leaves the user without it.
/// </summary>
internal sealed class UpdateWorkflow
{
    private readonly UpdaterArguments _args;
    private readonly HttpClient _http;
    private readonly IProgress<UpdateStatus> _status;
    private readonly string _workFolder;

    public UpdateWorkflow(UpdaterArguments args, HttpClient http, IProgress<UpdateStatus> status, string? workFolder = null)
    {
        _args = args;
        _http = http;
        _status = status;
        _workFolder = workFolder ?? UpdateConstants.DefaultWorkFolder;
    }

    /// <summary>Set once files start moving: cancelling is no longer allowed after this point.</summary>
    public bool IsPastPointOfNoReturn { get; private set; }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(_args.InstallFolder));
        var userData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Kazo", "Bar");
        UpdaterLog.Write($"Updater started: install={install}, tag={_args.Tag ?? "(newest)"}, force={_args.Force}.");

        UpdateInstaller.EnsureSafeInstallFolder(install, userData);
        UpdateInstaller.CleanupBackups(install, UpdaterLog.Write);

        _status.Report(new UpdateStatus("Checking for updates…", "Contacting GitHub."));
        var installed = InstalledVersion.FromInstallFolder(install);
        var update = await FindUpdateAsync(installed, cancellationToken);
        if (update == null)
        {
            var message = installed == null ? "No release package found for this PC." : $"Kazo Bar {installed} is up to date.";
            UpdaterLog.Write(message);
            _status.Report(new UpdateStatus("No update", message, 1, Finished: true));
            return;
        }

        var folder = Path.Combine(_workFolder, SafeName(update.Release.Tag));
        var zip = Path.Combine(folder, update.Package.Name);
        var label = $"Kazo Bar {update.Version}";
        _status.Report(new UpdateStatus($"Downloading {label}…", $"{update.Package.Name} ({update.Package.Size / 1_048_576.0:0.#} MB)", 0));
        await PackageDownloader.DownloadAsync(_http, update.Package, zip,
            new Progress<double>(p => _status.Report(new UpdateStatus($"Downloading {label}…", $"{p:P0}", p))), cancellationToken);
        UpdaterLog.Write($"Downloaded and verified {zip}.");

        _status.Report(new UpdateStatus($"Preparing {label}…", "Extracting the package."));
        var content = await Task.Run(() => UpdateInstaller.ExtractPackage(zip, Path.Combine(folder, "staging")), cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        IsPastPointOfNoReturn = true;

        var bar = new BarProcessController(install);
        _status.Report(new UpdateStatus($"Installing {label}…", "Closing Kazo Bar."));
        var wasRunning = await bar.CloseAsync(CancellationToken.None);

        _status.Report(new UpdateStatus($"Installing {label}…", "Replacing the program files. Your settings and data are not touched."));
        try
        {
            await Task.Run(() => new UpdateInstaller(UpdaterLog.Write).Apply(content, install));
        }
        catch (Exception ex)
        {
            UpdaterLog.Write($"Install failed: {ex}");
            if (wasRunning || _args.StartAfterUpdate) TryStart(bar);
            throw new InvalidOperationException($"The update could not be installed; the previous version was restored. {ex.Message}", ex);
        }
        UpdaterLog.Write($"Installed {update.Release.Tag}.");
        TryDelete(folder);

        if (_args.StartAfterUpdate)
        {
            _status.Report(new UpdateStatus($"Starting {label}…", ""));
            bar.Start();
        }
        _status.Report(new UpdateStatus($"{label} installed", installed == null ? "" : $"Updated from {installed}.", 1, Finished: true));
    }

    private async Task<AvailableUpdate?> FindUpdateAsync(ReleaseVersion? installed, CancellationToken cancellationToken)
    {
        var checker = new UpdateChecker(new GitHubReleaseClient(_http));
        if (_args.Tag == null)
            return await checker.FindUpdateAsync(_args.Force ? null : installed, _args.IncludePreReleases, cancellationToken);

        var tagged = await checker.FindTagAsync(_args.Tag, cancellationToken)
            ?? throw new InvalidOperationException($"Release {_args.Tag} has no package for this PC.");
        return _args.Force || installed == null || tagged.Version > installed ? tagged : null;
    }

    private static void TryStart(BarProcessController bar)
    {
        try { bar.Start(); }
        catch (Exception ex) { UpdaterLog.Write($"Could not restart the toolbar: {ex}"); }
    }

    private static void TryDelete(string folder)
    {
        try { Directory.Delete(folder, recursive: true); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { UpdaterLog.Write($"Could not delete {folder}: {ex.Message}"); }
    }

    private static string SafeName(string tag) =>
        string.Concat(tag.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
}
