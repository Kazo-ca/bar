namespace DevToolbar.Infrastructure.Configuration;

using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;

/// <summary>
/// <see cref="IGlobalSettingsService"/> over the <c>settings</c> section of the user configuration file.
/// </summary>
public sealed class UserConfigGlobalSettingsService : IGlobalSettingsService
{
    private readonly UserConfigurationStore _store;

    public UserConfigGlobalSettingsService(UserConfigurationStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public GlobalSettings Current => _store.Current.Settings;

    public event Action<GlobalSettings>? OnSettingsChanged;

    /// <summary>The store loads the file when it is created; nothing else to do.</summary>
    public Task LoadAsync() => Task.CompletedTask;

    public async Task SaveAsync()
    {
        await _store.UpdateAsync(_ => { });
        OnSettingsChanged?.Invoke(Current);
    }

    public async Task UpdateAsync(Action<GlobalSettings> updateAction)
    {
        ArgumentNullException.ThrowIfNull(updateAction);
        await _store.UpdateAsync(c => updateAction(c.Settings));
        OnSettingsChanged?.Invoke(Current);
    }
}
