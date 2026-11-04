namespace Kazo.Bar.Infrastructure.Secrets;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>
/// The <see cref="ISecretStore"/> the app uses: forwards to the store chosen in
/// <see cref="GlobalSettings.SecretStore"/>, and moves the secrets when the choice changes.
/// </summary>
public sealed class SelectableSecretStore : ISecretStore, ISecretStoreSelector
{
    private readonly IReadOnlyDictionary<SecretStoreKind, ISecretStore> _stores;
    private readonly IGlobalSettingsService _settings;
    private readonly SemaphoreSlim _switchLock = new(1, 1);

    public SelectableSecretStore(IReadOnlyDictionary<SecretStoreKind, ISecretStore> stores, IGlobalSettingsService settings)
    {
        ArgumentNullException.ThrowIfNull(stores);
        if (stores.Count == 0)
            throw new ArgumentException("At least one secret store is required.", nameof(stores));
        _stores = stores;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>The chosen store, or the first available one when the choice is not available on this host.</summary>
    public SecretStoreKind Current => _stores.ContainsKey(_settings.Current.SecretStore)
        ? _settings.Current.SecretStore
        : _stores.Keys.First();

    public IReadOnlyList<SecretStoreKind> Available => _stores.Keys.ToList();

    private ISecretStore Active => _stores[Current];

    public Task SetAsync(string secretRef, string secret) => Active.SetAsync(secretRef, secret);

    public Task<string?> GetAsync(string secretRef) => Active.GetAsync(secretRef);

    public Task<bool> DeleteAsync(string secretRef) => Active.DeleteAsync(secretRef);

    public Task<IReadOnlyList<string>> ListAsync() => Active.ListAsync();

    public async Task<int> SwitchToAsync(SecretStoreKind target)
    {
        if (!_stores.TryGetValue(target, out var destination))
            throw new ArgumentException($"The secret store {target} is not available.", nameof(target));

        await _switchLock.WaitAsync();
        try
        {
            if (target == Current)
                return 0;

            // Copy everything first, then save the choice, then clean the old store:
            // a failure part-way leaves every secret readable from the current store.
            var source = Active;
            var references = await source.ListAsync();
            foreach (var reference in references)
            {
                var value = await source.GetAsync(reference);
                if (value != null)
                    await destination.SetAsync(reference, value);
            }
            await _settings.UpdateAsync(s => s.SecretStore = target);
            foreach (var reference in references)
                await source.DeleteAsync(reference);
            return references.Count;
        }
        finally
        {
            _switchLock.Release();
        }
    }
}
