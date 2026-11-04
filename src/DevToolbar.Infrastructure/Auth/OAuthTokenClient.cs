namespace DevToolbar.Infrastructure.Auth;

using System.Net.Http.Headers;
using System.Text.Json;
using DevToolbar.Core.Interfaces;
using DevToolbar.Infrastructure.Providers;

/// <summary>Result of one poll of the token endpoint during the device flow.</summary>
public enum DevicePollStatus
{
    Pending,
    SlowDown,
    Approved,
    Denied,
    Expired,
    Failed,
}

public sealed record DevicePoll(DevicePollStatus Status, OAuthTokens? Tokens = null, string? Message = null);

/// <summary>
/// HTTP calls of the OAuth 2.0 device flow (RFC 8628) and of the refresh grant, for GitHub
/// and Microsoft Entra ID. Both answer JSON; GitHub reports errors with status 200, Entra with 400,
/// so the body is read whatever the status.
/// </summary>
public sealed class OAuthTokenClient
{
    private const string DeviceCodeGrant = "urn:ietf:params:oauth:grant-type:device_code";

    private readonly HttpClient _http;
    private readonly TimeProvider _time;

    public OAuthTokenClient(HttpClient http, TimeProvider? time = null)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _time = time ?? TimeProvider.System;
    }

    public async Task<DeviceCodeChallenge> RequestDeviceCodeAsync(OAuthApp app, CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync(app, app.DeviceCodeUri, new()
        {
            ["client_id"] = app.ClientId,
            ["scope"] = app.Scope,
        }, cancellationToken);
        var root = doc.RootElement;
        if (Text(root, "error") is { } error)
            throw new ProviderException(DescribeError(app, error, Text(root, "error_description")));

        var userCode = Text(root, "user_code");
        var deviceCode = Text(root, "device_code");
        var uri = Text(root, "verification_uri");
        // The link is shown to the user: only an https address is accepted.
        if (userCode == null || deviceCode == null || !Uri.TryCreate(uri, UriKind.Absolute, out var verificationUri)
            || verificationUri.Scheme != Uri.UriSchemeHttps)
            throw new ProviderException($"{app.ServiceName} returned an unexpected answer to the sign-in request.");

        var expiresIn = Number(root, "expires_in") ?? 900;
        var interval = Math.Max(Number(root, "interval") ?? 5, 1);
        return new DeviceCodeChallenge(app.Provider, userCode, verificationUri,
            _time.GetUtcNow().AddSeconds(expiresIn), TimeSpan.FromSeconds(interval), deviceCode);
    }

    public async Task<DevicePoll> PollAsync(OAuthApp app, string deviceCode, CancellationToken cancellationToken = default)
    {
        using var doc = await PostAsync(app, app.TokenUri, new()
        {
            ["client_id"] = app.ClientId,
            ["device_code"] = deviceCode,
            ["grant_type"] = DeviceCodeGrant,
        }, cancellationToken);
        var root = doc.RootElement;
        return Text(root, "error") switch
        {
            null => ReadTokens(app, root) is { } tokens
                ? new DevicePoll(DevicePollStatus.Approved, tokens)
                : new DevicePoll(DevicePollStatus.Failed, Message: $"{app.ServiceName} returned an unexpected answer."),
            "authorization_pending" => new DevicePoll(DevicePollStatus.Pending),
            "slow_down" => new DevicePoll(DevicePollStatus.SlowDown),
            "expired_token" or "code_expired" => new DevicePoll(DevicePollStatus.Expired, Message: "The code expired before it was entered. Start the sign-in again."),
            "access_denied" or "authorization_declined" => new DevicePoll(DevicePollStatus.Denied, Message: $"The sign-in was refused on {app.ServiceName}."),
            var error => new DevicePoll(DevicePollStatus.Failed, Message: DescribeError(app, error, Text(root, "error_description"))),
        };
    }

    /// <summary>A new access token from a refresh token; throws <see cref="ProviderException"/> when the sign-in must be done again.</summary>
    public async Task<OAuthTokens> RefreshAsync(OAuthApp app, string refreshToken, CancellationToken cancellationToken = default)
    {
        var form = new Dictionary<string, string>
        {
            ["client_id"] = app.ClientId,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
        };
        if (app.Provider == OAuthProvider.AzureDevOps)
            form["scope"] = app.Scope;

        using var doc = await PostAsync(app, app.TokenUri, form, cancellationToken);
        var root = doc.RootElement;
        if (Text(root, "error") is { } error)
        {
            throw new ProviderException(error is "invalid_grant" or "bad_refresh_token" or "interaction_required"
                ? $"The {app.ServiceName} sign-in has expired. Sign in again in the plugin settings."
                : DescribeError(app, error, Text(root, "error_description")));
        }
        return ReadTokens(app, root) ?? throw new ProviderException($"{app.ServiceName} returned an unexpected answer.");
    }

    private OAuthTokens? ReadTokens(OAuthApp app, JsonElement root)
    {
        var access = Text(root, "access_token");
        if (access == null)
            return null;
        DateTimeOffset? expiresAt = Number(root, "expires_in") is { } seconds and > 0
            ? _time.GetUtcNow().AddSeconds(seconds)
            : null;
        return new OAuthTokens(access, Text(root, "refresh_token"), expiresAt);
    }

    private async Task<JsonDocument> PostAsync(OAuthApp app, Uri uri, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("DevToolbar", "1.0"));

        HttpResponseMessage response;
        try
        {
            response = await _http.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException($"{app.ServiceName} cannot be reached ({ex.Message}).", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException($"{app.ServiceName} did not answer in time.", ex);
        }

        using (response)
        {
            try
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                return JsonDocument.Parse(body);
            }
            catch (JsonException ex)
            {
                throw response.IsSuccessStatusCode
                    ? new ProviderException($"{app.ServiceName} returned an unexpected answer.", ex)
                    : ProviderException.FromStatus(app.ServiceName, response.StatusCode);
            }
        }
    }

    private static string DescribeError(OAuthApp app, string error, string? description) => error switch
    {
        "device_flow_disabled" => "Device flow is off for this GitHub OAuth app. Turn on \"Enable Device Flow\" in the app settings on GitHub.",
        "incorrect_client_credentials" or "invalid_client" or "unauthorized_client" =>
            $"{app.ServiceName} does not know this client ID. Check it in Settings → Sign-in apps.",
        _ => $"{app.ServiceName} refused the sign-in ({FirstLine(description) ?? error}).",
    };

    private static string? FirstLine(string? text) =>
        string.IsNullOrWhiteSpace(text) ? null : text.Split('\n', 2)[0].Trim().TrimEnd('.');

    private static string? Text(JsonElement root, string name) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int? Number(JsonElement root, string name)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number) ? number : null;
    }
}
