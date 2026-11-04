namespace Kazo.Bar.Core.Interfaces;

using Kazo.Bar.Core.Models;

/// <summary>
/// Service for loading and saving application-wide global settings.
/// </summary>
public interface IGlobalSettingsService
{
    /// <summary>Gets the current global settings.</summary>
    GlobalSettings Current { get; }

    /// <summary>Loads settings from persistent storage.</summary>
    Task LoadAsync();

    /// <summary>Saves the current settings to persistent storage.</summary>
    Task SaveAsync();

    /// <summary>Updates and persists the settings.</summary>
    Task UpdateAsync(Action<GlobalSettings> updateAction);

    /// <summary>Fired when settings change.</summary>
    event Action<GlobalSettings>? OnSettingsChanged;
}
