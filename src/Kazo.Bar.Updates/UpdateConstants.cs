using System.Runtime.InteropServices;

namespace Kazo.Bar.Updates;

/// <summary>Names shared by the toolbar and the updater.</summary>
public static class UpdateConstants
{
    public const string GitHubOwner = "Kazo-ca";
    public const string GitHubRepository = "bar";

    /// <summary>The toolbar executable (WPF host) in the install folder.</summary>
    public const string BarExecutableName = "Kazo.Bar.Windows.exe";
    public const string BarProcessName = "Kazo.Bar.Windows";
    public const string BarAssemblyName = "Kazo.Bar.Windows.dll";

    public const string UpdaterExecutableName = "Kazo.Bar.Updater.exe";

    /// <summary>
    /// Named event (per Windows session) the toolbar waits on; the updater sets it to ask
    /// the toolbar to close normally (time tracking is saved) before files are replaced.
    /// </summary>
    public const string ShutdownEventName = @"Local\Kazo.Bar.Windows.Shutdown";

    /// <summary>Background check period in the toolbar.</summary>
    public static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(30);

    /// <summary>Delay before the first background check, so start-up stays fast.</summary>
    public static readonly TimeSpan FirstCheckDelay = TimeSpan.FromMinutes(1);

    /// <summary>Release asset for this machine, e.g. <c>Kazo.Bar-0.2.0-win-x64.zip</c>.</summary>
    public static string PackageSuffix(Architecture architecture) => architecture switch
    {
        Architecture.Arm64 => "-win-arm64.zip",
        _ => "-win-x64.zip",
    };

    public const string PackagePrefix = "Kazo.Bar-";

    /// <summary>
    /// Downloads and extracted packages: %LOCALAPPDATA%\Kazo\Bar\updates.
    /// User data in %APPDATA%\Kazo\Bar is never touched by an update.
    /// </summary>
    public static string DefaultWorkFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kazo", "Bar", "updates");
}
