namespace DevToolbar.Web.Mocks;

using DevToolbar.Core.Interfaces;

/// <summary>
/// Simulated "Sign in with GitHub / Microsoft" for the Web test host (US2.4): no network call;
/// the code is "WDJB-MJHT" and the sign-in is approved after a short delay.
/// The client IDs entered in Settings decide whether the button is enabled, as in the WPF host.
/// </summary>
public class MockOAuthSignInService(ISecretStore secrets, IGlobalSettingsService settings) : IOAuthSignInService
{
    public const string UserCode = "WDJB-MJHT";
    private static readonly TimeSpan ApprovalDelay = TimeSpan.FromMilliseconds(1200);

    public bool IsConfigured(OAuthProvider provider)
    {
        var app = provider == OAuthProvider.GitHub ? settings.Current.OAuth.GitHub : settings.Current.OAuth.AzureDevOps;
        return !string.IsNullOrWhiteSpace(app.ClientId);
    }

    public Task<DeviceCodeChallenge> StartAsync(OAuthProvider provider, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(provider))
            throw new InvalidOperationException("Enter the client ID in Settings → Sign-in apps first.");
        var uri = provider == OAuthProvider.GitHub
            ? new Uri("https://github.com/login/device")
            : new Uri("https://microsoft.com/devicelogin");
        return Task.FromResult(new DeviceCodeChallenge(provider, UserCode, uri,
            DateTimeOffset.UtcNow.AddMinutes(15), TimeSpan.FromSeconds(5), "simulated-device-code"));
    }

    public async Task CompleteAsync(DeviceCodeChallenge challenge, string secretRef, CancellationToken cancellationToken = default)
    {
        await Task.Delay(ApprovalDelay, cancellationToken);
        var provider = challenge.Provider == OAuthProvider.GitHub ? "gitHub" : "azureDevOps";
        await secrets.SetAsync(secretRef, $$"""{"kind":"oauth","provider":"{{provider}}","accessToken":"simulated"}""");
    }

    public async Task<StoredCredentialKind> DescribeAsync(string secretRef)
    {
        var saved = string.IsNullOrWhiteSpace(secretRef) ? null : await secrets.GetAsync(secretRef);
        return string.IsNullOrEmpty(saved) ? StoredCredentialKind.None
            : saved.StartsWith('{') ? StoredCredentialKind.OAuth
            : StoredCredentialKind.PersonalAccessToken;
    }
}
