using System.Net;
using Kazo.Bar.Tests.Unit.Providers;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

public class GitHubReleaseClientTests
{
    [Test]
    public async Task Reads_releases_and_assets_from_the_public_api()
    {
        var http = new FakeHttp().On("/repos/Kazo-ca/bar/releases", UpdateTestData.ReleasesJson);
        var client = new GitHubReleaseClient(http.Client());

        var releases = await client.GetReleasesAsync();

        Assert.That(releases.Select(r => r.Tag), Is.EqualTo(new[] { "v0.2.0-beta.1", "v0.1.0" }));
        var beta = releases[0];
        Assert.That(beta.IsPreRelease, Is.True);
        Assert.That(beta.Assets.Single().Digest, Is.EqualTo("sha256:abc"));
        Assert.That(releases[1].Assets.Single().Size, Is.EqualTo(86036007));
        Assert.That(releases[1].Assets.Single().Digest, Is.Null);
        Assert.That(http.Requests.Single().Authorization, Is.Null, "no token is sent");
    }

    [Test]
    public void Throws_on_rate_limit_so_the_caller_can_stay_quiet()
    {
        var http = new FakeHttp().On("/releases", "{\"message\":\"API rate limit exceeded\"}", HttpStatusCode.Forbidden);
        var client = new GitHubReleaseClient(http.Client());

        Assert.ThrowsAsync<HttpRequestException>(() => client.GetReleasesAsync());
    }
}
