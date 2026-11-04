using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Web.Mocks;

/// <summary>
/// Mock IGlobalSettingsService for web testing — in-memory only, no file persistence.
/// </summary>
public class MockGlobalSettingsService : IGlobalSettingsService
{
    private GlobalSettings _current = new();

    public GlobalSettings Current => _current;

    public event Action<GlobalSettings>? OnSettingsChanged;

    public Task LoadAsync() => Task.CompletedTask;

    public Task SaveAsync()
    {
        OnSettingsChanged?.Invoke(_current);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Action<GlobalSettings> updateAction)
    {
        updateAction(_current);
        OnSettingsChanged?.Invoke(_current);
        return Task.CompletedTask;
    }
}
