using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

internal static class UpdateTestData
{
    public static ReleaseInfo Release(string tag, bool prerelease = false, bool draft = false, params string[] assets) =>
        new(tag, tag, draft, prerelease, new Uri($"https://github.com/Kazo-ca/bar/releases/tag/{tag}"),
            (assets.Length == 0 ? [$"Kazo.Bar-{tag.TrimStart('v')}-win-x64.zip"] : assets)
                .Select(name => new ReleaseAsset(name, new Uri($"https://github.com/Kazo-ca/bar/releases/download/{tag}/{name}"), 100, null))
                .ToList());

    public const string ReleasesJson = """
        [
          {
            "tag_name": "v0.2.0-beta.1", "name": "Kazo Bar 0.2.0 beta", "draft": false, "prerelease": true,
            "html_url": "https://github.com/Kazo-ca/bar/releases/tag/v0.2.0-beta.1",
            "assets": [
              { "name": "Kazo.Bar-0.2.0-beta.1-win-x64.zip", "size": 1234,
                "browser_download_url": "https://github.com/Kazo-ca/bar/releases/download/v0.2.0-beta.1/Kazo.Bar-0.2.0-beta.1-win-x64.zip",
                "digest": "sha256:abc" }
            ]
          },
          {
            "tag_name": "v0.1.0", "name": "Kazo Bar 0.1.0", "draft": false, "prerelease": true,
            "html_url": "https://github.com/Kazo-ca/bar/releases/tag/v0.1.0",
            "assets": [
              { "name": "Kazo.Bar-0.1.0-win-x64.zip", "size": 86036007,
                "browser_download_url": "https://github.com/Kazo-ca/bar/releases/download/v0.1.0/Kazo.Bar-0.1.0-win-x64.zip" }
            ]
          },
          { "name": "missing tag" }
        ]
        """;
}
