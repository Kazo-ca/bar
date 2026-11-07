using System.IO.Compression;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

public class UpdateInstallerTests
{
    private string _root = null!;
    private string _install = null!;
    private readonly List<string> _log = new();

    [SetUp]
    public void SetUp()
    {
        _root = Directory.CreateTempSubdirectory("kazo-install-").FullName;
        _install = Path.Combine(_root, "app");
        Write(_install, UpdateConstants.BarExecutableName, "old exe");
        Write(_install, "Old.Only.dll", "old");
        Write(_install, "wwwroot/css/app.css", "old css");
        Write(_install, "legacy/folder/file.txt", "old");
    }

    [TearDown]
    public void TearDown() => Directory.Delete(_root, recursive: true);

    [Test]
    public void Replaces_every_file_and_keeps_a_backup()
    {
        var package = Path.Combine(_root, "package");
        Write(package, UpdateConstants.BarExecutableName, "new exe");
        Write(package, "wwwroot/css/app.css", "new css");
        Write(package, "New.dll", "new");

        var backup = new UpdateInstaller(_log.Add).Apply(package, _install);

        Assert.That(Read(_install, UpdateConstants.BarExecutableName), Is.EqualTo("new exe"));
        Assert.That(Read(_install, "wwwroot/css/app.css"), Is.EqualTo("new css"));
        Assert.That(File.Exists(Path.Combine(_install, "New.dll")), Is.True);
        Assert.That(File.Exists(Path.Combine(_install, "Old.Only.dll")), Is.False, "stale files are removed");
        Assert.That(Directory.Exists(Path.Combine(_install, "legacy")), Is.False, "empty folders are removed");
        Assert.That(Read(backup, UpdateConstants.BarExecutableName), Is.EqualTo("old exe"));
        Assert.That(Path.GetDirectoryName(backup), Is.EqualTo(Path.GetFullPath(_install)));
    }

    [Test]
    public void Restores_the_previous_version_when_a_copy_fails()
    {
        var package = Path.Combine(_root, "package");
        Write(package, UpdateConstants.BarExecutableName, "new exe");
        // "legacy" is a folder in the install: copying a file with that name fails.
        Write(package, "legacy", "boom");

        Assert.That(() => new UpdateInstaller(_log.Add).Apply(package, _install), Throws.InstanceOf<IOException>().Or.InstanceOf<UnauthorizedAccessException>());

        Assert.That(Read(_install, UpdateConstants.BarExecutableName), Is.EqualTo("old exe"));
        Assert.That(Read(_install, "Old.Only.dll"), Is.EqualTo("old"));
        Assert.That(Read(_install, "legacy/folder/file.txt"), Is.EqualTo("old"));
        Assert.That(_log, Has.Some.Contains("restoring the previous version"));
    }

    [Test]
    public void Second_update_does_not_move_the_first_backup()
    {
        var package = Path.Combine(_root, "package");
        Write(package, UpdateConstants.BarExecutableName, "v2");
        var installer = new UpdateInstaller(_log.Add);
        var first = installer.Apply(package, _install);

        UpdateInstaller.CleanupBackups(_install, _log.Add);

        Assert.That(Directory.Exists(first), Is.False);
        Assert.That(Read(_install, UpdateConstants.BarExecutableName), Is.EqualTo("v2"));
    }

    [Test]
    public void Extracts_a_package_zipped_with_or_without_a_top_folder()
    {
        var flat = Zip("flat.zip", UpdateConstants.BarExecutableName, "a.dll");
        var nested = Zip("nested.zip", $"Kazo.Bar-0.2.0/{UpdateConstants.BarExecutableName}", "Kazo.Bar-0.2.0/a.dll");

        var flatRoot = UpdateInstaller.ExtractPackage(flat, Path.Combine(_root, "s1"));
        var nestedRoot = UpdateInstaller.ExtractPackage(nested, Path.Combine(_root, "s2"));

        Assert.That(File.Exists(Path.Combine(flatRoot, "a.dll")), Is.True);
        Assert.That(Path.GetFileName(nestedRoot), Is.EqualTo("Kazo.Bar-0.2.0"));
    }

    [Test]
    public void Rejects_a_zip_that_is_not_a_kazo_bar_package()
    {
        var zip = Zip("other.zip", "readme.txt");

        Assert.Throws<InvalidDataException>(() => UpdateInstaller.ExtractPackage(zip, Path.Combine(_root, "s")));
    }

    [Test]
    public void Install_folder_must_contain_the_toolbar_and_stay_away_from_user_data()
    {
        var userData = Path.Combine(_root, "AppData", "Kazo", "Bar");
        Directory.CreateDirectory(userData);

        Assert.DoesNotThrow(() => UpdateInstaller.EnsureSafeInstallFolder(_install, userData));
        Assert.Throws<InvalidOperationException>(() => UpdateInstaller.EnsureSafeInstallFolder(_root, userData), "no exe");
        Assert.Throws<InvalidOperationException>(() => UpdateInstaller.EnsureSafeInstallFolder(_install, Path.Combine(_install, "data")), "user data inside");
        Write(userData, UpdateConstants.BarExecutableName, "x");
        Assert.Throws<InvalidOperationException>(() => UpdateInstaller.EnsureSafeInstallFolder(userData, userData), "same folder");
    }

    private string Zip(string name, params string[] entries)
    {
        var path = Path.Combine(_root, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var entry in entries)
        {
            using var writer = new StreamWriter(zip.CreateEntry(entry).Open());
            writer.Write(entry);
        }
        return path;
    }

    private static void Write(string folder, string relative, string content)
    {
        var path = Path.Combine(folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    private static string Read(string folder, string relative) => File.ReadAllText(Path.Combine(folder, relative));
}
