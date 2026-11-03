namespace DevToolbar.Infrastructure.Storage;

using System.Text.Json.Nodes;

/// <summary>
/// Upgrades a JSON document from <see cref="FromVersion"/> to <c>FromVersion + 1</c>.
/// Migrations work on the raw JSON tree, so they keep working when the C# models change later.
/// </summary>
public interface IJsonMigration
{
    /// <summary>Schema version this migration reads. A file without <c>schemaVersion</c> is version 0.</summary>
    int FromVersion { get; }

    /// <summary>
    /// Returns the upgraded document. The input must not be modified: work on a clone.
    /// The runner sets <c>schemaVersion</c> afterwards.
    /// </summary>
    JsonObject Apply(JsonObject document);
}
