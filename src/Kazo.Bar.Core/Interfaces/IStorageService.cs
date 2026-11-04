namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Abstracts local data storage (SQLite in MAUI, in-memory for Web testing).
/// Provides key-value and typed collection storage for plugins and services.
/// </summary>
public interface IStorageService
{
    /// <summary>Store a value by key, scoped to a namespace (e.g., plugin ID).</summary>
    Task SetAsync<T>(string ns, string key, T value);

    /// <summary>Retrieve a value by key from a namespace.</summary>
    Task<T?> GetAsync<T>(string ns, string key);

    /// <summary>Delete a key from a namespace.</summary>
    Task DeleteAsync(string ns, string key);

    /// <summary>Get all keys in a namespace.</summary>
    Task<IReadOnlyList<string>> GetKeysAsync(string ns);

    /// <summary>Store an item in a typed collection.</summary>
    Task AddToCollectionAsync<T>(string ns, string collection, T item);

    /// <summary>Query items from a typed collection.</summary>
    Task<IReadOnlyList<T>> QueryCollectionAsync<T>(string ns, string collection, Func<T, bool>? predicate = null);

    /// <summary>Clear all items in a collection.</summary>
    Task ClearCollectionAsync(string ns, string collection);
}
