namespace Kazo.Bar.Tests.Unit.Providers;

using System.Net;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Providers;

[TestFixture]
public class GitHubIssuesClientTests
{
    private static readonly WorkItemSource Source = new()
    {
        Provider = WorkItemProviderKind.GitHub,
        Repository = "contoso/alpha-api",
        SecretRef = "alpha-github",
    };

    private const string IssueList = """
        [
          { "number": 12, "title": "Fix login", "state": "open", "html_url": "https://github.com/contoso/alpha-api/issues/12", "assignee": { "login": "ben" } },
          { "number": 13, "title": "A pull request", "state": "open", "html_url": "https://github.com/contoso/alpha-api/pull/13", "pull_request": {} }
        ]
        """;

    private static (GitHubIssuesClient Client, FakeHttp Http) Create(Action<FakeHttp> setup, bool withToken = true)
    {
        var http = new FakeHttp();
        setup(http);
        var secrets = new MemorySecrets();
        if (withToken)
            secrets.Values["alpha-github"] = "ghp_token";
        return (new GitHubIssuesClient(new GitHubApi(http.Client(), secrets)), http);
    }

    [Test]
    public async Task Recent_ListsOpenIssues_WithoutPullRequests_AndSendsTheToken()
    {
        var (client, http) = Create(h => h.On("repos/contoso/alpha-api/issues?", IssueList));

        var items = await client.RecentAsync(Source);

        var item = items.Single();
        Assert.That(item.Id, Is.EqualTo("12"));
        Assert.That(item.Title, Is.EqualTo("Fix login"));
        Assert.That(item.State, Is.EqualTo("Open"));
        Assert.That(item.AssignedTo, Is.EqualTo("ben"));
        Assert.That(item.Url, Is.EqualTo("https://github.com/contoso/alpha-api/issues/12"));
        Assert.That(http.Requests.Single().Authorization, Is.EqualTo("Bearer ghp_token"));
    }

    [Test]
    public async Task Search_ByNumber_ReadsThatIssue()
    {
        var (client, http) = Create(h => h.On("repos/contoso/alpha-api/issues/12",
            """{ "number": 12, "title": "Fix login", "state": "closed", "html_url": "u" }"""));

        var items = await client.SearchAsync(Source, "#12");

        Assert.That(items.Single().State, Is.EqualTo("Closed"));
        Assert.That(http.Requests.Single().Url, Does.EndWith("repos/contoso/alpha-api/issues/12"));
    }

    [Test]
    public async Task Search_ByText_UsesSearchApi_LimitedToTheRepository()
    {
        var (client, http) = Create(h => h.On("search/issues", """{ "items": [ { "number": 3, "title": "Login page", "state": "open", "html_url": "u" } ] }"""));

        var items = await client.SearchAsync(Source, "login & co");

        Assert.That(items.Single().Id, Is.EqualTo("3"));
        var url = Uri.UnescapeDataString(http.Requests.Single().Url);
        Assert.That(url, Does.Contain("q=login & co in:title repo:contoso/alpha-api is:issue"));
        Assert.That(http.Requests.Single().Url, Does.Contain("%26"));
    }

    [Test]
    public async Task Get_ReturnsNull_ForUnknownOrInvalidNumber()
    {
        var (client, _) = Create(_ => { });

        Assert.That(await client.GetAsync(Source, "999"), Is.Null);
        Assert.That(await client.GetAsync(Source, "abc"), Is.Null);
    }

    [Test]
    public void MissingToken_GivesActionableError()
    {
        var (client, http) = Create(_ => { }, withToken: false);

        var ex = Assert.ThrowsAsync<ProviderException>(() => client.RecentAsync(Source));
        Assert.That(ex!.Message, Does.Contain("No GitHub token"));
        Assert.That(http.Requests, Is.Empty);
    }

    [Test]
    public void RejectedToken_IsReported_WithoutTheToken()
    {
        var (client, _) = Create(h => h.On("issues", "{}", HttpStatusCode.Unauthorized));

        var ex = Assert.ThrowsAsync<ProviderException>(() => client.RecentAsync(Source));
        Assert.That(ex!.Message, Does.Contain("rejected the token"));
        Assert.That(ex.Message, Does.Not.Contain("ghp_token"));
    }
}
