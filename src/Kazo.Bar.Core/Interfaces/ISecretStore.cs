namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Stores access tokens outside the configuration file (US2.4). The configuration only keeps
/// a reference (<c>secretRef</c>); the value lives in the store the user chose.
/// A reference uses 1-100 letters, digits, '.', '-' or '_'.
/// </summary>
public interface ISecretStore
{
    /// <summary>Creates or replaces the secret.</summary>
    Task SetAsync(string secretRef, string secret);

    /// <summary>The secret, or null when there is none.</summary>
    Task<string?> GetAsync(string secretRef);

    /// <summary>Removes the secret; returns false when there was none.</summary>
    Task<bool> DeleteAsync(string secretRef);

    /// <summary>References of every secret Kazo Bar stored (never the values).</summary>
    Task<IReadOnlyList<string>> ListAsync();
}
