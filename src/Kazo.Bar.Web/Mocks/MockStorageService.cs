namespace Kazo.Bar.Web.Mocks;

using System.Collections.Concurrent;
using System.Text.Json;
using Kazo.Bar.Core.Interfaces;

/// <summary>
/// In-memory storage service for web testing.
/// </summary>
public class MockStorageService : IStorageService
{
    private readonly ConcurrentDictionary<string, string> _store = new();
    private readonly ConcurrentDictionary<string, List<string>> _collections = new();

    private static string MakeKey(string ns, string key) => $"{ns}::{key}";
    private static string MakeCollectionKey(string ns, string collection) => $"{ns}::col::{collection}";

    public Task SetAsync<T>(string ns, string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        _store[MakeKey(ns, key)] = json;
        return Task.CompletedTask;
    }

    public Task<T?> GetAsync<T>(string ns, string key)
    {
        if (_store.TryGetValue(MakeKey(ns, key), out var json))
        {
            return Task.FromResult(JsonSerializer.Deserialize<T>(json));
        }
        return Task.FromResult(default(T));
    }

    public Task DeleteAsync(string ns, string key)
    {
        _store.TryRemove(MakeKey(ns, key), out _);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> GetKeysAsync(string ns)
    {
        var prefix = $"{ns}::";
        var keys = _store.Keys
            .Where(k => k.StartsWith(prefix) && !k.Contains("::col::"))
            .Select(k => k[prefix.Length..])
            .ToList();
        return Task.FromResult<IReadOnlyList<string>>(keys.AsReadOnly());
    }

    public Task AddToCollectionAsync<T>(string ns, string collection, T item)
    {
        var colKey = MakeCollectionKey(ns, collection);
        var json = JsonSerializer.Serialize(item);
        _collections.AddOrUpdate(colKey,
            _ => new List<string> { json },
            (_, list) => { list.Add(json); return list; });
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<T>> QueryCollectionAsync<T>(string ns, string collection, Func<T, bool>? predicate = null)
    {
        var colKey = MakeCollectionKey(ns, collection);
        if (!_collections.TryGetValue(colKey, out var items))
        {
            return Task.FromResult<IReadOnlyList<T>>(Array.Empty<T>());
        }

        var results = items
            .Select(json => JsonSerializer.Deserialize<T>(json))
            .Where(item => item != null && (predicate == null || predicate(item)))
            .Cast<T>()
            .ToList();

        return Task.FromResult<IReadOnlyList<T>>(results.AsReadOnly());
    }

    public Task ClearCollectionAsync(string ns, string collection)
    {
        var colKey = MakeCollectionKey(ns, collection);
        _collections.TryRemove(colKey, out _);
        return Task.CompletedTask;
    }
}
