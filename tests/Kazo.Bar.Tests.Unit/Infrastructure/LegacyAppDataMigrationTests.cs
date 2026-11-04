namespace Kazo.Bar.Tests.Unit.Infrastructure;

using Kazo.Bar.Infrastructure.Storage;

[TestFixture]
public class LegacyAppDataMigrationTests
{
    [Test]
    public void LegacyFolder_IsCopied_WithSubfolders_AndKeptWithAMarker()
    {
        using var temp = new TempDirectory();
        var legacy = Path.Combine(temp.Root, "DevToolbar");
        var target = Path.Combine(temp.Root, "Kazo", "Bar");
        WriteFile(legacy, "config.json", "{\"schemaVersion\":1}");
        WriteFile(legacy, Path.Combine("data", "time-2026-11.json"), "{}");
        WriteFile(legacy, Path.Combine("backups", "config.v0.json"), "{}");

        var outcome = LegacyAppDataMigration.Migrate(legacy, target);

        Assert.That(outcome, Is.EqualTo(LegacyAppDataMigrationOutcome.Copied));
        Assert.That(File.ReadAllText(Path.Combine(target, "config.json")), Is.EqualTo("{\"schemaVersion\":1}"));
        Assert.That(File.Exists(Path.Combine(target, "data", "time-2026-11.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(target, "backups", "config.v0.json")), Is.True);
        Assert.That(File.Exists(Path.Combine(legacy, "config.json")), Is.True, "the old folder stays as a backup");
        Assert.That(File.Exists(Path.Combine(legacy, LegacyAppDataMigration.MarkerFileName)), Is.True);
        Assert.That(File.Exists(Path.Combine(target, LegacyAppDataMigration.MarkerFileName)), Is.False);
        Assert.That(Directory.Exists(target + ".migrating"), Is.False);
    }

    [Test]
    public void SecondStart_DoesNotCopyAgain()
    {
        using var temp = new TempDirectory();
        var legacy = Path.Combine(temp.Root, "DevToolbar");
        var target = Path.Combine(temp.Root, "Kazo", "Bar");
        WriteFile(legacy, "config.json", "old");
        LegacyAppDataMigration.Migrate(legacy, target);
        File.WriteAllText(Path.Combine(target, "config.json"), "edited in Kazo Bar");
        Directory.Delete(target, recursive: true); // even if the user removes the new folder

        var outcome = LegacyAppDataMigration.Migrate(legacy, target);

        Assert.That(outcome, Is.EqualTo(LegacyAppDataMigrationOutcome.AlreadyMigrated));
        Assert.That(Directory.Exists(target), Is.False);
    }

    [Test]
    public void ExistingKazoBarData_IsNeverOverwritten()
    {
        using var temp = new TempDirectory();
        var legacy = Path.Combine(temp.Root, "DevToolbar");
        var target = Path.Combine(temp.Root, "Kazo", "Bar");
        WriteFile(legacy, "config.json", "old");
        WriteFile(target, "config.json", "new");

        var outcome = LegacyAppDataMigration.Migrate(legacy, target);

        Assert.That(outcome, Is.EqualTo(LegacyAppDataMigrationOutcome.NewFolderInUse));
        Assert.That(File.ReadAllText(Path.Combine(target, "config.json")), Is.EqualTo("new"));
        Assert.That(File.Exists(Path.Combine(legacy, LegacyAppDataMigration.MarkerFileName)), Is.False);
    }

    [Test]
    public void EmptyKazoBarFolder_IsFilled()
    {
        using var temp = new TempDirectory();
        var legacy = Path.Combine(temp.Root, "DevToolbar");
        var target = Path.Combine(temp.Root, "Kazo", "Bar");
        WriteFile(legacy, "config.json", "old");
        Directory.CreateDirectory(target);

        Assert.That(LegacyAppDataMigration.Migrate(legacy, target), Is.EqualTo(LegacyAppDataMigrationOutcome.Copied));
        Assert.That(File.ReadAllText(Path.Combine(target, "config.json")), Is.EqualTo("old"));
    }

    [Test]
    public void NoLegacyFolder_CreatesNothing()
    {
        using var temp = new TempDirectory();
        var target = Path.Combine(temp.Root, "Kazo", "Bar");

        var outcome = LegacyAppDataMigration.Migrate(Path.Combine(temp.Root, "DevToolbar"), target);

        Assert.That(outcome, Is.EqualTo(LegacyAppDataMigrationOutcome.NoLegacyFolder));
        Assert.That(Directory.Exists(target), Is.False);
    }

    [Test]
    public void LeftoverStagingFolder_FromAnInterruptedCopy_IsReplaced()
    {
        using var temp = new TempDirectory();
        var legacy = Path.Combine(temp.Root, "DevToolbar");
        var target = Path.Combine(temp.Root, "Kazo", "Bar");
        WriteFile(legacy, "config.json", "old");
        WriteFile(target + ".migrating", "partial.json", "half");

        Assert.That(LegacyAppDataMigration.Migrate(legacy, target), Is.EqualTo(LegacyAppDataMigrationOutcome.Copied));
        Assert.That(File.Exists(Path.Combine(target, "partial.json")), Is.False);
        Assert.That(File.ReadAllText(Path.Combine(target, "config.json")), Is.EqualTo("old"));
    }

    [Test]
    public void CurrentUserRoot_IsKazoBar_AndLegacyRoot_IsDevToolbar()
    {
        Assert.That(AppDataPaths.CurrentUserRoot, Does.EndWith(Path.Combine("Kazo", "Bar")));
        Assert.That(AppDataPaths.LegacyCurrentUserRoot, Does.EndWith("DevToolbar"));
        Assert.That(AppDataPaths.ForCurrentUser().Root, Is.EqualTo(Path.GetFullPath(AppDataPaths.CurrentUserRoot)));
    }

    private static void WriteFile(string root, string relativePath, string content)
    {
        var path = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }
}
