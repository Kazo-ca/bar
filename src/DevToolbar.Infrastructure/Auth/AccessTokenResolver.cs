namespace DevToolbar.Infrastructure.Auth;

using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Providers;

/// <summary>
/// Turns what is saved under a <c>secretRef</c> into the Authorization header of a provider call (US2.4):
/// a personal access token as before (Bearer for GitHub, Basic for Azure DevOps), or an OAuth sign-in
/// (Bearer). Access tokens that expire are kept in memory and renewed with the saved refresh token
/// a little before they expire; a rotated refresh token is saved back.
/// </summary>
public sealed class AccessTokenResolver
{
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(2);

    private readonly ISecretStore _secrets;
    private readonly OAuthTokenClient? _client;
    private readonly Func<OAuthSettings> _settings;
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, (string SavedValue, OAuthTokens Tokens)> _cache = new();
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    /// <param name="client">Null when the host cannot refresh (OAuth sign-ins then need a token that does not expire).</param>
    /// <param name="settings">Client IDs entered in Settings.</param>
    public AccessTokenResolver(ISecretStore secrets, OAuthTokenClient? client = null,
        Func<OAuthSettings>? settings = null, TimeProvider? time = null)
    {
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
        _client = client;
        _settings = settings ?? (() => new OAuthSettings());
        _time = time ?? TimeProvider.System;
    }

    public async Task<AuthenticationHeaderValue> GetAuthorizationAsync(string secretRef, OAuthProvider provider,
        CancellationToken cancellationToken = default)
    {
        var service = provider == OAuthProvider.GitHub ? "GitHub" : "Azure DevOps";
        var saved = await ProviderHttp.RequireTokenAsync(_secrets, secretRef, service);
        if (!OAuthCredential.TryParse(saved, out var credential))
        {
            return provider == OAuthProvider.GitHub
                ? new AuthenticationHeaderValue("Bearer", saved)
                : new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($":{saved}")));
        }

        if (credential.Provider != provider)
            throw new ProviderException($"The sign-in saved under '{secretRef}' is not a {service} sign-in. Sign in again in the plugin settings.");
        if (string.IsNullOrEmpty(credential.RefreshToken))
            return new AuthenticationHeaderValue("Bearer", credential.AccessToken);

        var tokens = await GetFreshTokensAsync(secretRef, saved, credential, cancellationToken);
        return new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
    }

    /// <summary>Keeps the tokens of a sign-in that was just saved, so the first call needs no refresh.</summary>
    public void Remember(string secretRef, string savedValue, OAuthTokens tokens) =>
        _cache[secretRef] = (savedValue, tokens);

    private bool IsFresh(string secretRef, string saved, out OAuthTokens tokens)
    {
        tokens = null!;
        if (!_cache.TryGetValue(secretRef, out var cached) || cached.SavedValue != saved)
            return false;
        tokens = cached.Tokens;
        return tokens.ExpiresAt is not { } expiresAt || expiresAt - RenewBefore > _time.GetUtcNow();
    }

    private async Task<OAuthTokens> GetFreshTokensAsync(string secretRef, string saved, OAuthCredential credential,
        CancellationToken cancellationToken)
    {
        if (IsFresh(secretRef, saved, out var tokens))
            return tokens;

        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            // Another call may have refreshed (and rotated the saved value) while this one waited.
            var current = await _secrets.GetAsync(secretRef) ?? saved;
            if (IsFresh(secretRef, current, out tokens))
                return tokens;
            if (current != saved && OAuthCredential.TryParse(current, out var updated))
                credential = updated;

            var app = OAuthApp.FromSettings(_settings(), credential.Provider);
            var service = credential.Provider == OAuthProvider.GitHub ? "GitHub" : "Azure DevOps";
            if (_client == null || app == null)
                throw new ProviderException($"The {service} sign-in cannot be renewed: the client ID is missing in Settings → Sign-in apps.");

            tokens = await _client.RefreshAsync(app, credential.RefreshToken!, cancellationToken);
            var renewed = OAuthCredential.From(credential.Provider, tokens with { RefreshToken = tokens.RefreshToken ?? credential.RefreshToken });
            var value = renewed.Serialize();
            if (value != current)
                await _secrets.SetAsync(secretRef, value);
            _cache[secretRef] = (value, tokens);
            return tokens;
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
