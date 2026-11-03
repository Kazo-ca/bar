namespace DevToolbar.Tests.Unit.Providers;

using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Configuration;
using DevToolbar.Infrastructure.Providers;
using DevToolbar.Infrastructure.Storage;
using DevToolbar.Tests.Unit.Infrastructure;

[TestFixture]
public class GitHubActionsServiceTests
{
    private static string Run(long id, string status, string? conclusion, string started) => $$"""
        { "id": {{id}}, "name": "CI", "run_number": {{id}}, "status": "{{status}}", "conclusion": {{(conclusion == null ? "null" : $"\"{conclusion}\"")}},
          "head_branch": "main", "html_url": "https://github.com/contoso/api/actions/runs/{{id}}",
          "created_at": "{{started}}", "updated_at": "{{started}}", "run_started_at": "{{started}}" }
        """;

    private static string Runs(params string[] runs) => $$"""{ "workflow_runs": [ {{string.Join(",", runs)}} ] }""";

    private static async Task<(GitHubActionsService Service, FakeHttp Http, TempDirectory Temp)> CreateAsync(string json)
    {
        var temp = new TempDirectory();
        var store = new UserConfigurationStore(temp.Paths);
        await store.UpdateAsync(c => c.Projects.Add(new ProjectConfig
        {
            Id = "p",
            GitRepositories =
            [
                new GitRepository { Name = "api", Path = "/api", GitHub = new GitHubTracking { Repository = "contoso/api", SecretRef = "gh" } },
                new GitRepository { Name = "off", Path = "/off", GitHub = new GitHubTracking { Repository = "contoso/off", TrackActions = false, SecretRef = "gh" } },
            ],
        }));
        var http = new FakeHttp().On("repos/contoso/api/actions/runs", json);
        var secrets = new MemorySecrets();
        secrets.Values["gh"] = "token";
        var service = new GitHubActionsService(new GitHubApi(http.Client(), secrets), new UserConfigSettingsService(store),
            new UserConfigGlobalSettingsService(store), new JsonStorageService(temp.Paths));
        return (service, http, temp);
    }

    [Test]
    public async Task Sessions_AreMapped_AndOnlyTrackedRepositoriesArePolled()
    {
        var (service, http, temp) = await CreateAsync(Runs(Run(2, "in_progress", null, "2026-11-03T10:00:00Z"), Run(1, "completed", "failure", "2026-11-03T09:00:00Z")));
        using var _ = temp;

        var sessions = await service.GetSessionsAsync("p");

        Assert.That(sessions.Select(s => s.Status), Is.EqualTo(new[] { CiCdStatus.InProgress, CiCdStatus.Completed }));
        Assert.That(sessions[1].Conclusion, Is.EqualTo("failure"));
        Assert.That(sessions[1].Name, Is.EqualTo("CI #1"));
        Assert.That(sessions[1].Url, Is.EqualTo("https://github.com/contoso/api/actions/runs/1"));
        Assert.That(sessions[0].Id, Is.EqualTo("contoso/api#2"));
        Assert.That(http.Requests.Select(r => r.Url), Has.All.Contains("contoso/api/"));
    }

    [Test]
    public async Task FirstRead_TreatsFinishedRunsAsRead_ThenNewResultsAreUnread()
    {
        var (service, http, temp) = await CreateAsync(Runs(Run(1, "completed", "success", "2026-11-03T09:00:00Z")));
        using var _ = temp;

        await service.GetSessionsAsync("p");
        Assert.That(service.GetUnreadCount("p"), Is.EqualTo(0));

        http.On("repos/contoso/api/actions/runs", Runs(Run(2, "completed", "failure", "2026-11-03T10:00:00Z"), Run(1, "completed", "success", "2026-11-03T09:00:00Z")));
        var changed = await service.RefreshAsync("p", CancellationToken.None);

        Assert.That(changed, Is.True);
        Assert.That(service.GetUnreadCount("p"), Is.EqualTo(1));
    }

    [Test]
    public async Task MarkAsRead_IsSaved_AndSurvivesARestart()
    {
        var (service, http, temp) = await CreateAsync(Runs(Run(1, "completed", "success", "2026-11-03T09:00:00Z")));
        using var _ = temp;
        await service.GetSessionsAsync("p");
        http.On("repos/contoso/api/actions/runs", Runs(Run(2, "completed", "success", "2026-11-03T10:00:00Z"), Run(1, "completed", "success", "2026-11-03T09:00:00Z")));
        await service.RefreshAsync("p", CancellationToken.None);

        service.MarkAsRead("contoso/api#2");
        Assert.That(service.GetUnreadCount("p"), Is.EqualTo(0));

        // Let the background save finish, then read with a new service (same files).
        for (var i = 0; i < 50 && (await new JsonStorageService(temp.Paths).GetAsync<GitHubActionsService.ReadState>(GitHubActionsService.StorageNamespace, "read:p"))?.Ids.Contains("contoso/api#2") != true; i++)
            await Task.Delay(20);
        var store = new UserConfigurationStore(temp.Paths);
        var restarted = new GitHubActionsService(new GitHubApi(http.Client(), new MemorySecrets { Values = { ["gh"] = "token" } }),
            new UserConfigSettingsService(store), new UserConfigGlobalSettingsService(store), new JsonStorageService(temp.Paths));
        await restarted.GetSessionsAsync("p");
        Assert.That(restarted.GetUnreadCount("p"), Is.EqualTo(0));
    }

    [Test]
    public async Task PollFailure_IsKeptInLastError_InsteadOfThrowing()
    {
        var (service, http, temp) = await CreateAsync(Runs());
        using var _ = temp;
        http.On("repos/contoso/api/actions/runs", "{}", System.Net.HttpStatusCode.Unauthorized);

        var sessions = await service.GetSessionsAsync("p");

        Assert.That(sessions, Is.Empty);
        Assert.That(service.LastError, Does.Contain("rejected the token"));
    }

    [Test]
    public async Task PollingInterval_HasAMinimum()
    {
        var (service, _, temp) = await CreateAsync(Runs());
        using var _t = temp;

        Assert.That(service.PollingInterval, Is.EqualTo(TimeSpan.FromSeconds(60)));
    }
}
