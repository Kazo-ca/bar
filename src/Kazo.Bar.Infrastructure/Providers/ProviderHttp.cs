namespace Kazo.Bar.Infrastructure.Providers;

using System.Net.Http.Json;
using System.Text.Json;
using Kazo.Bar.Core.Interfaces;

/// <summary>HTTP plumbing shared by the GitHub and Azure DevOps clients.</summary>
internal static class ProviderHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads the token or explains what is missing.</summary>
    public static async Task<string> RequireTokenAsync(ISecretStore secrets, string secretRef, string service)
    {
        if (string.IsNullOrWhiteSpace(secretRef))
            throw new ProviderException($"No {service} token is configured. Add one in the plugin settings.");
        var token = await secrets.GetAsync(secretRef);
        return string.IsNullOrEmpty(token)
            ? throw new ProviderException($"No {service} token is saved under '{secretRef}'. Add one in the plugin settings.")
            : token;
    }

    /// <summary>Sends the request and deserializes the JSON answer, turning failures into <see cref="ProviderException"/>.</summary>
    public static async Task<T> SendAsync<T>(HttpClient http, HttpRequestMessage request, string service, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException($"{service} cannot be reached ({ex.Message}).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException($"{service} did not answer in time.", ex);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
                throw ProviderException.FromStatus(service, response.StatusCode);
            try
            {
                return await response.Content.ReadFromJsonAsync<T>(Json, cancellationToken)
                    ?? throw new ProviderException($"{service} returned an empty answer.");
            }
            catch (JsonException ex)
            {
                throw new ProviderException($"{service} returned an unexpected answer.", ex);
            }
        }
    }
}
