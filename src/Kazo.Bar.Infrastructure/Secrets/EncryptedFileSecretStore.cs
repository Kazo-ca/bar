namespace Kazo.Bar.Infrastructure.Secrets;

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Infrastructure.Storage;

/// <summary>
/// Secrets in <c>%APPDATA%\DevToolbar\secrets.json</c>, each value encrypted by an
/// <see cref="ISecretProtector"/> (DPAPI in the Windows host):
/// <code>{ "schemaVersion": 1, "secrets": { "alpha-github-pat": "base64..." } }</code>
/// </summary>
public sealed class EncryptedFileSecretStore : ISecretStore
{
    public const int CurrentSchemaVersion = 1;
    private const string SecretsProperty = "secrets";

    private readonly string _path;
    private readonly ISecretProtector _protector;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public EncryptedFileSecretStore(AppDataPaths paths, ISecretProtector protector)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _path = Path.Combine(paths.Root, "secrets.json");
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
    }

    public string FilePath => _path;

    public async Task SetAsync(string secretRef, string secret)
    {
        SecretReference.Validate(secretRef);
        ArgumentNullException.ThrowIfNull(secret);
        var encrypted = Convert.ToBase64String(_protector.Protect(Encoding.UTF8.GetBytes(secret)));
        await ChangeAsync(secrets => secrets[secretRef] = encrypted);
    }

    public async Task<string?> GetAsync(string secretRef)
    {
        SecretReference.Validate(secretRef);
        var secrets = await ReadAsync();
        if (secrets[secretRef] is not JsonValue value || !value.TryGetValue<string>(out var encoded))
            return null;
        try
        {
            return Encoding.UTF8.GetString(_protector.Unprotect(Convert.FromBase64String(encoded)));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            throw new CryptographicException(
                $"The secret '{secretRef}' cannot be decrypted (was secrets.json copied from another Windows account?). Enter it again.", ex);
        }
    }

    public async Task<bool> DeleteAsync(string secretRef)
    {
        SecretReference.Validate(secretRef);
        var removed = false;
        await ChangeAsync(secrets => removed = secrets.Remove(secretRef));
        return removed;
    }

    public async Task<IReadOnlyList<string>> ListAsync() =>
        (await ReadAsync()).Select(p => p.Key).ToList();

    private async Task<JsonObject> ReadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            return Secrets(Load());
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task ChangeAsync(Action<JsonObject> change)
    {
        await _lock.WaitAsync();
        try
        {
            var document = Load();
            change(Secrets(document));
            await VersionedJson.WriteFileAtomicAsync(_path, document.ToJsonString(VersionedJson.SerializerOptions));
        }
        finally
        {
            _lock.Release();
        }
    }

    private JsonObject Load()
    {
        var document = VersionedJson.ReadFile(_path)
            ?? new JsonObject { [VersionedJson.VersionProperty] = CurrentSchemaVersion, [SecretsProperty] = new JsonObject() };
        // Throws SchemaVersionTooNewException for a newer file: it is then never overwritten.
        return VersionedJson.Migrate(document, CurrentSchemaVersion, [new InitialVersion()], _path);
    }

    private static JsonObject Secrets(JsonObject document)
    {
        if (document[SecretsProperty] is JsonObject secrets)
            return secrets;
        secrets = new JsonObject();
        document[SecretsProperty] = secrets;
        return secrets;
    }

    /// <summary>A file without <c>schemaVersion</c> (hand-made) is read as version 1.</summary>
    private sealed class InitialVersion : IJsonMigration
    {
        public int FromVersion => 0;
        public JsonObject Apply(JsonObject document) => (JsonObject)document.DeepClone();
    }
}
