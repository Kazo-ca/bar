namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>In-memory secrets for the Web test host; both store kinds are offered so Settings can be tested.</summary>
public class MockSecretStore : ISecretStore, ISecretStoreSelector
{
    private readonly Dictionary<string, string> _secrets = new();

    public SecretStoreKind Current { get; private set; } = SecretStoreKind.CredentialManager;

    public IReadOnlyList<SecretStoreKind> Available { get; } = [SecretStoreKind.CredentialManager, SecretStoreKind.EncryptedFile];

    public Task SetAsync(string secretRef, string secret)
    {
        _secrets[secretRef] = secret;
        return Task.CompletedTask;
    }

    public Task<string?> GetAsync(string secretRef) =>
        Task.FromResult(_secrets.TryGetValue(secretRef, out var value) ? value : null);

    public Task<bool> DeleteAsync(string secretRef) => Task.FromResult(_secrets.Remove(secretRef));

    public Task<IReadOnlyList<string>> ListAsync() => Task.FromResult<IReadOnlyList<string>>(_secrets.Keys.ToList());

    public Task<int> SwitchToAsync(SecretStoreKind target)
    {
        var moved = target == Current ? 0 : _secrets.Count;
        Current = target;
        return Task.FromResult(moved);
    }
}
