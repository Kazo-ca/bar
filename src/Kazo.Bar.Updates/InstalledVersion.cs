using System.Diagnostics;
using System.Reflection;

namespace Kazo.Bar.Updates;

/// <summary>Reads the Kazo Bar version (the <c>Version</c> MSBuild property, set by the release script).</summary>
public static class InstalledVersion
{
    /// <summary>Version of the toolbar in an install folder; null when unknown (folder without the toolbar).</summary>
    public static ReleaseVersion? FromInstallFolder(string installFolder)
    {
        var dll = Path.Combine(installFolder, UpdateConstants.BarAssemblyName);
        if (!File.Exists(dll)) return null;
        var info = FileVersionInfo.GetVersionInfo(dll);
        return ReleaseVersion.TryParse(info.ProductVersion, out var version) ? version : null;
    }

    /// <summary>Version of a running assembly (the toolbar itself).</summary>
    public static ReleaseVersion? FromAssembly(Assembly? assembly)
    {
        var text = assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return ReleaseVersion.TryParse(text, out var version) ? version : null;
    }
}
