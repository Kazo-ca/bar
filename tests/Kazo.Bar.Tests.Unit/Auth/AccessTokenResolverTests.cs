namespace Kazo.Bar.Tests.Unit.Auth;

using System.Net;
using System.Text;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Auth;
using Kazo.Bar.Infrastructure.Providers;
using Kazo.Bar.Tests.Unit.Providers;

/// <summary>US2.4: a personal access token or an OAuth sign-in becomes the Authorization header.</summary>
[TestFixture]
public class AccessTokenResolverTests
{
    // NUnit reuses one fixture instance for every test: fresh state for each.
    private InstantClock _clock = null!;
    private MemorySecrets _secrets = null!;
    private OAuthSettings _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new InstantClock(new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero));
        _secrets = new MemorySecrets();
        _settings = new OAuthSettings { AzureDevOps = { ClientId = "app-id" }, GitHub = { ClientId = "gh-app" } };
    }

    private AccessTokenResolver Create(SequenceHttp http) =>
        new(_secrets, new OAuthTokenClient(http.Client(), _clock), () => _settings, _clock);

    private void SaveEntraSignIn(string refreshToken) =>
        _secrets.Values["devops"] = OAuthCredential.From(OAuthProvider.AzureDevOps, new OAuthTokens("old", refreshToken, null)).Serialize();

    [Test]
    public async Task A_personal_access_token_keeps_the_scheme_each_provider_expects()
    {
        _secrets.Values["gh"] = "ghp_pat";
        _secrets.Values["ado"] = "adopat";
        var resolver = new AccessTokenResolver(_secrets);

        var github = await resolver.GetAuthorizationAsync("gh", OAuthProvider.GitHub);
        var azure = await resolver.GetAuthorizationAsync("ado", OAuthProvider.AzureDevOps);

        Assert.That(github.ToString(), Is.EqualTo("Bearer ghp_pat"));
        Assert.That(azure.Scheme, Is.EqualTo("Basic"));
        Assert.That(Encoding.ASCII.GetString(Convert.FromBase64String(azure.Parameter!)), Is.EqualTo(":adopat"));
    }

    [Test]
    public async Task A_github_sign_in_without_expiry_is_used_as_is()
    {
        _secrets.Values["gh"] = OAuthCredential.From(OAuthProvider.GitHub, new OAuthTokens("gho_token", null, null)).Serialize();
        var http = new SequenceHttp();

        var header = await Create(http).GetAuthorizationAsync("gh", OAuthProvider.GitHub);

        Assert.That(header.ToString(), Is.EqualTo("Bearer gho_token"));
        Assert.That(http.Requests, Is.Empty);
    }

    [Test]
    public async Task An_entra_sign_in_is_refreshed_once_then_reused_until_it_nearly_expires()
    {
        // Arrange
        SaveEntraSignIn("rt-1");
        var http = new SequenceHttp().On("oauth2/v2.0/token",
            """{"access_token":"at-1","refresh_token":"rt-2","expires_in":3600}""",
            """{"access_token":"at-2","refresh_token":"rt-3","expires_in":3600}""");
        var resolver = Create(http);

        // Act
        var first = await resolver.GetAuthorizationAsync("devops", OAuthProvider.AzureDevOps);
        var again = await resolver.GetAuthorizationAsync("devops", OAuthProvider.AzureDevOps);
        _clock.Advance(TimeSpan.FromMinutes(59));
        var renewed = await resolver.GetAuthorizationAsync("devops", OAuthProvider.AzureDevOps);

        // Assert
        Assert.That(first.ToString(), Is.EqualTo("Bearer at-1"));
        Assert.That(again.ToString(), Is.EqualTo("Bearer at-1"));
        Assert.That(renewed.ToString(), Is.EqualTo("Bearer at-2"), "renewed 2 minutes before expiry");
        Assert.That(http.Count("oauth2/v2.0/token"), Is.EqualTo(2));
        Assert.That(http.Requests[0].Body, Does.Contain("grant_type=refresh_token").And.Contain("refresh_token=rt-1"));
        Assert.That(http.Requests[1].Body, Does.Contain("refresh_token=rt-2"), "the rotated refresh token is used next");
        Assert.That(_secrets.Values["devops"], Does.Contain("rt-3"), "the rotated refresh token is saved");
    }

    [Test]
    public void An_expired_sign_in_asks_to_sign_in_again()
    {
        SaveEntraSignIn("rt-old");
        var http = new SequenceHttp().On("oauth2/v2.0/token",
            (HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"AADSTS700082: The refresh token has expired"}"""));

        var ex = Assert.ThrowsAsync<ProviderException>(() => Create(http).GetAuthorizationAsync("devops", OAuthProvider.AzureDevOps));

        Assert.That(ex!.Message, Does.Contain("Sign in again"));
    }

    [Test]
    public void A_sign_in_for_another_provider_is_rejected()
    {
        _secrets.Values["gh"] = OAuthCredential.From(OAuthProvider.GitHub, new OAuthTokens("gho_token", null, null)).Serialize();

        var ex = Assert.ThrowsAsync<ProviderException>(() => Create(new SequenceHttp()).GetAuthorizationAsync("gh", OAuthProvider.AzureDevOps));

        Assert.That(ex!.Message, Does.Contain("not a Azure DevOps sign-in").Or.Contain("not an Azure DevOps sign-in"));
    }

    [Test]
    public void A_missing_secret_keeps_the_existing_message()
    {
        var ex = Assert.ThrowsAsync<ProviderException>(() => new AccessTokenResolver(_secrets).GetAuthorizationAsync("none", OAuthProvider.GitHub));

        Assert.That(ex!.Message, Does.Contain("No GitHub token is saved under 'none'"));
    }

    [Test]
    public async Task Azure_devops_work_items_are_read_with_the_signed_in_token()
    {
        // Arrange
        SaveEntraSignIn("rt-1");
        var http = new SequenceHttp()
            .On("oauth2/v2.0/token", """{"access_token":"at-1","refresh_token":"rt-2","expires_in":3600}""")
            .On("_apis/wit/wiql", """{"workItems":[]}""");
        var client = new AzureDevOpsWorkItemsClient(http.Client(), _secrets, Create(http));
        var source = new WorkItemSource { Provider = WorkItemProviderKind.AzureDevOps, Organization = "contoso", Project = "Alpha", SecretRef = "devops" };

        // Act
        var items = await client.RecentAsync(source);

        // Assert
        Assert.That(items, Is.Empty);
        Assert.That(http.Requests.Single(r => r.Url.Contains("_apis/wit/wiql")).Authorization, Is.EqualTo("Bearer at-1"));
    }
}
