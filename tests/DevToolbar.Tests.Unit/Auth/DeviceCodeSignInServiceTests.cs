namespace DevToolbar.Tests.Unit.Auth;

using System.Net;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Auth;
using DevToolbar.Infrastructure.Providers;
using DevToolbar.Tests.Unit.Providers;

/// <summary>US2.4: "Sign in with GitHub / Microsoft" (device flow) with simulated HTTP answers.</summary>
[TestFixture]
public class DeviceCodeSignInServiceTests
{
    private const string GitHubDeviceCode = """
        {"device_code":"dev-1","user_code":"WDJB-MJHT","verification_uri":"https://github.com/login/device","expires_in":900,"interval":1}
        """;
    private const string EntraDeviceCode = """
        {"device_code":"dev-2","user_code":"F3K9QXY2","verification_uri":"https://microsoft.com/devicelogin","expires_in":900,"interval":1,"message":"To sign in..."}
        """;
    private const string Pending = """{"error":"authorization_pending"}""";

    // NUnit reuses one fixture instance for every test: fresh state for each.
    private InstantClock _clock = null!;
    private MemorySecrets _secrets = null!;
    private OAuthSettings _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _clock = new InstantClock(new DateTimeOffset(2026, 11, 4, 9, 0, 0, TimeSpan.Zero));
        _secrets = new MemorySecrets();
        _settings = new OAuthSettings
        {
            GitHub = { ClientId = "Ov23liGitHubApp" },
            AzureDevOps = { ClientId = "11111111-2222-3333-4444-555555555555", Tenant = "contoso.onmicrosoft.com" },
        };
    }

    private (DeviceCodeSignInService Service, AccessTokenResolver Resolver) Create(SequenceHttp http)
    {
        var client = new OAuthTokenClient(http.Client(), _clock);
        var resolver = new AccessTokenResolver(_secrets, client, () => _settings, _clock);
        return (new DeviceCodeSignInService(client, _secrets, resolver, () => _settings, _clock), resolver);
    }

    [Test]
    public async Task GitHub_device_flow_saves_the_token_once_the_user_approves()
    {
        // Arrange
        var http = new SequenceHttp()
            .On("login/device/code", GitHubDeviceCode)
            .On("login/oauth/access_token", Pending, """{"error":"slow_down","interval":10}""", Pending,
                """{"access_token":"gho_signed_in","token_type":"bearer","scope":"repo"}""");
        var (service, _) = Create(http);

        // Act
        var challenge = await service.StartAsync(OAuthProvider.GitHub);
        await service.CompleteAsync(challenge, "alpha-github");

        // Assert
        Assert.That(challenge.UserCode, Is.EqualTo("WDJB-MJHT"));
        Assert.That(challenge.VerificationUri, Is.EqualTo(new Uri("https://github.com/login/device")));
        Assert.That(http.Count("login/oauth/access_token"), Is.EqualTo(4));
        Assert.That(_clock.Delays.Select(d => d.TotalSeconds).ToArray(), Is.EqualTo(new double[] { 1, 1, 6, 6 }),
            "slow_down adds 5 seconds to the polling interval");
        Assert.That(await service.DescribeAsync("alpha-github"), Is.EqualTo(StoredCredentialKind.OAuth));
        var start = http.Requests.First();
        Assert.That(start.Body, Does.Contain("client_id=Ov23liGitHubApp").And.Contain("scope=repo"));
    }

    [Test]
    public async Task Entra_device_flow_keeps_the_access_token_in_memory_and_saves_the_refresh_token()
    {
        // Arrange
        var http = new SequenceHttp()
            .On("oauth2/v2.0/devicecode", EntraDeviceCode)
            .On("oauth2/v2.0/token", (HttpStatusCode.BadRequest, Pending),
                (HttpStatusCode.OK, """{"access_token":"eyJ.access","refresh_token":"rt-1","expires_in":3600,"token_type":"Bearer"}"""));
        var (service, resolver) = Create(http);

        // Act
        var challenge = await service.StartAsync(OAuthProvider.AzureDevOps);
        await service.CompleteAsync(challenge, "alpha-devops");
        var header = await resolver.GetAuthorizationAsync("alpha-devops", OAuthProvider.AzureDevOps);

        // Assert
        Assert.That(http.Requests[0].Url, Does.StartWith("https://login.microsoftonline.com/contoso.onmicrosoft.com/oauth2/v2.0/devicecode"));
        Assert.That(WebUtility.UrlDecode(http.Requests[0].Body!), Does.Contain("499b84ac-1321-427f-aa17-267ca6975798/.default offline_access"));
        Assert.That(_secrets.Values["alpha-devops"], Does.Contain("rt-1").And.Not.Contain("eyJ.access"));
        Assert.That(header.ToString(), Is.EqualTo("Bearer eyJ.access"));
        Assert.That(http.Count("oauth2/v2.0/token"), Is.EqualTo(2), "the first API call needs no refresh");
    }

    [TestCase("access_denied", "refused")]
    [TestCase("authorization_declined", "refused")]
    [TestCase("expired_token", "expired")]
    [TestCase("incorrect_client_credentials", "client ID")]
    public void A_refused_or_expired_sign_in_saves_nothing_and_explains_why(string error, string message)
    {
        var http = new SequenceHttp()
            .On("login/device/code", GitHubDeviceCode)
            .On("login/oauth/access_token", $$"""{"error":"{{error}}"}""");
        var (service, _) = Create(http);

        var ex = Assert.ThrowsAsync<ProviderException>(async () =>
            await service.CompleteAsync(await service.StartAsync(OAuthProvider.GitHub), "alpha-github"));

        Assert.That(ex!.Message, Does.Contain(message));
        Assert.That(_secrets.Values, Is.Empty);
    }

    [Test]
    public void Device_flow_turned_off_on_the_github_app_is_explained()
    {
        var http = new SequenceHttp().On("login/device/code", """{"error":"device_flow_disabled","error_description":"Device Flow must be explicitly enabled"}""");
        var (service, _) = Create(http);

        var ex = Assert.ThrowsAsync<ProviderException>(() => service.StartAsync(OAuthProvider.GitHub));

        Assert.That(ex!.Message, Does.Contain("Enable Device Flow"));
    }

    [TestCase("javascript:alert(1)")]
    [TestCase("http://github.com/login/device")]
    public void A_verification_link_that_is_not_https_is_refused(string link)
    {
        var http = new SequenceHttp().On("login/device/code",
            $$"""{"device_code":"d","user_code":"U","verification_uri":"{{link}}","expires_in":900,"interval":5}""");
        var (service, _) = Create(http);

        var ex = Assert.ThrowsAsync<ProviderException>(() => service.StartAsync(OAuthProvider.GitHub));

        Assert.That(ex!.Message, Does.Contain("unexpected answer"));
    }

    [Test]
    public void The_code_expiring_while_waiting_stops_the_sign_in()
    {
        var http = new SequenceHttp().On("login/oauth/access_token", Pending);
        var (service, _) = Create(http);
        var challenge = new DeviceCodeChallenge(OAuthProvider.GitHub, "CODE", new Uri("https://github.com/login/device"),
            _clock.GetUtcNow().AddSeconds(3), TimeSpan.FromSeconds(1), "dev-1");

        var ex = Assert.ThrowsAsync<ProviderException>(() => service.CompleteAsync(challenge, "alpha-github"));

        Assert.That(ex!.Message, Does.Contain("expired"));
        Assert.That(http.Count("login/oauth/access_token"), Is.EqualTo(2));
    }

    [Test]
    public void Without_a_client_id_the_sign_in_is_not_offered()
    {
        _settings.GitHub.ClientId = "  ";
        var (service, _) = Create(new SequenceHttp());

        Assert.That(service.IsConfigured(OAuthProvider.GitHub), Is.False);
        Assert.That(service.IsConfigured(OAuthProvider.AzureDevOps), Is.True);
        var ex = Assert.ThrowsAsync<ProviderException>(() => service.StartAsync(OAuthProvider.GitHub));
        Assert.That(ex!.Message, Does.Contain("Settings"));
    }

    [Test]
    public async Task Describe_tells_a_personal_access_token_from_a_sign_in()
    {
        var (service, _) = Create(new SequenceHttp());
        _secrets.Values["pat"] = "ghp_123";
        _secrets.Values["oauth"] = OAuthCredential.From(OAuthProvider.GitHub, new OAuthTokens("gho_1", null, null)).Serialize();

        Assert.That(await service.DescribeAsync("pat"), Is.EqualTo(StoredCredentialKind.PersonalAccessToken));
        Assert.That(await service.DescribeAsync("oauth"), Is.EqualTo(StoredCredentialKind.OAuth));
        Assert.That(await service.DescribeAsync("missing"), Is.EqualTo(StoredCredentialKind.None));
    }

    [Test]
    public void Cancelling_stops_the_wait_without_saving()
    {
        var http = new SequenceHttp().On("login/oauth/access_token", Pending);
        var (service, _) = Create(http);
        var challenge = new DeviceCodeChallenge(OAuthProvider.GitHub, "CODE", new Uri("https://github.com/login/device"),
            _clock.GetUtcNow().AddMinutes(15), TimeSpan.FromSeconds(1), "dev-1");
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();

        Assert.That(async () => await service.CompleteAsync(challenge, "alpha-github", cancel.Token),
            Throws.InstanceOf<OperationCanceledException>());
        Assert.That(_secrets.Values, Is.Empty);
    }
}
