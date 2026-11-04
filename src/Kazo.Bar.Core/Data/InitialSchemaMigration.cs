namespace Kazo.Bar.Core.Data;

/// <summary>
/// Initial SQLite schema migration (US8.3).
/// Creates the core tables for time tracking, plugin data, and configuration.
/// Target: %APPDATA%\DevToolbar\data.db
/// </summary>
public class InitialSchemaMigration : IDbMigration
{
    public int Version => 1;
    public string Description => "Create core tables: TimeEntries, PluginData, ConfigCache, MigrationHistory";

    public IReadOnlyList<string> UpStatements => new[]
    {
        // Migration history table (tracks which migrations have been applied)
        """
        CREATE TABLE IF NOT EXISTS MigrationHistory (
            Version     INTEGER PRIMARY KEY,
            Description TEXT    NOT NULL,
            AppliedAt   TEXT    NOT NULL DEFAULT (datetime('now'))
        );
        """,

        // Time entries table (US5.3/US5.4 - time tracking with work item association)
        """
        CREATE TABLE IF NOT EXISTS TimeEntries (
            Id          TEXT    PRIMARY KEY,
            ProjectId   TEXT    NOT NULL,
            WorkItemId  TEXT,
            StartTime   TEXT    NOT NULL,
            EndTime     TEXT,
            Description TEXT    NOT NULL DEFAULT '',
            CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now'))
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_TimeEntries_ProjectId ON TimeEntries (ProjectId);",
        "CREATE INDEX IF NOT EXISTS IX_TimeEntries_StartTime ON TimeEntries (StartTime);",

        // Plugin data table (key-value storage scoped by plugin and project)
        """
        CREATE TABLE IF NOT EXISTS PluginData (
            Namespace   TEXT    NOT NULL,
            Key         TEXT    NOT NULL,
            Value       TEXT    NOT NULL,
            UpdatedAt   TEXT    NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (Namespace, Key)
        );
        """,

        // Plugin collections table (typed collection storage for plugins)
        """
        CREATE TABLE IF NOT EXISTS PluginCollections (
            Id          INTEGER PRIMARY KEY AUTOINCREMENT,
            Namespace   TEXT    NOT NULL,
            Collection  TEXT    NOT NULL,
            ItemJson    TEXT    NOT NULL,
            CreatedAt   TEXT    NOT NULL DEFAULT (datetime('now'))
        );
        """,
        "CREATE INDEX IF NOT EXISTS IX_PluginCollections_NsCol ON PluginCollections (Namespace, Collection);",

        // CI/CD session read state (US7.2 - mark as read persistence)
        """
        CREATE TABLE IF NOT EXISTS CiCdReadState (
            SessionId   TEXT    NOT NULL,
            ProjectId   TEXT    NOT NULL,
            ReadAt      TEXT    NOT NULL DEFAULT (datetime('now')),
            PRIMARY KEY (SessionId, ProjectId)
        );
        """,

        // Configuration cache (JSON config hierarchy resolution)
        """
        CREATE TABLE IF NOT EXISTS ConfigCache (
            ConfigPath  TEXT    PRIMARY KEY,
            ConfigJson  TEXT    NOT NULL,
            LastHash    TEXT    NOT NULL,
            CachedAt    TEXT    NOT NULL DEFAULT (datetime('now'))
        );
        """
    };
}
