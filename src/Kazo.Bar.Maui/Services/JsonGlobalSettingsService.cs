using System.Text.Json;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

namespace Kazo.Bar.Maui.Services;

/// <summary>
/// Persists GlobalSettings to %APPDATA%/DevToolbar/global-settings.json.
/// Thread-safe load/save with event notification on change.
/// </summary>
public class JsonGlobalSettingsService : IGlobalSettingsService
{
    private static readonly string SettingsDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DevToolbar");

    private static readonly string SettingsPath =
        Path.Combine(SettingsDir, "global-settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private GlobalSettings _current = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public GlobalSettings Current => _current;

    public event Action<GlobalSettings>? OnSettingsChanged;

    public async Task LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                _current = JsonSerializer.Deserialize<GlobalSettings>(json, JsonOptions) ?? new GlobalSettings();
            }
            else
            {
                _current = new GlobalSettings();
                // Create default file
                await PersistAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[GlobalSettings] Failed to load: {ex.Message}");
            _current = new GlobalSettings();
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }

        OnSettingsChanged?.Invoke(_current);
    }

    public async Task UpdateAsync(Action<GlobalSettings> updateAction)
    {
        await _lock.WaitAsync();
        try
        {
            updateAction(_current);
            await PersistAsync();
        }
        finally
        {
            _lock.Release();
        }

        OnSettingsChanged?.Invoke(_current);
    }

    private async Task PersistAsync()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(_current, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }
}
