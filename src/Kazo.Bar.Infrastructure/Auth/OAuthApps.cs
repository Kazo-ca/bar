namespace Kazo.Bar.Infrastructure.Auth;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>Endpoints and scopes of the device flow for each provider.</summary>
public sealed record OAuthApp(OAuthProvider Provider, string ClientId, string Tenant)
{
    /// <summary>Azure DevOps resource; ".default" asks for the permissions the app registration declares.</summary>
    public const string AzureDevOpsScope = "499b84ac-1321-427f-aa17-267ca6975798/.default offline_access";

    /// <summary>"repo" lets the toolbar read issues and Actions runs of private repositories.</summary>
    public const string GitHubScope = "repo";

    public string ServiceName => Provider == OAuthProvider.GitHub ? "GitHub" : "Azure DevOps";

    public Uri DeviceCodeUri => Provider == OAuthProvider.GitHub
        ? new Uri("https://github.com/login/device/code")
        : new Uri($"https://login.microsoftonline.com/{Uri.EscapeDataString(Tenant)}/oauth2/v2.0/devicecode");

    public Uri TokenUri => Provider == OAuthProvider.GitHub
        ? new Uri("https://github.com/login/oauth/access_token")
        : new Uri($"https://login.microsoftonline.com/{Uri.EscapeDataString(Tenant)}/oauth2/v2.0/token");

    public string Scope => Provider == OAuthProvider.GitHub ? GitHubScope : AzureDevOpsScope;

    /// <summary>The app the user configured in Settings, or null when its client ID is empty.</summary>
    public static OAuthApp? FromSettings(OAuthSettings settings, OAuthProvider provider)
    {
        var app = provider == OAuthProvider.GitHub ? settings.GitHub : settings.AzureDevOps;
        if (string.IsNullOrWhiteSpace(app.ClientId))
            return null;
        var tenant = string.IsNullOrWhiteSpace(app.Tenant) ? OAuthAppSettings.DefaultTenant : app.Tenant.Trim();
        return new OAuthApp(provider, app.ClientId.Trim(), tenant);
    }
}
