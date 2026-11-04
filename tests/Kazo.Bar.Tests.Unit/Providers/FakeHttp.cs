namespace Kazo.Bar.Tests.Unit.Providers;

using System.Net;
using System.Text;
using Kazo.Bar.Core.Interfaces;

/// <summary>Answers requests from a list of (url fragment, status, json) rules and records them.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly List<(string Fragment, HttpStatusCode Status, string Json)> _rules = new();

    public List<(HttpMethod Method, string Url, string? Authorization, string? Body)> Requests { get; } = new();

    public FakeHttp On(string urlFragment, string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _rules.Add((urlFragment, status, json));
        return this;
    }

    public HttpClient Client() => new(this);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var url = request.RequestUri!.AbsoluteUri;
        Requests.Add((request.Method, url, request.Headers.Authorization?.ToString(), body));
        var rule = _rules.LastOrDefault(r => url.Contains(r.Fragment, StringComparison.Ordinal));
        return rule.Fragment == null
            ? new HttpResponseMessage(HttpStatusCode.NotFound)
            : new HttpResponseMessage(rule.Status) { Content = new StringContent(rule.Json, Encoding.UTF8, "application/json") };
    }
}

internal sealed class MemorySecrets : ISecretStore
{
    public Dictionary<string, string> Values { get; } = new();
    public Task SetAsync(string secretRef, string secret) { Values[secretRef] = secret; return Task.CompletedTask; }
    public Task<string?> GetAsync(string secretRef) => Task.FromResult(Values.TryGetValue(secretRef, out var v) ? v : null);
    public Task<bool> DeleteAsync(string secretRef) => Task.FromResult(Values.Remove(secretRef));
    public Task<IReadOnlyList<string>> ListAsync() => Task.FromResult<IReadOnlyList<string>>(Values.Keys.ToList());
}
