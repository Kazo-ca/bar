namespace Kazo.Bar.Infrastructure.Providers;

using System.Net.Http.Headers;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Infrastructure.Auth;

/// <summary>Builds authenticated requests to the GitHub REST API.</summary>
public sealed class GitHubApi
{
    public const string ServiceName = "GitHub";
    private static readonly Uri BaseAddress = new("https://api.github.com/");

    private readonly HttpClient _http;
    private readonly AccessTokenResolver _tokens;

    /// <param name="tokens">Reads a personal access token or an OAuth sign-in (US2.4); a resolver without refresh by default.</param>
    public GitHubApi(HttpClient http, ISecretStore secrets, AccessTokenResolver? tokens = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        ArgumentNullException.ThrowIfNull(secrets);
        _tokens = tokens ?? new AccessTokenResolver(secrets);
    }

    /// <summary>GET <paramref name="relativeUrl"/> (already escaped) with the token stored under <paramref name="secretRef"/>.</summary>
    public async Task<T> GetAsync<T>(string relativeUrl, string secretRef, CancellationToken cancellationToken = default)
    {
        var authorization = await _tokens.GetAuthorizationAsync(secretRef, OAuthProvider.GitHub, cancellationToken);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, relativeUrl));
        request.Headers.Authorization = authorization;
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("KazoBar", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        return await ProviderHttp.SendAsync<T>(_http, request, ServiceName, cancellationToken);
    }

    /// <summary><c>repos/{owner}/{name}</c> with each part escaped.</summary>
    public static string RepositoryPath(string ownerAndName)
    {
        var parts = ownerAndName.Split('/', 2);
        if (parts.Length != 2)
            throw new ProviderException("The GitHub repository is written owner/name.");
        return $"repos/{Uri.EscapeDataString(parts[0])}/{Uri.EscapeDataString(parts[1])}";
    }
}
