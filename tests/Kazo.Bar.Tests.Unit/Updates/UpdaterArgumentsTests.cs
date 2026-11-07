using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

public class UpdaterArgumentsTests
{
    [Test]
    public void Defaults_install_the_newest_release_in_the_updater_folder_and_restart()
    {
        var args = UpdaterArguments.Parse([], @"C:\Kazo");

        Assert.That(args, Is.EqualTo(new UpdaterArguments(@"C:\Kazo", null, Force: false, StartAfterUpdate: true, IncludePreReleases: true)));
    }

    [Test]
    public void Parses_every_option_and_round_trips()
    {
        var args = UpdaterArguments.Parse(["--install-dir", @"D:\Apps\Kazo Bar", "--TAG", "v0.2.0", "--force", "--no-start", "--stable-only", "--relocated"], "x");

        Assert.That(args, Is.EqualTo(new UpdaterArguments(@"D:\Apps\Kazo Bar", "v0.2.0", true, false, false, true)));
        Assert.That(UpdaterArguments.Parse(args.ToCommandLine(), "y"), Is.EqualTo(args));
    }

    [TestCase("--bogus")]
    [TestCase("--tag")]
    [TestCase("--install-dir", "--force")]
    public void Rejects_unknown_options_and_missing_values(params string[] args)
    {
        Assert.Throws<ArgumentException>(() => UpdaterArguments.Parse(args, "x"));
    }
}
