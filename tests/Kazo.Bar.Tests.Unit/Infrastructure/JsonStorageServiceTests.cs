namespace Kazo.Bar.Tests.Unit.Infrastructure;

using System.Text.Json.Nodes;
using Kazo.Bar.Infrastructure.Storage;

[TestFixture]
public class JsonStorageServiceTests
{
    private sealed record Entry(string Id, int Minutes);

    [Test]
    public async Task Values_RoundTrip_AndSurviveANewInstance()
    {
        using var temp = new TempDirectory();
        var storage = new JsonStorageService(temp.Paths);

        await storage.SetAsync("github-agents", "read", new[] { "run-1", "run-2" });

        var reloaded = new JsonStorageService(temp.Paths);
        Assert.That(await reloaded.GetAsync<string[]>("github-agents", "read"), Is.EqualTo(new[] { "run-1", "run-2" }));
        Assert.That(await reloaded.GetKeysAsync("github-agents"), Is.EqualTo(new[] { "read" }));
        Assert.That(await reloaded.GetAsync<string[]>("github-agents", "missing"), Is.Null);
    }

    [Test]
    public async Task EveryFile_CarriesSchemaVersion()
    {
        using var temp = new TempDirectory();
        var storage = new JsonStorageService(temp.Paths);

        await storage.SetAsync("time", "k", 1);

        var document = JsonNode.Parse(File.ReadAllText(storage.FileFor("time")))!;
        Assert.That(document["schemaVersion"]!.GetValue<int>(), Is.EqualTo(JsonStorageService.CurrentSchemaVersion));
        Assert.That(Path.GetDirectoryName(storage.FileFor("time")), Is.EqualTo(temp.Paths.DataDirectory));
    }

    [Test]
    public async Task Delete_RemovesKey()
    {
        using var temp = new TempDirectory();
        var storage = new JsonStorageService(temp.Paths);
        await storage.SetAsync("ns", "a", 1);
        await storage.SetAsync("ns", "b", 2);

        await storage.DeleteAsync("ns", "a");

        Assert.That(await storage.GetKeysAsync("ns"), Is.EqualTo(new[] { "b" }));
    }

    [Test]
    public async Task Collections_AddQueryAndClear()
    {
        using var temp = new TempDirectory();
        var storage = new JsonStorageService(temp.Paths);
        await storage.AddToCollectionAsync("time", "entries", new Entry("1", 10));
        await storage.AddToCollectionAsync("time", "entries", new Entry("2", 45));

        var reloaded = new JsonStorageService(temp.Paths);
        Assert.That(await reloaded.QueryCollectionAsync<Entry>("time", "entries"), Has.Count.EqualTo(2));
        Assert.That((await reloaded.QueryCollectionAsync<Entry>("time", "entries", e => e.Minutes > 30)).Single().Id, Is.EqualTo("2"));

        await reloaded.ClearCollectionAsync("time", "entries");
        Assert.That(await reloaded.QueryCollectionAsync<Entry>("time", "entries"), Is.Empty);
    }

    [TestCase("")]
    [TestCase("..")]
    [TestCase("../config")]
    [TestCase("a/b")]
    [TestCase("a\\b")]
    public void InvalidNamespace_IsRejected(string ns)
    {
        using var temp = new TempDirectory();
        var storage = new JsonStorageService(temp.Paths);

        Assert.ThrowsAsync<ArgumentException>(() => storage.SetAsync(ns, "k", 1));
    }

    [Test]
    public void NewerFile_IsNotOverwritten()
    {
        using var temp = new TempDirectory();
        var path = temp.Write(Path.Combine("data", "ns.json"), """{ "schemaVersion": 7 }""");
        var storage = new JsonStorageService(temp.Paths);

        Assert.ThrowsAsync<SchemaVersionTooNewException>(() => storage.SetAsync("ns", "k", 1));
        Assert.That(File.ReadAllText(path), Is.EqualTo("""{ "schemaVersion": 7 }"""));
    }

    [Test]
    public async Task InvalidJson_IsBackedUp_AndStartsEmpty()
    {
        using var temp = new TempDirectory();
        temp.Write(Path.Combine("data", "ns.json"), "{ broken");
        var storage = new JsonStorageService(temp.Paths);

        Assert.That(await storage.GetKeysAsync("ns"), Is.Empty);
        Assert.That(temp.Backups().Single(), Does.StartWith("ns.unreadable."));
    }
}
