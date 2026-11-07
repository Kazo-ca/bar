using System.Runtime.InteropServices;

namespace Kazo.Bar.Updates;

/// <summary>Asks GitHub whether a newer Kazo Bar release exists.</summary>
public sealed class UpdateChecker
{
    private readonly GitHubReleaseClient _client;
    private readonly Architecture _architecture;

    public UpdateChecker(GitHubReleaseClient client, Architecture? architecture = null)
    {
        _client = client;
        _architecture = architecture ?? RuntimeInformation.OSArchitecture;
    }

    /// <summary>
    /// Pre-releases are included while Kazo Bar has only pre-releases (0.x).
    /// Throws on network or GitHub errors: callers decide how quiet to be.
    /// </summary>
    public async Task<AvailableUpdate?> FindUpdateAsync(ReleaseVersion? installed, bool includePreReleases = true, CancellationToken cancellationToken = default)
    {
        var releases = await _client.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
        return UpdateSelector.SelectNewest(releases, installed, includePreReleases, _architecture);
    }

    public async Task<AvailableUpdate?> FindTagAsync(string tag, CancellationToken cancellationToken = default)
    {
        var releases = await _client.GetReleasesAsync(cancellationToken).ConfigureAwait(false);
        return UpdateSelector.SelectTag(releases, tag, _architecture);
    }
}
