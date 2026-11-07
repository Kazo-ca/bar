using System.Net.Http.Headers;
using System.Text.Json;

namespace Kazo.Bar.Updates;

/// <summary>Reads the releases of the Kazo Bar repository from the public GitHub API (no token needed).</summary>
public sealed class GitHubReleaseClient
{
    private const int PageSize = 30;
    private readonly HttpClient _http;
    private readonly string _owner;
    private readonly string _repository;

    public GitHubReleaseClient(HttpClient http, string owner = UpdateConstants.GitHubOwner, string repository = UpdateConstants.GitHubRepository)
    {
        _http = http;
        _owner = owner;
        _repository = repository;
    }

    /// <summary>The most recent releases, newest first, drafts included as returned by GitHub.</summary>
    /// <exception cref="HttpRequestException">Network failure or non-success status.</exception>
    public async Task<IReadOnlyList<ReleaseInfo>> GetReleasesAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://api.github.com/repos/{_owner}/{_repository}/releases?per_page={PageSize}");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Kazo-Bar-Updater", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        return Parse(document.RootElement);
    }

    internal static IReadOnlyList<ReleaseInfo> Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) throw new JsonException("Expected a JSON array of releases.");

        var releases = new List<ReleaseInfo>();
        foreach (var item in root.EnumerateArray())
        {
            var tag = GetString(item, "tag_name");
            var page = GetString(item, "html_url");
            if (tag == null || !Uri.TryCreate(page, UriKind.Absolute, out var pageUrl)) continue;

            var assets = new List<ReleaseAsset>();
            if (item.TryGetProperty("assets", out var assetArray) && assetArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetArray.EnumerateArray())
                {
                    var name = GetString(asset, "name");
                    var url = GetString(asset, "browser_download_url");
                    if (name == null || !Uri.TryCreate(url, UriKind.Absolute, out var downloadUrl)) continue;
                    var size = asset.TryGetProperty("size", out var s) && s.TryGetInt64(out var bytes) ? bytes : 0;
                    assets.Add(new ReleaseAsset(name, downloadUrl, size, GetString(asset, "digest")));
                }
            }

            releases.Add(new ReleaseInfo(
                tag,
                GetString(item, "name") ?? tag,
                GetBool(item, "draft"),
                GetBool(item, "prerelease"),
                pageUrl,
                assets));
        }
        return releases;
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool GetBool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;
}
