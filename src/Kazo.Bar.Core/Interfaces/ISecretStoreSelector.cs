namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>The user's choice of secret store (US2.4), shown in Settings.</summary>
public interface ISecretStoreSelector
{
    /// <summary>The store secrets currently go to.</summary>
    SecretStoreKind Current { get; }

    /// <summary>Stores this host can use.</summary>
    IReadOnlyList<SecretStoreKind> Available { get; }

    /// <summary>
    /// Moves every existing secret to <paramref name="target"/>, then saves the choice.
    /// Returns how many secrets were moved.
    /// </summary>
    Task<int> SwitchToAsync(SecretStoreKind target);
}
