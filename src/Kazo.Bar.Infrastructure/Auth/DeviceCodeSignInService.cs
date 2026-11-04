namespace Kazo.Bar.Infrastructure.Auth;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Providers;

/// <summary>
/// "Sign in with GitHub / Microsoft" for the provider settings (US2.4): device flow, then the
/// sign-in is saved in the secret store under the reference the provider already reads.
/// </summary>
public sealed class DeviceCodeSignInService : IOAuthSignInService
{
    /// <summary>RFC 8628 §3.5: on "slow_down" the interval grows by 5 seconds.</summary>
    private static readonly TimeSpan SlowDownStep = TimeSpan.FromSeconds(5);

    private readonly OAuthTokenClient _client;
    private readonly ISecretStore _secrets;
    private readonly AccessTokenResolver _resolver;
    private readonly Func<OAuthSettings> _settings;
    private readonly TimeProvider _time;

    public DeviceCodeSignInService(OAuthTokenClient client, ISecretStore secrets, AccessTokenResolver resolver,
        Func<OAuthSettings> settings, TimeProvider? time = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _time = time ?? TimeProvider.System;
    }

    public bool IsConfigured(OAuthProvider provider) => OAuthApp.FromSettings(_settings(), provider) != null;

    public Task<DeviceCodeChallenge> StartAsync(OAuthProvider provider, CancellationToken cancellationToken = default) =>
        _client.RequestDeviceCodeAsync(RequireApp(provider), cancellationToken);

    public async Task CompleteAsync(DeviceCodeChallenge challenge, string secretRef, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        if (string.IsNullOrWhiteSpace(secretRef))
            throw new ProviderException("No secret reference to save the sign-in under.");

        var app = RequireApp(challenge.Provider);
        var interval = challenge.Interval;
        while (true)
        {
            await Task.Delay(interval, _time, cancellationToken);
            if (_time.GetUtcNow() >= challenge.ExpiresAt)
                throw new ProviderException("The code expired before it was entered. Start the sign-in again.");

            var poll = await _client.PollAsync(app, challenge.DeviceCode, cancellationToken);
            switch (poll.Status)
            {
                case DevicePollStatus.Pending:
                    continue;
                case DevicePollStatus.SlowDown:
                    interval += SlowDownStep;
                    continue;
                case DevicePollStatus.Approved:
                    var value = OAuthCredential.From(challenge.Provider, poll.Tokens!).Serialize();
                    await _secrets.SetAsync(secretRef, value);
                    _resolver.Remember(secretRef, value, poll.Tokens!);
                    return;
                default:
                    throw new ProviderException(poll.Message ?? $"The {app.ServiceName} sign-in failed.");
            }
        }
    }

    public async Task<StoredCredentialKind> DescribeAsync(string secretRef)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            return StoredCredentialKind.None;
        var saved = await _secrets.GetAsync(secretRef);
        if (string.IsNullOrEmpty(saved))
            return StoredCredentialKind.None;
        return OAuthCredential.TryParse(saved, out _) ? StoredCredentialKind.OAuth : StoredCredentialKind.PersonalAccessToken;
    }

    private OAuthApp RequireApp(OAuthProvider provider) =>
        OAuthApp.FromSettings(_settings(), provider)
        ?? throw new ProviderException(provider == OAuthProvider.GitHub
            ? "Enter the client ID of your GitHub OAuth app in Settings → Sign-in apps first."
            : "Enter the client ID of your Microsoft Entra app in Settings → Sign-in apps first.");
}
