namespace Kazo.Bar.Core.Interfaces;

/// <summary>Services that offer a "Sign in" button (US2.4).</summary>
public enum OAuthProvider
{
    GitHub,
    AzureDevOps,
}

/// <summary>What is saved under a secret reference.</summary>
public enum StoredCredentialKind
{
    None,
    PersonalAccessToken,
    OAuth,
}

/// <summary>
/// The code the user types at <see cref="VerificationUri"/> to approve the sign-in
/// (OAuth 2.0 device authorization grant, RFC 8628).
/// </summary>
public sealed record DeviceCodeChallenge(
    OAuthProvider Provider,
    string UserCode,
    Uri VerificationUri,
    DateTimeOffset ExpiresAt,
    TimeSpan Interval,
    string DeviceCode);

/// <summary>
/// Signs in to GitHub or Azure DevOps with the device flow and saves the result in the
/// secret store, under the same reference a personal access token would use.
/// </summary>
public interface IOAuthSignInService
{
    /// <summary>True when the user entered a client ID for this provider in Settings.</summary>
    bool IsConfigured(OAuthProvider provider);

    /// <summary>Asks the provider for a user code.</summary>
    Task<DeviceCodeChallenge> StartAsync(OAuthProvider provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits until the user approves (or refuses) the code, then saves the sign-in under
    /// <paramref name="secretRef"/>. Throws with a user-facing message when it fails or expires.
    /// </summary>
    Task CompleteAsync(DeviceCodeChallenge challenge, string secretRef, CancellationToken cancellationToken = default);

    /// <summary>What is saved under <paramref name="secretRef"/> (never the value).</summary>
    Task<StoredCredentialKind> DescribeAsync(string secretRef);
}
