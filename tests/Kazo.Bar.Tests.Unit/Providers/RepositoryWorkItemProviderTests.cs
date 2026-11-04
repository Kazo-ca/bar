namespace Kazo.Bar.Tests.Unit.Providers;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Providers;

[TestFixture]
public class RepositoryWorkItemProviderTests
{
    private sealed class OneProjectSettings(ProjectConfig project) : ISettingsService
    {
        public event Action<ProjectConfig>? OnActiveProjectChanged { add { } remove { } }
        public Task<IReadOnlyList<ProjectConfig>> GetProjectsAsync() => Task.FromResult<IReadOnlyList<ProjectConfig>>([project]);
        public ProjectConfig? GetActiveProject() => project;
        public Task SetActiveProjectAsync(string projectId) => Task.CompletedTask;
        public Task SaveProjectAsync(ProjectConfig p) => Task.CompletedTask;
    }

    private sealed class RecordingClient(WorkItemProviderKind kind) : IWorkItemSourceClient
    {
        public List<string> Queries { get; } = new();
        public WorkItemProviderKind Kind => kind;
        public Task<IReadOnlyList<WorkItem>> RecentAsync(WorkItemSource source, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<WorkItem>>([]);
        public Task<IReadOnlyList<WorkItem>> SearchAsync(WorkItemSource source, string text, CancellationToken cancellationToken = default)
        {
            Queries.Add(text);
            return Task.FromResult<IReadOnlyList<WorkItem>>([new WorkItem { Id = "1", Url = "https://x/1" }]);
        }
        public Task<WorkItem?> GetAsync(WorkItemSource source, string id, CancellationToken cancellationToken = default) =>
            Task.FromResult<WorkItem?>(null);
    }

    [Test]
    public async Task WithoutSource_ReturnsNothing_AndSaysNotConfigured()
    {
        var provider = new RepositoryWorkItemProvider(
            new OneProjectSettings(new ProjectConfig { GitRepositories = [new GitRepository { Path = "/a" }] }), []);

        Assert.That(await provider.SearchAsync("x"), Is.Empty);
        Assert.That(provider.ProviderName, Is.EqualTo("Not configured"));
    }

    [Test]
    public async Task UsesTheClientOfTheFirstRepositoryWithASource()
    {
        var github = new RecordingClient(WorkItemProviderKind.GitHub);
        var azure = new RecordingClient(WorkItemProviderKind.AzureDevOps);
        var project = new ProjectConfig
        {
            GitRepositories =
            [
                new GitRepository { Path = "/a" },
                new GitRepository { Path = "/b", WorkItems = new WorkItemSource { Provider = WorkItemProviderKind.AzureDevOps } },
            ],
        };
        var provider = new RepositoryWorkItemProvider(new OneProjectSettings(project), [github, azure]);

        var items = await provider.SearchAsync("bug");

        Assert.That(azure.Queries, Is.EqualTo(new[] { "bug" }));
        Assert.That(github.Queries, Is.Empty);
        Assert.That(provider.ProviderName, Is.EqualTo("Azure DevOps"));
        Assert.That(provider.GetWebUrl(items[0]), Is.EqualTo("https://x/1"));
    }
}
