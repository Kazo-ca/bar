using System.Runtime.InteropServices;
using Kazo.Bar.Updates;
using static Kazo.Bar.Tests.Unit.Updates.UpdateTestData;

namespace Kazo.Bar.Tests.Unit.Updates;

public class UpdateSelectorTests
{
    private static readonly ReleaseVersion Installed = ReleaseVersion.Parse("0.1.0");

    [Test]
    public void Picks_the_newest_release_newer_than_the_installed_one()
    {
        var releases = new[] { Release("v0.1.0"), Release("v0.3.0"), Release("v0.2.0") };

        var update = UpdateSelector.SelectNewest(releases, Installed, includePreReleases: true, Architecture.X64);

        Assert.That(update!.Release.Tag, Is.EqualTo("v0.3.0"));
        Assert.That(update.Package.Name, Is.EqualTo("Kazo.Bar-0.3.0-win-x64.zip"));
    }

    [Test]
    public void Returns_null_when_up_to_date()
    {
        var update = UpdateSelector.SelectNewest([Release("v0.1.0")], Installed, true, Architecture.X64);

        Assert.That(update, Is.Null);
    }

    [Test]
    public void Skips_drafts_and_optionally_prereleases()
    {
        var releases = new[] { Release("v0.4.0", draft: true), Release("v0.3.0", prerelease: true), Release("v0.2.0") };

        Assert.That(UpdateSelector.SelectNewest(releases, Installed, true, Architecture.X64)!.Release.Tag, Is.EqualTo("v0.3.0"));
        Assert.That(UpdateSelector.SelectNewest(releases, Installed, false, Architecture.X64)!.Release.Tag, Is.EqualTo("v0.2.0"));
    }

    [Test]
    public void Skips_releases_without_a_package_for_this_architecture()
    {
        var releases = new[] { Release("v0.3.0", assets: "Kazo.Bar-0.3.0-win-arm64.zip"), Release("v0.2.0") };

        Assert.That(UpdateSelector.SelectNewest(releases, Installed, true, Architecture.X64)!.Release.Tag, Is.EqualTo("v0.2.0"));
        Assert.That(UpdateSelector.SelectNewest(releases, Installed, true, Architecture.Arm64)!.Release.Tag, Is.EqualTo("v0.3.0"));
    }

    [Test]
    public void Unknown_installed_version_accepts_the_newest_release()
    {
        var update = UpdateSelector.SelectNewest([Release("v0.1.0")], installed: null, true, Architecture.X64);

        Assert.That(update!.Release.Tag, Is.EqualTo("v0.1.0"));
    }

    [Test]
    public void Selects_a_specific_tag_case_insensitively()
    {
        var releases = new[] { Release("v0.3.0"), Release("v0.2.0") };

        Assert.That(UpdateSelector.SelectTag(releases, "V0.2.0", Architecture.X64)!.Version.ToString(), Is.EqualTo("0.2.0"));
        Assert.That(UpdateSelector.SelectTag(releases, "v9.9.9", Architecture.X64), Is.Null);
    }
}
