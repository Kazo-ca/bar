namespace Kazo.Bar.Infrastructure.Storage;

/// <summary>What <see cref="LegacyAppDataMigration.Migrate"/> did.</summary>
public enum LegacyAppDataMigrationOutcome
{
    /// <summary>No DevToolbar folder: nothing to copy.</summary>
    NoLegacyFolder,
    /// <summary>The DevToolbar folder already carries the marker of an earlier copy.</summary>
    AlreadyMigrated,
    /// <summary>The Kazo Bar folder already has files; nothing was overwritten.</summary>
    NewFolderInUse,
    /// <summary>Every file was copied to the Kazo Bar folder.</summary>
    Copied,
}

/// <summary>
/// One-time copy of the user data written under the DevToolbar name
/// (<c>%APPDATA%\DevToolbar</c>) into the Kazo Bar folder (<c>%APPDATA%\Kazo\Bar</c>).
/// The old folder is never deleted: it stays as a backup and receives a marker file.
/// </summary>
public static class LegacyAppDataMigration
{
    /// <summary>Written in the old folder once its content has been copied.</summary>
    public const string MarkerFileName = "migrated-to-kazo-bar.txt";

    private const string StagingSuffix = ".migrating";

    public static LegacyAppDataMigrationOutcome Migrate(string legacyRoot, string newRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(legacyRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(newRoot);
        legacyRoot = Path.GetFullPath(legacyRoot);
        newRoot = Path.GetFullPath(newRoot);

        if (!Directory.Exists(legacyRoot))
            return LegacyAppDataMigrationOutcome.NoLegacyFolder;
        if (File.Exists(Path.Combine(legacyRoot, MarkerFileName)))
            return LegacyAppDataMigrationOutcome.AlreadyMigrated;
        if (Directory.Exists(newRoot) && Directory.EnumerateFileSystemEntries(newRoot).Any())
            return LegacyAppDataMigrationOutcome.NewFolderInUse;

        // Copy into a staging folder first so a failure never leaves a half-filled Kazo Bar folder.
        var staging = newRoot + StagingSuffix;
        if (Directory.Exists(staging))
            Directory.Delete(staging, recursive: true);
        CopyDirectory(legacyRoot, staging);

        Directory.CreateDirectory(Path.GetDirectoryName(newRoot)!);
        if (Directory.Exists(newRoot))
            Directory.Delete(newRoot); // empty, checked above
        Directory.Move(staging, newRoot);

        File.WriteAllText(Path.Combine(legacyRoot, MarkerFileName),
            $"Copied to {newRoot} on {DateTimeOffset.Now:O}. This folder is kept as a backup and is no longer read." + Environment.NewLine);
        return LegacyAppDataMigrationOutcome.Copied;
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
