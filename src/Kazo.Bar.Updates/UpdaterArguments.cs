namespace Kazo.Bar.Updates;

/// <summary>
/// Command line of Kazo.Bar.Updater.exe:
/// <code>
/// Kazo.Bar.Updater.exe [--install-dir &lt;folder&gt;] [--tag &lt;v0.2.0&gt;] [--force] [--no-start] [--stable-only]
/// </code>
/// </summary>
/// <param name="InstallFolder">Folder holding Kazo.Bar.Windows.exe; defaults to the updater's own folder.</param>
/// <param name="Tag">Release to install (the one the toolbar announced); null = newest release.</param>
/// <param name="Force">Install even when the release is not newer than the installed version.</param>
/// <param name="StartAfterUpdate">Start the toolbar once the update is applied (default).</param>
/// <param name="IncludePreReleases">Consider pre-releases (default while Kazo Bar is 0.x).</param>
/// <param name="Relocated">Internal: this copy already runs outside the install folder (see <see cref="ToCommandLine"/>).</param>
public sealed record UpdaterArguments(
    string InstallFolder,
    string? Tag,
    bool Force,
    bool StartAfterUpdate,
    bool IncludePreReleases,
    bool Relocated = false)
{
    /// <summary>The arguments to start another updater process with the same request.</summary>
    public IReadOnlyList<string> ToCommandLine()
    {
        var args = new List<string> { "--install-dir", InstallFolder };
        if (Tag != null) args.AddRange(["--tag", Tag]);
        if (Force) args.Add("--force");
        if (!StartAfterUpdate) args.Add("--no-start");
        if (!IncludePreReleases) args.Add("--stable-only");
        if (Relocated) args.Add("--relocated");
        return args;
    }

    /// <exception cref="ArgumentException">Unknown option or missing value.</exception>
    public static UpdaterArguments Parse(IReadOnlyList<string> args, string defaultInstallFolder)
    {
        var result = new UpdaterArguments(defaultInstallFolder, null, Force: false, StartAfterUpdate: true, IncludePreReleases: true);
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--install-dir":
                    result = result with { InstallFolder = Value(args, ref i) };
                    break;
                case "--tag":
                    result = result with { Tag = Value(args, ref i) };
                    break;
                case "--force":
                    result = result with { Force = true };
                    break;
                case "--no-start":
                    result = result with { StartAfterUpdate = false };
                    break;
                case "--stable-only":
                    result = result with { IncludePreReleases = false };
                    break;
                case "--relocated":
                    result = result with { Relocated = true };
                    break;
                default:
                    throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }
        return result;
    }

    private static string Value(IReadOnlyList<string> args, ref int i)
    {
        if (i + 1 >= args.Count || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Option '{args[i]}' needs a value.");
        return args[++i];
    }
}
