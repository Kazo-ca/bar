namespace Kazo.Bar.Tests.Unit.Infrastructure;

using System.Security.Cryptography;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Configuration;
using Kazo.Bar.Infrastructure.Secrets;

[TestFixture]
public class SecretStoreTests
{
    /// <summary>Stands in for DPAPI: reversible, and fails on data it did not produce.</summary>
    private sealed class FakeProtector : ISecretProtector
    {
        private const byte Marker = 0x42;
        public byte[] Protect(byte[] plain) => [Marker, .. plain.Select(b => (byte)(b ^ 0x5A))];
        public byte[] Unprotect(byte[] encrypted) => encrypted.Length > 0 && encrypted[0] == Marker
            ? encrypted.Skip(1).Select(b => (byte)(b ^ 0x5A)).ToArray()
            : throw new CryptographicException("not mine");
    }

    private sealed class MemoryStore : ISecretStore
    {
        public Dictionary<string, string> Values { get; } = new();
        public Task SetAsync(string secretRef, string secret) { Values[secretRef] = secret; return Task.CompletedTask; }
        public Task<string?> GetAsync(string secretRef) => Task.FromResult(Values.TryGetValue(secretRef, out var v) ? v : null);
        public Task<bool> DeleteAsync(string secretRef) => Task.FromResult(Values.Remove(secretRef));
        public Task<IReadOnlyList<string>> ListAsync() => Task.FromResult<IReadOnlyList<string>>(Values.Keys.ToList());
    }

    [Test]
    public async Task EncryptedFile_RoundTrips_WithoutWritingTheValueInClear()
    {
        using var temp = new TempDirectory();
        var store = new EncryptedFileSecretStore(temp.Paths, new FakeProtector());

        await store.SetAsync("alpha-github-pat", "ghp_secret_value");

        var reloaded = new EncryptedFileSecretStore(temp.Paths, new FakeProtector());
        Assert.That(await reloaded.GetAsync("alpha-github-pat"), Is.EqualTo("ghp_secret_value"));
        Assert.That(await reloaded.ListAsync(), Is.EqualTo(new[] { "alpha-github-pat" }));
        var onDisk = File.ReadAllText(store.FilePath);
        Assert.That(onDisk, Does.Not.Contain("ghp_secret_value"));
        Assert.That(onDisk, Does.Contain("\"schemaVersion\": 1"));
    }

    [Test]
    public async Task EncryptedFile_DeleteAndMissing()
    {
        using var temp = new TempDirectory();
        var store = new EncryptedFileSecretStore(temp.Paths, new FakeProtector());
        await store.SetAsync("a", "1");

        Assert.That(await store.DeleteAsync("a"), Is.True);
        Assert.That(await store.DeleteAsync("a"), Is.False);
        Assert.That(await store.GetAsync("a"), Is.Null);
    }

    [Test]
    public async Task EncryptedFile_ValueFromAnotherAccount_GivesAClearError()
    {
        using var temp = new TempDirectory();
        temp.Write("secrets.json", """{ "schemaVersion": 1, "secrets": { "a": "AAAA" } }""");
        var store = new EncryptedFileSecretStore(temp.Paths, new FakeProtector());

        var ex = Assert.ThrowsAsync<CryptographicException>(() => store.GetAsync("a"));
        Assert.That(ex!.Message, Does.Contain("Enter it again"));
    }

    [TestCase("")]
    [TestCase("../x")]
    [TestCase("has space")]
    public void InvalidReference_IsRejected(string secretRef)
    {
        using var temp = new TempDirectory();
        var store = new EncryptedFileSecretStore(temp.Paths, new FakeProtector());

        Assert.ThrowsAsync<ArgumentException>(() => store.SetAsync(secretRef, "x"));
    }

    [Test]
    public async Task Selectable_ForwardsToChosenStore_AndSwitchMovesSecrets()
    {
        using var temp = new TempDirectory();
        var settings = new UserConfigGlobalSettingsService(new UserConfigurationStore(temp.Paths));
        var vault = new MemoryStore();
        var file = new MemoryStore();
        var store = new SelectableSecretStore(new Dictionary<SecretStoreKind, ISecretStore>
        {
            [SecretStoreKind.CredentialManager] = vault,
            [SecretStoreKind.EncryptedFile] = file,
        }, settings);

        await store.SetAsync("a", "1");
        await store.SetAsync("b", "2");
        Assert.That(store.Current, Is.EqualTo(SecretStoreKind.CredentialManager));
        Assert.That(vault.Values, Has.Count.EqualTo(2));

        var moved = await store.SwitchToAsync(SecretStoreKind.EncryptedFile);

        Assert.That(moved, Is.EqualTo(2));
        Assert.That(file.Values, Is.EqualTo(new Dictionary<string, string> { ["a"] = "1", ["b"] = "2" }));
        Assert.That(vault.Values, Is.Empty);
        Assert.That(await store.GetAsync("a"), Is.EqualTo("1"));
        Assert.That(new UserConfigurationStore(temp.Paths).Current.Settings.SecretStore, Is.EqualTo(SecretStoreKind.EncryptedFile));
        Assert.That(await store.SwitchToAsync(SecretStoreKind.EncryptedFile), Is.EqualTo(0));
    }

    [Test]
    public void Selectable_UsesFirstAvailable_WhenChoiceIsNotOnThisHost_AndRejectsUnknownTarget()
    {
        using var temp = new TempDirectory();
        var settings = new UserConfigGlobalSettingsService(new UserConfigurationStore(temp.Paths));
        var store = new SelectableSecretStore(new Dictionary<SecretStoreKind, ISecretStore>
        {
            [SecretStoreKind.EncryptedFile] = new MemoryStore(),
        }, settings);

        Assert.That(store.Current, Is.EqualTo(SecretStoreKind.EncryptedFile));
        Assert.ThrowsAsync<ArgumentException>(() => store.SwitchToAsync(SecretStoreKind.CredentialManager));
    }

    [Test]
    public void SecretStoreSetting_IsWrittenAsText()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new GlobalSettings { SecretStore = SecretStoreKind.EncryptedFile });

        Assert.That(json, Does.Contain("\"secretStore\":\"EncryptedFile\""));
    }
}
