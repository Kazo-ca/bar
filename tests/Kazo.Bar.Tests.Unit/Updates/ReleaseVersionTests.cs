using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

public class ReleaseVersionTests
{
    [TestCase("v0.1.0", "0.1.0")]
    [TestCase("0.2", "0.2.0")]
    [TestCase("1.2.3.0", "1.2.3")]
    [TestCase("0.2.0-beta.1", "0.2.0-beta.1")]
    [TestCase("0.1.0+8e0c948", "0.1.0")]
    [TestCase("V3", "3.0.0")]
    public void Parses_tags_and_assembly_versions(string text, string expected)
    {
        Assert.That(ReleaseVersion.TryParse(text, out var version), Is.True);
        Assert.That(version.ToString(), Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("latest")]
    [TestCase("v1.2.x")]
    [TestCase("1.0-")]
    public void Rejects_text_that_is_not_a_version(string? text)
    {
        Assert.That(ReleaseVersion.TryParse(text, out _), Is.False);
    }

    [TestCase("0.2.0", "0.1.0")]
    [TestCase("0.10.0", "0.9.0")]
    [TestCase("0.2.0", "0.2.0-rc.1")]
    [TestCase("0.2.0-rc.2", "0.2.0-rc.1")]
    [TestCase("0.2.0-rc.10", "0.2.0-rc.9")]
    [TestCase("0.2.0-rc", "0.2.0-beta")]
    [TestCase("0.2.0-beta.1", "0.2.0-beta")]
    public void Orders_versions_like_semver(string newer, string older)
    {
        Assert.That(ReleaseVersion.Parse(newer) > ReleaseVersion.Parse(older), Is.True);
        Assert.That(ReleaseVersion.Parse(older) < ReleaseVersion.Parse(newer), Is.True);
    }

    [Test]
    public void Same_numbers_with_different_metadata_are_equal()
    {
        Assert.That(ReleaseVersion.Parse("v0.1.0").CompareTo(ReleaseVersion.Parse("0.1.0+abc")), Is.Zero);
    }
}
