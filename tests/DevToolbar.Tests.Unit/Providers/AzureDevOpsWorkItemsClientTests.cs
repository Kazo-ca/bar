namespace DevToolbar.Tests.Unit.Providers;

using System.Text;
using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Providers;

[TestFixture]
public class AzureDevOpsWorkItemsClientTests
{
    private static readonly WorkItemSource Source = new()
    {
        Provider = WorkItemProviderKind.AzureDevOps,
        Organization = "contoso",
        Project = "Alpha Team",
        SecretRef = "alpha-ado",
    };

    private const string Batch = """
        { "count": 2, "value": [
          { "id": 7, "fields": { "System.Title": "Older", "System.State": "Active", "System.WorkItemType": "Bug" } },
          { "id": 42, "fields": { "System.Title": "Newest", "System.State": "New", "System.WorkItemType": "User Story",
                                  "System.AssignedTo": { "displayName": "Benoit" } } }
        ] }
        """;

    private static (AzureDevOpsWorkItemsClient Client, FakeHttp Http) Create(Action<FakeHttp> setup)
    {
        var http = new FakeHttp();
        setup(http);
        var secrets = new MemorySecrets();
        secrets.Values["alpha-ado"] = "pat123";
        return (new AzureDevOpsWorkItemsClient(http.Client(), secrets), http);
    }

    [Test]
    public async Task Recent_RunsWiql_ThenReadsItemsInQueryOrder()
    {
        var (client, http) = Create(h => h
            .On("_apis/wit/wiql", """{ "workItems": [ { "id": 42 }, { "id": 7 } ] }""")
            .On("_apis/wit/workitems?ids=42,7", Batch));

        var items = await client.RecentAsync(Source);

        Assert.That(items.Select(i => i.Id), Is.EqualTo(new[] { "42", "7" }));
        Assert.That(items[0].AssignedTo, Is.EqualTo("Benoit"));
        Assert.That(items[0].Type, Is.EqualTo("User Story"));
        Assert.That(items[0].Url, Is.EqualTo("https://dev.azure.com/contoso/Alpha%20Team/_workitems/edit/42"));

        var wiql = http.Requests[0];
        Assert.That(wiql.Method, Is.EqualTo(HttpMethod.Post));
        Assert.That(wiql.Url, Does.StartWith("https://dev.azure.com/contoso/Alpha%20Team/_apis/wit/wiql"));
        Assert.That(wiql.Body, Does.Contain("@Me"));
        var expectedAuth = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(":pat123"));
        Assert.That(wiql.Authorization, Is.EqualTo(expectedAuth));
    }

    [Test]
    public async Task Search_EscapesQuotesInWiql()
    {
        var (client, http) = Create(h => h.On("_apis/wit/wiql", """{ "workItems": [] }"""));

        var items = await client.SearchAsync(Source, "it's broken");

        Assert.That(items, Is.Empty);
        var query = System.Text.Json.Nodes.JsonNode.Parse(http.Requests.Single().Body!)!["query"]!.GetValue<string>();
        Assert.That(query, Does.Contain("CONTAINS 'it''s broken'"));
    }

    [Test]
    public async Task Search_ByNumber_ReadsThatItem()
    {
        var (client, http) = Create(h => h.On("_apis/wit/workitems?ids=7", Batch));

        var items = await client.SearchAsync(Source, "7");

        Assert.That(items.Single().Title, Is.EqualTo("Older"));
        Assert.That(http.Requests.Single().Method, Is.EqualTo(HttpMethod.Get));
    }

    [Test]
    public void EscapeWiql_DoublesSingleQuotes()
    {
        Assert.That(AzureDevOpsWorkItemsClient.EscapeWiql("a'b''c"), Is.EqualTo("a''b''''c"));
    }
}
