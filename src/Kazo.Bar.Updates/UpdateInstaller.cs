using System.IO.Compression;

namespace Kazo.Bar.Updates;

/// <summary>
/// Replaces the files of a Kazo Bar install folder with the content of a release package.
///
/// Every current file is first <b>moved</b> into a backup folder inside the install folder (same volume,
/// so a move works even for the running updater's own files), then the new files are copied in.
/// Any failure moves the old files back. User data (%APPDATA%\Kazo\Bar) lives elsewhere and is never read
/// or written here; <see cref="EnsureSafeInstallFolder"/> refuses a folder that overlaps it.
/// </summary>
public sealed class UpdateInstaller
{
    /// <summary>Backups are named <c>.kazo-update-backup-yyyyMMddHHmmss</c> and deleted on a later run.</summary>
    public const string BackupFolderPrefix = ".kazo-update-backup";

    private readonly Action<string> _log;

    public UpdateInstaller(Action<string> log)
    {
        _log = log;
    }

    /// <summary>Extracts the zip into a fresh <paramref name="stagingFolder"/> and returns the folder holding the toolbar exe.</summary>
    /// <exception cref="InvalidDataException">The zip is not a Kazo Bar package.</exception>
    public static string ExtractPackage(string zipPath, string stagingFolder)
    {
        if (Directory.Exists(stagingFolder)) Directory.Delete(stagingFolder, recursive: true);
        Directory.CreateDirectory(stagingFolder);
        // ExtractToDirectory rejects entries that would land outside the folder ("zip slip").
        ZipFile.ExtractToDirectory(zipPath, stagingFolder);
        return FindContentRoot(stagingFolder);
    }

    /// <summary>The package root: the folder itself, or its single sub-folder, containing Kazo.Bar.Windows.exe.</summary>
    internal static string FindContentRoot(string folder)
    {
        if (File.Exists(Path.Combine(folder, UpdateConstants.BarExecutableName))) return folder;
        var directories = Directory.GetDirectories(folder);
        if (Directory.GetFiles(folder).Length == 0 && directories.Length == 1 &&
            File.Exists(Path.Combine(directories[0], UpdateConstants.BarExecutableName)))
            return directories[0];
        throw new InvalidDataException($"The package does not contain {UpdateConstants.BarExecutableName}.");
    }

    /// <summary>
    /// Refuses folders where moving every file away would be dangerous: a drive root, a folder without
    /// the toolbar exe, or a folder overlapping the user data folder.
    /// </summary>
    /// <exception cref="InvalidOperationException">The folder is not a Kazo Bar install folder.</exception>
    public static void EnsureSafeInstallFolder(string installFolder, string userDataFolder)
    {
        var install = Normalize(installFolder);
        var userData = Normalize(userDataFolder);

        if (Path.GetPathRoot(install) is { } root && string.Equals(Normalize(root), install, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Refusing to update a drive root ({installFolder}).");
        if (!File.Exists(Path.Combine(install, UpdateConstants.BarExecutableName)))
            throw new InvalidOperationException($"{installFolder} is not a Kazo Bar folder ({UpdateConstants.BarExecutableName} not found).");
        if (IsSameOrInside(install, userData) || IsSameOrInside(userData, install))
            throw new InvalidOperationException($"{installFolder} overlaps the user data folder {userDataFolder}; refusing to update it.");
    }

    /// <summary>Installs the files of <paramref name="contentRoot"/> into <paramref name="installFolder"/>, rolling back on failure.</summary>
    /// <returns>The backup folder holding the previous version.</returns>
    public string Apply(string contentRoot, string installFolder)
    {
        var install = Normalize(installFolder);
        var source = Normalize(contentRoot);
        var backup = Path.Combine(install, $"{BackupFolderPrefix}-{DateTime.UtcNow:yyyyMMddHHmmss}");
        var moved = new List<(string Original, string Backup)>();
        var copied = new List<string>();

        try
        {
            foreach (var file in EnumerateInstalledFiles(install))
            {
                var target = Path.Combine(backup, Path.GetRelativePath(install, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Move(file, target);
                moved.Add((file, target));
            }
            _log($"Moved {moved.Count} file(s) of the current version to {backup}.");

            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(install, Path.GetRelativePath(source, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(file, target, overwrite: false);
                copied.Add(target);
            }
            _log($"Copied {copied.Count} file(s) of the new version.");
        }
        catch (Exception ex)
        {
            _log($"Install failed, restoring the previous version: {ex}");
            Rollback(moved, copied);
            throw;
        }

        DeleteEmptyFolders(install);
        return backup;
    }

    /// <summary>Deletes backups left by earlier updates. Files still in use stay until the next run.</summary>
    public static void CleanupBackups(string installFolder, Action<string> log)
    {
        if (!Directory.Exists(installFolder)) return;
        foreach (var folder in Directory.GetDirectories(installFolder, BackupFolderPrefix + "*"))
        {
            try
            {
                Directory.Delete(folder, recursive: true);
                log($"Deleted old update backup {folder}.");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                log($"Old update backup {folder} still in use; will retry later.");
            }
        }
    }

    private void Rollback(List<(string Original, string Backup)> moved, List<string> copied)
    {
        foreach (var file in copied)
        {
            try { File.Delete(file); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { _log($"Rollback: could not delete {file}: {ex.Message}"); }
        }
        for (var i = moved.Count - 1; i >= 0; i--)
        {
            var (original, backup) = moved[i];
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(original)!);
                File.Move(backup, original, overwrite: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log($"Rollback: could not restore {original} from {backup}: {ex.Message}");
            }
        }
    }

    private static List<string> EnumerateInstalledFiles(string install) =>
        Directory.EnumerateFiles(install, "*", SearchOption.AllDirectories)
            .Where(file => !Path.GetRelativePath(install, file).StartsWith(BackupFolderPrefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

    private static void DeleteEmptyFolders(string install)
    {
        foreach (var folder in Directory.GetDirectories(install, "*", SearchOption.AllDirectories).OrderByDescending(f => f.Length))
        {
            if (Path.GetRelativePath(install, folder).StartsWith(BackupFolderPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                if (!Directory.EnumerateFileSystemEntries(folder).Any()) Directory.Delete(folder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private static string Normalize(string path) =>
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

    private static bool IsSameOrInside(string path, string folder) =>
        string.Equals(path, folder, StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
}
