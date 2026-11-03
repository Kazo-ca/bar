namespace DevToolbar.Infrastructure.Storage;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>Thrown when a file was written by a newer DevToolbar than the one running.</summary>
public sealed class SchemaVersionTooNewException(string path, int fileVersion, int supportedVersion)
    : Exception($"'{path}' has schema version {fileVersion}; this DevToolbar supports up to {supportedVersion}.")
{
    public int FileVersion { get; } = fileVersion;
    public int SupportedVersion { get; } = supportedVersion;
}

/// <summary>
/// Reading, migrating and writing JSON files that carry a <c>schemaVersion</c> property (US2.5).
/// </summary>
public static class VersionedJson
{
    public const string VersionProperty = "schemaVersion";

    public static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Schema version of a document; 0 when the property is missing or not a number.</summary>
    public static int ReadVersion(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return document[VersionProperty] is JsonValue value && value.TryGetValue<int>(out var version) ? version : 0;
    }

    /// <summary>
    /// Applies the migrations needed to bring <paramref name="document"/> to <paramref name="targetVersion"/>.
    /// Returns a new document; the input is left untouched.
    /// </summary>
    /// <exception cref="SchemaVersionTooNewException">The document is newer than <paramref name="targetVersion"/>.</exception>
    /// <exception cref="InvalidOperationException">A migration step is missing.</exception>
    public static JsonObject Migrate(JsonObject document, int targetVersion, IEnumerable<IJsonMigration> migrations, string sourceName)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(migrations);

        var version = ReadVersion(document);
        if (version > targetVersion)
            throw new SchemaVersionTooNewException(sourceName, version, targetVersion);

        var steps = migrations.ToDictionary(m => m.FromVersion);
        var current = (JsonObject)document.DeepClone();
        while (version < targetVersion)
        {
            if (!steps.TryGetValue(version, out var step))
                throw new InvalidOperationException($"No migration from schema version {version} for '{sourceName}'.");
            current = step.Apply(current);
            version++;
            current[VersionProperty] = version;
        }
        return current;
    }

    /// <summary>Reads a file as a JSON object, or returns null when it does not exist.</summary>
    /// <exception cref="JsonException">The file is not a JSON object.</exception>
    public static JsonObject? ReadFile(string path)
    {
        if (!File.Exists(path))
            return null;
        var text = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(text))
            return new JsonObject();
        return JsonNode.Parse(text, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject
            ?? throw new JsonException($"'{path}' does not contain a JSON object.");
    }

    /// <summary>
    /// Writes <paramref name="content"/> next to <paramref name="path"/> then moves it over the target,
    /// so a crash during the write never leaves a half-written file.
    /// </summary>
    public static async Task WriteFileAtomicAsync(string path, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporary, content, cancellationToken);
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    /// <summary>
    /// Copies <paramref name="path"/> into <paramref name="backupDirectory"/> as
    /// <c>name.{label}.{timestamp}.ext</c> and returns the copy's path, or null when there was nothing to copy.
    /// </summary>
    public static string? Backup(string path, string backupDirectory, string label, DateTimeOffset now)
    {
        if (!File.Exists(path))
            return null;
        Directory.CreateDirectory(backupDirectory);
        var name = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        var target = Path.Combine(backupDirectory, $"{name}.{label}.{now:yyyyMMdd-HHmmss}{extension}");
        for (var i = 1; File.Exists(target); i++)
            target = Path.Combine(backupDirectory, $"{name}.{label}.{now:yyyyMMdd-HHmmss}-{i}{extension}");
        File.Copy(path, target);
        return target;
    }
}
