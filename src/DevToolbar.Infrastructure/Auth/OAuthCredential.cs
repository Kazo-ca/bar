namespace DevToolbar.Infrastructure.Auth;

using System.Text.Json;
using System.Text.Json.Serialization;
using DevToolbar.Core.Interfaces;

/// <summary>
/// An OAuth sign-in as saved in the secret store (US2.4). A personal access token is saved
/// as is; a sign-in is saved as this small JSON object, so both live under the same
/// <c>secretRef</c>. Only what is needed to get a new access token is kept: the refresh
/// token when there is one (Microsoft Entra), otherwise the access token (GitHub OAuth apps,
/// whose tokens do not expire). This keeps the value under the 2,560 bytes Credential Manager accepts.
/// </summary>
public sealed record OAuthCredential(
    [property: JsonPropertyName("provider")] OAuthProvider Provider,
    [property: JsonPropertyName("accessToken")] string? AccessToken,
    [property: JsonPropertyName("refreshToken")] string? RefreshToken)
{
    private const string Marker = "oauth";

    private static readonly JsonSerializerOptions Options = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    [JsonPropertyName("kind")]
    public string Kind => Marker;

    /// <summary>What to save after a sign-in or a refresh.</summary>
    public static OAuthCredential From(OAuthProvider provider, OAuthTokens tokens) =>
        string.IsNullOrEmpty(tokens.RefreshToken)
            ? new(provider, tokens.AccessToken, null)
            : new(provider, null, tokens.RefreshToken);

    public string Serialize() => JsonSerializer.Serialize(this, Options);

    /// <summary>Reads a saved sign-in; false for anything else (a personal access token never starts with '{').</summary>
    public static bool TryParse(string? value, out OAuthCredential credential)
    {
        credential = null!;
        if (string.IsNullOrWhiteSpace(value) || value.TrimStart()[0] != '{')
            return false;
        try
        {
            using var doc = JsonDocument.Parse(value);
            if (!doc.RootElement.TryGetProperty("kind", out var kind) || kind.GetString() != Marker)
                return false;
            var parsed = JsonSerializer.Deserialize<OAuthCredential>(value, Options);
            if (parsed == null || (string.IsNullOrEmpty(parsed.AccessToken) && string.IsNullOrEmpty(parsed.RefreshToken)))
                return false;
            credential = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>Tokens returned by a provider; <see cref="ExpiresAt"/> is null when the access token does not expire.</summary>
public sealed record OAuthTokens(string AccessToken, string? RefreshToken, DateTimeOffset? ExpiresAt);
