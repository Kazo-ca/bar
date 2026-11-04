namespace Kazo.Bar.Tests.Unit.Configuration;

using Kazo.Bar.Core.Configuration;

[TestFixture]
public class ProjectFactoryTests
{
    private static readonly string[] Plugins = ["git-tools", "work-items", "git-tools"];

    [Test]
    public void Create_LinksTheProjectToOneRepositoryAtTheFolder()
    {
        var project = ProjectFactory.Create("  Alpha API ", @" C:\src\alpha-api\ ", [], Plugins);

        Assert.That(project.Id, Is.EqualTo("alpha-api"));
        Assert.That(project.Name, Is.EqualTo("Alpha API"));
        Assert.That(project.EnabledPlugins, Is.EqualTo(new[] { "git-tools", "work-items" }));
        var repository = project.GitRepositories.Single();
        Assert.That(repository.Path, Is.EqualTo(@"C:\src\alpha-api\"));
        Assert.That(repository.Name, Is.EqualTo("alpha-api"));
    }

    [Test]
    public void Create_UsesFolderName_WhenNameIsEmpty()
    {
        var project = ProjectFactory.Create("", "/home/me/devtoolbar", [], Plugins);

        Assert.That(project.Name, Is.EqualTo("devtoolbar"));
        Assert.That(project.Id, Is.EqualTo("devtoolbar"));
    }

    [TestCase("Projet Été", "projet-ete")]
    [TestCase("***", "project")]
    [TestCase("a  b__c", "a-b-c")]
    public void UniqueId_IsASlug(string name, string expected)
    {
        Assert.That(ProjectFactory.UniqueId(name, []), Is.EqualTo(expected));
    }

    [Test]
    public void UniqueId_AddsSuffix_WhenAlreadyUsed()
    {
        Assert.That(ProjectFactory.UniqueId("Alpha", ["alpha", "ALPHA-2"]), Is.EqualTo("alpha-3"));
    }

    [TestCase("x", "", "The local folder is required.")]
    [TestCase("x", "C:\\a\"b", "The local folder contains invalid characters.")]
    public void Validate_RejectsBadFolder(string name, string path, string expected)
    {
        Assert.That(ProjectFactory.Validate(name, path), Is.EqualTo(expected));
        Assert.Throws<ArgumentException>(() => ProjectFactory.Create(name, path, [], Plugins));
    }

    [Test]
    public void Validate_RejectsLongName()
    {
        Assert.That(ProjectFactory.Validate(new string('a', 61), "/src/a"), Does.Contain("60"));
        Assert.That(ProjectFactory.Validate("ok", "/src/a"), Is.Null);
    }
}
