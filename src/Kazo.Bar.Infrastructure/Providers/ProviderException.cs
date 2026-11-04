namespace Kazo.Bar.Infrastructure.Providers;

using System.Net;

/// <summary>A provider call failed; <see cref="Exception.Message"/> is safe to show to the user (never contains the token).</summary>
public sealed class ProviderException(string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>User-facing message for an HTTP error from <paramref name="service"/>.</summary>
    public static ProviderException FromStatus(string service, HttpStatusCode status) => new(status switch
    {
        HttpStatusCode.Unauthorized => $"{service} rejected the token. Enter a new one in the plugin settings.",
        HttpStatusCode.Forbidden => $"{service} refused access (token scope or rate limit).",
        HttpStatusCode.NotFound => $"{service}: not found, or the token cannot see it. Check the names in the plugin settings.",
        _ => $"{service} answered {(int)status} ({status}).",
    });
}
