using Kazo.Bar.Updates;

namespace Kazo.Bar.Tests.Unit.Updates;

public class InstalledVersionTests
{
    [Test]
    public void Reads_the_msbuild_version_of_an_assembly()
    {
        var version = InstalledVersion.FromAssembly(typeof(UpdateConstants).Assembly);

        Assert.That(version, Is.Not.Null);
        Assert.That(version!.IsPreRelease, Is.False);
    }

    [Test]
    public void Reads_the_product_version_of_the_toolbar_in_an_install_folder()
    {
        var folder = Directory.CreateTempSubdirectory("kazo-version-").FullName;
        try
        {
            Assert.That(InstalledVersion.FromInstallFolder(folder), Is.Null, "no toolbar");
            File.Copy(typeof(UpdateConstants).Assembly.Location, Path.Combine(folder, UpdateConstants.BarAssemblyName));

            Assert.That(InstalledVersion.FromInstallFolder(folder),
                Is.EqualTo(InstalledVersion.FromAssembly(typeof(UpdateConstants).Assembly)));
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
