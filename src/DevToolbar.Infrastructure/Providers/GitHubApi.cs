namespace DevToolbar.Infrastructure.Providers;

using System.Net.Http.Headers;
using DevToolbar.Core.Interfaces;

/// <summary>Builds authenticated requests to the GitHub REST API.</summary>
public sealed class GitHubApi
{
    public const string ServiceName = "GitHub";
    private static readonly Uri BaseAddress = new("https://api.github.com/");

    private readonly HttpClient _http;
    private readonly ISecretStore _secrets;

    public GitHubApi(HttpClient http, ISecretStore secrets)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _secrets = secrets ?? throw new ArgumentNullException(nameof(secrets));
    }

    /// <summary>GET <paramref name="relativeUrl"/> (already escaped) with the token stored under <paramref name="secretRef"/>.</summary>
    public async Task<T> GetAsync<T>(string relativeUrl, string secretRef, CancellationToken cancellationToken = default)
    {
        var token = await ProviderHttp.RequireTokenAsync(_secrets, secretRef, ServiceName);
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseAddress, relativeUrl));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DevToolbar", "1.0"));
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
