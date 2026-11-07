using System.Runtime.InteropServices;

namespace Kazo.Bar.Updates;

/// <summary>Picks the release to install among the ones GitHub returns.</summary>
public static class UpdateSelector
{
    /// <summary>
    /// The newest non-draft release that is newer than <paramref name="installed"/> and has a package for
    /// <paramref name="architecture"/>; null when up to date. An unknown installed version accepts any release.
    /// </summary>
    public static AvailableUpdate? SelectNewest(
        IEnumerable<ReleaseInfo> releases,
        ReleaseVersion? installed,
        bool includePreReleases,
        Architecture architecture)
    {
        var suffix = UpdateConstants.PackageSuffix(architecture);
        AvailableUpdate? best = null;
        foreach (var release in releases)
        {
            if (release.IsDraft) continue;
            if (release.IsPreRelease && !includePreReleases) continue;
            if (!ReleaseVersion.TryParse(release.Tag, out var version)) continue;
            if (installed != null && !(version > installed)) continue;
            if (best != null && !(version > best.Version)) continue;

            var package = release.Assets.FirstOrDefault(a =>
                a.Name.StartsWith(UpdateConstants.PackagePrefix, StringComparison.OrdinalIgnoreCase) &&
                a.Name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));
            if (package == null) continue;

            best = new AvailableUpdate(release, version, package);
        }
        return best;
    }

    /// <summary>Same as <see cref="SelectNewest"/> but for one tag (the one the toolbar announced).</summary>
    public static AvailableUpdate? SelectTag(IEnumerable<ReleaseInfo> releases, string tag, Architecture architecture) =>
        SelectNewest(releases.Where(r => string.Equals(r.Tag, tag, StringComparison.OrdinalIgnoreCase)),
            installed: null, includePreReleases: true, architecture);
}
