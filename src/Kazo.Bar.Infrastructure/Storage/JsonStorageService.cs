namespace Kazo.Bar.Infrastructure.Storage;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Kazo.Bar.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

/// <summary>
/// Real <see cref="IStorageService"/> (US8.3): one JSON file per namespace in
/// <c>%APPDATA%\DevToolbar\data\{namespace}.json</c>, each with a <c>schemaVersion</c>:
/// <code>
/// { "schemaVersion": 1, "values": { "key": ... }, "collections": { "name": [ ... ] } }
/// </code>
/// Everything goes through this interface, so the format can move to a database later (US2.5).
/// </summary>
public sealed partial class JsonStorageService : IStorageService
{
    public const int CurrentSchemaVersion = 1;
    private const string ValuesProperty = "values";
    private const string CollectionsProperty = "collections";

    private static readonly IJsonMigration[] Migrations = [];

    private readonly AppDataPaths _paths;
    private readonly ILogger _logger;
    private readonly TimeProvider _time;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly Dictionary<string, JsonObject> _cache = new(StringComparer.OrdinalIgnoreCase);

    public JsonStorageService(AppDataPaths paths, ILogger<JsonStorageService>? logger = null, TimeProvider? time = null)
    {
        _paths = paths ?? throw new ArgumentNullException(nameof(paths));
        _logger = logger ?? NullLogger<JsonStorageService>.Instance;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>Path of the file that holds <paramref name="ns"/>.</summary>
    public string FileFor(string ns) => Path.Combine(_paths.DataDirectory, $"{ValidateNamespace(ns)}.json");

    public Task SetAsync<T>(string ns, string key, T value) =>
        ChangeAsync(ns, document => Section(document, ValuesProperty)[ValidateKey(key)] = JsonSerializer.SerializeToNode(value, VersionedJson.SerializerOptions));

    public Task<T?> GetAsync<T>(string ns, string key) => ReadAsync(ns, document =>
    {
        var node = Section(document, ValuesProperty)[ValidateKey(key)];
        return node == null ? default : node.Deserialize<T>(VersionedJson.SerializerOptions);
    });

    public Task DeleteAsync(string ns, string key) =>
        ChangeAsync(ns, document => Section(document, ValuesProperty).Remove(ValidateKey(key)));

    public Task<IReadOnlyList<string>> GetKeysAsync(string ns) => ReadAsync(ns, document =>
        (IReadOnlyList<string>)Section(document, ValuesProperty).Select(p => p.Key).ToList());

    public Task AddToCollectionAsync<T>(string ns, string collection, T item) =>
        ChangeAsync(ns, document => Collection(document, collection).Add(JsonSerializer.SerializeToNode(item, VersionedJson.SerializerOptions)));

    public Task<IReadOnlyList<T>> QueryCollectionAsync<T>(string ns, string collection, Func<T, bool>? predicate = null) =>
        ReadAsync(ns, document =>
        {
            var items = Collection(document, collection)
                .Where(node => node != null)
                .Select(node => node!.Deserialize<T>(VersionedJson.SerializerOptions)!)
                .Where(item => item != null && (predicate == null || predicate(item)));
            return (IReadOnlyList<T>)items.ToList();
        });

    public Task ClearCollectionAsync(string ns, string collection) =>
        ChangeAsync(ns, document => Section(document, CollectionsProperty).Remove(ValidateKey(collection)));

    private async Task<TResult> ReadAsync<TResult>(string ns, Func<JsonObject, TResult> read)
    {
        await _lock.WaitAsync();
        try
        {
            return read(Open(ns));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ChangeAsync(string ns, Action<JsonObject> change)
    {
        await _lock.WaitAsync();
        try
        {
            var document = (JsonObject)Open(ns).DeepClone();
            change(document);
            await VersionedJson.WriteFileAtomicAsync(FileFor(ns), document.ToJsonString(VersionedJson.SerializerOptions));
            _cache[ns] = document;
        }
        finally
        {
            _lock.Release();
        }
    }

    private JsonObject Open(string ns)
    {
        var path = FileFor(ns);
        if (_cache.TryGetValue(ns, out var cached))
            return cached;

        JsonObject document;
        try
        {
            var onDisk = VersionedJson.ReadFile(path);
            if (onDisk == null)
            {
                document = NewDocument();
            }
            else
            {
                var version = VersionedJson.ReadVersion(onDisk);
                if (version < CurrentSchemaVersion)
                    VersionedJson.Backup(path, _paths.BackupDirectory, $"v{version}", _time.GetLocalNow());
                // Throws SchemaVersionTooNewException for a newer file: nothing is overwritten.
                document = VersionedJson.Migrate(onDisk, CurrentSchemaVersion, Migrations, path);
            }
        }
        catch (JsonException ex)
        {
            var copy = VersionedJson.Backup(path, _paths.BackupDirectory, "unreadable", _time.GetLocalNow());
            _logger.LogError(ex, "Storage file {Path} is not valid JSON; starting empty. Copy: {Copy}", path, copy);
            document = NewDocument();
        }

        _cache[ns] = document;
        return document;
    }

    private static JsonObject NewDocument() => new()
    {
        [VersionedJson.VersionProperty] = CurrentSchemaVersion,
        [ValuesProperty] = new JsonObject(),
        [CollectionsProperty] = new JsonObject(),
    };

    private static JsonObject Section(JsonObject document, string name)
    {
        if (document[name] is JsonObject section)
            return section;
        section = new JsonObject();
        document[name] = section;
        return section;
    }

    private static JsonArray Collection(JsonObject document, string name)
    {
        var collections = Section(document, CollectionsProperty);
        var key = ValidateKey(name);
        if (collections[key] is JsonArray array)
            return array;
        array = new JsonArray();
        collections[key] = array;
        return array;
    }

    private static string ValidateNamespace(string ns)
    {
        if (string.IsNullOrEmpty(ns) || !NamespacePattern().IsMatch(ns) || ns.Trim('.').Length == 0)
            throw new ArgumentException("A storage namespace uses 1-64 letters, digits, '.', '-' or '_'.", nameof(ns));
        return ns;
    }

    private static string ValidateKey(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        return key;
    }

    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex NamespacePattern();
}
