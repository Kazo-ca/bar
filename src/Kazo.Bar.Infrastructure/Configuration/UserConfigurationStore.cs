namespace Kazo.Bar.Infrastructure.Configuration;

using System.Text.Json;
using System.Text.Json.Nodes;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Loads, migrates and saves the single user configuration file (US2.3, US2.5).
/// <para>
/// On load, a file older than <see cref="UserConfiguration.CurrentSchemaVersion"/> is copied to
/// <c>backups\</c>, migrated and written back. The pre-1 <c>global-settings.json</c> is merged into
/// the <c>settings</c> section, then moved to <c>backups\</c>.
/// </para>
/// <para>
/// When the file cannot be used (invalid JSON, written by a newer version) the store keeps working
/// on defaults, sets <see cref="LoadProblem"/> and never overwrites the file.
/// </para>
/// </summary>
public sealed class UserConfigurationStore
{
    private static readonly IJsonMigration[] Migrations = [new ConfigV0ToV1Migration()];

    private readonly AppDataPaths _paths;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public UserConfigurationStore(AppDataPaths paths, ILogger<UserConfigurationStore>? logger = null, TimeProvider? time = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? NullLogger<UserConfigurationStore>.Instance;
        _time = time ?? TimeProvider.System;
        Current = Load();
    }

    /// <summary>The configuration in memory. Change it through <see cref="UpdateAsync"/>.</summary>
    public UserConfiguration Current { get; private set; }

    /// <summary>Full path of <c>config.json</c>.</summary>
    public string FilePath => _paths.ConfigFile;

    /// <summary>Why the file on disk is not used, or null when it loaded normally.</summary>
    public string? LoadProblem { get; private set; }

    /// <summary>True when the file on disk must not be overwritten (see <see cref="LoadProblem"/>).</summary>
    public bool IsReadOnly => LoadProblem != null;

    /// <summary>Raised after every successful <see cref="UpdateAsync"/>.</summary>
    public event Action<UserConfiguration>? Changed;

    /// <summary>Applies <paramref name="change"/> to the configuration and saves it.</summary>
    public async Task UpdateAsync(Action<UserConfiguration> change, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(change);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            change(Current);
            Current.SchemaVersion = UserConfiguration.CurrentSchemaVersion;
            await PersistAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
        Changed?.Invoke(Current);
    }

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        if (IsReadOnly)
        {
            _logger.LogWarning("Configuration not saved: {Problem}", LoadProblem);
            return;
        }
        var json = JsonSerializer.Serialize(Current, VersionedJson.SerializerOptions);
        await VersionedJson.WriteFileAtomicAsync(_paths.ConfigFile, json, cancellationToken);
    }

    private UserConfiguration Load()
    {
        JsonObject? document;
        try
        {
            document = VersionedJson.ReadFile(_paths.ConfigFile);
        }
        catch (JsonException ex)
        {
            var copy = VersionedJson.Backup(_paths.ConfigFile, _paths.BackupDirectory, "unreadable", _time.GetLocalNow());
            return FailLoad($"config.json is not valid JSON ({ex.Message}). A copy was saved to {copy}.", ex);
        }

        var hasLegacySettings = File.Exists(_paths.LegacyGlobalSettingsFile);
        if (document == null && !hasLegacySettings)
            return new UserConfiguration();

        document ??= new JsonObject();
        var version = VersionedJson.ReadVersion(document);
        try
        {
            if (version >= UserConfiguration.CurrentSchemaVersion)
            {
                var migrated = VersionedJson.Migrate(document, UserConfiguration.CurrentSchemaVersion, Migrations, _paths.ConfigFile);
                return Deserialize(migrated);
            }
            return Upgrade(document, version, hasLegacySettings);
        }
        catch (SchemaVersionTooNewException ex)
        {
            return FailLoad($"{ex.Message} Update Kazo Bar; the file is left unchanged.", ex);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or IOException)
        {
            return FailLoad($"config.json could not be loaded ({ex.Message}); the file is left unchanged.", ex);
        }
    }

    private UserConfiguration Upgrade(JsonObject document, int version, bool hasLegacySettings)
    {
        var source = (JsonObject)document.DeepClone();
        if (version == 0 && hasLegacySettings && !source.ContainsKey("settings"))
            source["settings"] = VersionedJson.ReadFile(_paths.LegacyGlobalSettingsFile);

        var migrated = VersionedJson.Migrate(source, UserConfiguration.CurrentSchemaVersion, Migrations, _paths.ConfigFile);
        var configuration = Deserialize(migrated);

        var now = _time.GetLocalNow();
        var label = $"v{version}";
        var configCopy = VersionedJson.Backup(_paths.ConfigFile, _paths.BackupDirectory, label, now);
        var settingsCopy = version == 0 ? VersionedJson.Backup(_paths.LegacyGlobalSettingsFile, _paths.BackupDirectory, label, now) : null;

        var json = JsonSerializer.Serialize(configuration, VersionedJson.SerializerOptions);
        VersionedJson.WriteFileAtomicAsync(_paths.ConfigFile, json).GetAwaiter().GetResult();
        if (settingsCopy != null)
            File.Delete(_paths.LegacyGlobalSettingsFile);

        _logger.LogInformation(
            "Configuration migrated from schema {From} to {To}. Backups: {ConfigCopy} {SettingsCopy}",
            version, UserConfiguration.CurrentSchemaVersion, configCopy ?? "-", settingsCopy ?? "-");
        return configuration;
    }

    private static UserConfiguration Deserialize(JsonObject document)
    {
        var configuration = document.Deserialize<UserConfiguration>(VersionedJson.SerializerOptions) ?? new UserConfiguration();
        configuration.Settings ??= new GlobalSettings();
        configuration.Projects ??= new List<ProjectConfig>();
        return configuration;
    }

    private UserConfiguration FailLoad(string problem, Exception ex)
    {
        LoadProblem = problem;
        _logger.LogError(ex, "Configuration load failed: {Problem}", problem);
        return new UserConfiguration();
    }
}
