namespace Kazo.Bar.Tests.Unit.Infrastructure;

using System.Text.Json.Nodes;
using Kazo.Bar.Infrastructure.Storage;

[TestFixture]
public class VersionedJsonTests
{
    private sealed class AddField(int from, string field) : IJsonMigration
    {
        public int FromVersion => from;
        public JsonObject Apply(JsonObject document)
        {
            var copy = (JsonObject)document.DeepClone();
            copy[field] = true;
            return copy;
        }
    }

    [Test]
    public void ReadVersion_IsZero_WhenPropertyMissing()
    {
        Assert.That(VersionedJson.ReadVersion(new JsonObject()), Is.EqualTo(0));
        Assert.That(VersionedJson.ReadVersion(new JsonObject { ["schemaVersion"] = "x" }), Is.EqualTo(0));
        Assert.That(VersionedJson.ReadVersion(new JsonObject { ["schemaVersion"] = 3 }), Is.EqualTo(3));
    }

    [Test]
    public void Migrate_AppliesEveryStepInOrder_AndLeavesInputUntouched()
    {
        var input = new JsonObject { ["a"] = 1 };

        var result = VersionedJson.Migrate(input, 2, [new AddField(1, "two"), new AddField(0, "one")], "test");

        Assert.That(result["one"]!.GetValue<bool>(), Is.True);
        Assert.That(result["two"]!.GetValue<bool>(), Is.True);
        Assert.That(VersionedJson.ReadVersion(result), Is.EqualTo(2));
        Assert.That(input.ContainsKey("one"), Is.False);
        Assert.That(input.ContainsKey("schemaVersion"), Is.False);
    }

    [Test]
    public void Migrate_Throws_WhenDocumentIsNewerThanSupported()
    {
        var input = new JsonObject { ["schemaVersion"] = 5 };

        var ex = Assert.Throws<SchemaVersionTooNewException>(() => VersionedJson.Migrate(input, 1, [], "config.json"));
        Assert.That(ex!.FileVersion, Is.EqualTo(5));
    }

    [Test]
    public void Migrate_Throws_WhenAStepIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() => VersionedJson.Migrate(new JsonObject(), 2, [new AddField(0, "one")], "x"));
    }

    [Test]
    public async Task WriteFileAtomic_ReplacesContent_AndLeavesNoTemporaryFile()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Root, "sub", "file.json");

        await VersionedJson.WriteFileAtomicAsync(path, "{\"a\":1}");
        await VersionedJson.WriteFileAtomicAsync(path, "{\"a\":2}");

        Assert.That(File.ReadAllText(path), Is.EqualTo("{\"a\":2}"));
        Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!), Has.Length.EqualTo(1));
    }

    [Test]
    public void Backup_CopiesWithLabelAndTimestamp_WithoutOverwritingAnEarlierCopy()
    {
        using var temp = new TempDirectory();
        var path = temp.Write("config.json", "{}");
        var now = new DateTimeOffset(2026, 11, 3, 9, 30, 0, TimeSpan.Zero);

        var first = VersionedJson.Backup(path, temp.Paths.BackupDirectory, "v0", now);
        var second = VersionedJson.Backup(path, temp.Paths.BackupDirectory, "v0", now);

        Assert.That(Path.GetFileName(first), Is.EqualTo("config.v0.20261103-093000.json"));
        Assert.That(second, Is.Not.EqualTo(first));
        Assert.That(File.Exists(path), Is.True);
        Assert.That(VersionedJson.Backup(Path.Combine(temp.Root, "missing.json"), temp.Paths.BackupDirectory, "v0", now), Is.Null);
    }

    [Test]
    public void ReadFile_ReturnsNull_WhenMissing_AndRejectsNonObjects()
    {
        using var temp = new TempDirectory();
        Assert.That(VersionedJson.ReadFile(Path.Combine(temp.Root, "none.json")), Is.Null);
        var array = temp.Write("array.json", "[1,2]");
        Assert.Throws<System.Text.Json.JsonException>(() => VersionedJson.ReadFile(array));
    }
}
