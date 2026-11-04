namespace Kazo.Bar.Core.Data;

/// <summary>
/// Interface for database schema migrations (US8.3).
/// Implementations handle creating/updating SQLite tables.
/// </summary>
public interface IDbMigration
{
    /// <summary>Migration version number (applied in ascending order).</summary>
    int Version { get; }

    /// <summary>Description of what this migration does.</summary>
    string Description { get; }

    /// <summary>SQL statements to apply this migration.</summary>
    IReadOnlyList<string> UpStatements { get; }
}
