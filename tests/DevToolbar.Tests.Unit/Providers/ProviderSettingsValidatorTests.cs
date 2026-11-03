namespace DevToolbar.Tests.Unit.Providers;

using DevToolbar.Core.Configuration;
using DevToolbar.Core.Models;
using DevToolbar.Plugins.Providers;

[TestFixture]
public class ProviderSettingsValidatorTests
{
    [TestCase("contoso", "Alpha", null)]
    [TestCase("con/toso", "Alpha", "organization")]
    [TestCase("", "Alpha", "organization")]
    [TestCase("contoso", " ", "project")]
    public void AzureDevOps(string organization, string project, string? expectedError)
    {
        var error = ProviderSettingsValidator.Validate(new WorkItemSource
        {
            Provider = WorkItemProviderKind.AzureDevOps, Organization = organization, Project = project, SecretRef = "ref",
        });

        if (expectedError == null)
            Assert.That(error, Is.Null);
        else
            Assert.That(error, Does.Contain(expectedError));
    }

    [TestCase("contoso/alpha-api", true)]
    [TestCase("contoso", false)]
    [TestCase("contoso/alpha/extra", false)]
    [TestCase("../etc", false)]
    public void GitHubRepository(string repository, bool valid)
    {
        Assert.That(ProviderSettingsValidator.Validate(new GitHubTracking { Repository = repository, SecretRef = "ref" }) == null, Is.EqualTo(valid));
    }

    [Test]
    public void InvalidSecretRef_IsRejected()
    {
        Assert.That(ProviderSettingsValidator.Validate(new GitHubTracking { Repository = "a/b", SecretRef = "has space" }), Does.Contain("token reference"));
    }

    [Test]
    public void DefaultSecretRef_IsSharedByServicesOfTheSameRepository()
    {
        var project = new ProjectConfig { Id = "alpha" };
        var repository = new GitRepository { Name = "Alpha API" };

        Assert.That(ProviderSettingsValidator.DefaultSecretRef(project, repository, "github"), Is.EqualTo("alpha-alpha-api-github"));
    }

    [TestCase("https://github.com/contoso/alpha-api.git", "contoso/alpha-api")]
    [TestCase("git@github.com:contoso/alpha-api.git", "contoso/alpha-api")]
    [TestCase("https://dev.azure.com/contoso/_git/alpha", "")]
    public void GuessGitHubRepository_FromRemote(string remote, string expected)
    {
        Assert.That(WorkItemsSettingsPanel.GuessGitHubRepository(new GitRepository { RemoteUrl = remote }), Is.EqualTo(expected));
    }
}
