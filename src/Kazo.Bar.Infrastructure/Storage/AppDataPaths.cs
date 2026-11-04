namespace Kazo.Bar.Infrastructure.Storage;

/// <summary>
/// Locations of every file Kazo Bar writes for the current user.
/// The root is <c>%APPDATA%\Kazo Bar</c> in the hosts; tests pass a temporary folder.
/// </summary>
public sealed class AppDataPaths
{
    public AppDataPaths(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.GetFullPath(root);
    }

    /// <summary>The per-user folder in the roaming application data folder.</summary>
    public static AppDataPaths ForCurrentUser() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevToolbar"));

    public string Root { get; }

    /// <summary>The single user configuration file (projects, repositories, settings).</summary>
    public string ConfigFile => Path.Combine(Root, "config.json");

    /// <summary>Global settings file written before schema version 1 (merged into <see cref="ConfigFile"/>).</summary>
    public string LegacyGlobalSettingsFile => Path.Combine(Root, "global-settings.json");

    /// <summary>Copies of files taken before a migration or when a file could not be read.</summary>
    public string BackupDirectory => Path.Combine(Root, "backups");

    /// <summary>Plugin and service data (one JSON file per namespace).</summary>
    public string DataDirectory => Path.Combine(Root, "data");
}
