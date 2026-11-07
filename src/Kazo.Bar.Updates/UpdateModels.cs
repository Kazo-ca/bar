namespace Kazo.Bar.Updates;

/// <summary>One downloadable file of a GitHub release.</summary>
/// <param name="Digest">GitHub's checksum, e.g. <c>sha256:7f0b…</c>; null on older releases.</param>
public sealed record ReleaseAsset(string Name, Uri DownloadUrl, long Size, string? Digest);

/// <summary>A published GitHub release.</summary>
public sealed record ReleaseInfo(
    string Tag,
    string Name,
    bool IsDraft,
    bool IsPreRelease,
    Uri PageUrl,
    IReadOnlyList<ReleaseAsset> Assets);

/// <summary>A release newer than the installed one, with the package to install.</summary>
public sealed record AvailableUpdate(ReleaseInfo Release, ReleaseVersion Version, ReleaseAsset Package);
