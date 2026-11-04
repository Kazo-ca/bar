namespace Kazo.Bar.Core.Models;

using System.Text.Json.Serialization;

/// <summary>
/// OAuth applications the user registered for the "Sign in" buttons (US2.4).
/// A client ID is public: it identifies the app, it is not a secret.
/// </summary>
public class OAuthSettings
{
    /// <summary>GitHub OAuth app (or GitHub App) with device flow enabled.</summary>
    [JsonPropertyName("github")]
    public OAuthAppSettings GitHub { get; set; } = new();

    /// <summary>Microsoft Entra ID app registration ("Allow public client flows" on) for Azure DevOps.</summary>
    [JsonPropertyName("azureDevOps")]
    public OAuthAppSettings AzureDevOps { get; set; } = new();
}

/// <summary>One OAuth application.</summary>
public class OAuthAppSettings
{
    public const string DefaultTenant = "organizations";

    [JsonPropertyName("clientId")]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>Microsoft Entra tenant (id or domain); "organizations" accepts any work account. Unused by GitHub.</summary>
    [JsonPropertyName("tenant")]
    public string Tenant { get; set; } = DefaultTenant;
}
