namespace Kazo.Bar.Tests.Unit.Infrastructure;

using System.Text.Json.Nodes;
using Kazo.Bar.Infrastructure.Configuration;

[TestFixture]
public class ConfigV0ToV1MigrationTests
{
    private static JsonObject Parse(string json) => (JsonObject)JsonNode.Parse(json)!;

    [Test]
    public void Apply_RenamesPascalCaseRootProperties()
    {
        var result = new ConfigV0ToV1Migration().Apply(Parse("""{ "ActiveProjectId": "alpha", "Projects": [ { "id": "alpha" } ] }"""));

        Assert.That(result["activeProjectId"]!.GetValue<string>(), Is.EqualTo("alpha"));
        Assert.That(result["projects"]!.AsArray(), Has.Count.EqualTo(1));
        Assert.That(result.ContainsKey("Projects"), Is.False);
    }

    [Test]
    public void Apply_RenamesGitRepositories_AndRepositoryPathToLocalPath()
    {
        var result = new ConfigV0ToV1Migration().Apply(Parse("""
            { "Projects": [ { "id": "a", "gitRepositories": [ { "name": "api", "path": "C:\\src\\api" } ] } ] }
            """));

        var repository = result["projects"]![0]!["repositories"]![0]!;
        Assert.That(repository["localPath"]!.GetValue<string>(), Is.EqualTo(@"C:\src\api"));
        Assert.That(repository["path"], Is.Null);
        Assert.That(result["projects"]![0]!["gitRepositories"], Is.Null);
    }

    [Test]
    public void Apply_CreatesOneRepository_FromProjectFolder_WhenNoneConfigured()
    {
        var result = new ConfigV0ToV1Migration().Apply(Parse("""
            { "Projects": [ { "id": "a", "name": "Alpha", "path": "/src/alpha", "repositoryUrl": "https://x/a.git", "defaultBranch": "develop" } ] }
            """));

        var repositories = result["projects"]![0]!["repositories"]!.AsArray();
        Assert.That(repositories, Has.Count.EqualTo(1));
        Assert.That(repositories[0]!["name"]!.GetValue<string>(), Is.EqualTo("Alpha"));
        Assert.That(repositories[0]!["localPath"]!.GetValue<string>(), Is.EqualTo("/src/alpha"));
        Assert.That(repositories[0]!["remoteUrl"]!.GetValue<string>(), Is.EqualTo("https://x/a.git"));
        Assert.That(repositories[0]!["defaultBranch"]!.GetValue<string>(), Is.EqualTo("develop"));
    }

    [Test]
    public void Apply_KeepsSettingsSection_AndDoesNotModifyInput()
    {
        var input = Parse("""{ "settings": { "toolbarHeight": 64 }, "Projects": [] }""");
        var before = input.ToJsonString();

        var result = new ConfigV0ToV1Migration().Apply(input);

        Assert.That(result["settings"]!["toolbarHeight"]!.GetValue<int>(), Is.EqualTo(64));
        Assert.That(input.ToJsonString(), Is.EqualTo(before));
    }
}
