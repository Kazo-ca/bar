namespace DevToolbar.Infrastructure.Configuration;

using System.Text.Json.Nodes;
using DevToolbar.Infrastructure.Storage;

/// <summary>
/// Version 0 (before US2.5) to version 1 of <c>config.json</c>.
/// <list type="bullet">
/// <item><c>ActiveProjectId</c> / <c>Projects</c> (written in PascalCase) become <c>activeProjectId</c> / <c>projects</c>.</item>
/// <item><c>gitRepositories</c> becomes <c>repositories</c>, and each repository's <c>path</c> becomes <c>localPath</c>.</item>
/// <item>A project without repositories but with a <c>path</c> gets one repository for that folder.</item>
/// <item>The <c>settings</c> section is kept as provided (the store puts the old <c>global-settings.json</c> there).</item>
/// </list>
/// </summary>
public sealed class ConfigV0ToV1Migration : IJsonMigration
{
    public int FromVersion => 0;

    public JsonObject Apply(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var source = (JsonObject)document.DeepClone();
        var result = new JsonObject();

        var activeProjectId = Take(source, "activeProjectId");
        if (activeProjectId != null)
            result["activeProjectId"] = activeProjectId;

        if (Take(source, "settings") is JsonObject settings)
            result["settings"] = settings;

        var projects = new JsonArray();
        if (Take(source, "projects") is JsonArray oldProjects)
        {
            foreach (var project in oldProjects.OfType<JsonObject>())
                projects.Add(MigrateProject(project));
        }
        result["projects"] = projects;
        return result;
    }

    private static JsonObject MigrateProject(JsonObject project)
    {
        var result = (JsonObject)project.DeepClone();
        var repositories = new JsonArray();
        if ((Take(result, "gitRepositories") ?? Take(result, "repositories")) is JsonArray oldRepositories)
        {
            foreach (var repository in oldRepositories.OfType<JsonObject>())
                repositories.Add(MigrateRepository(repository));
        }

        if (repositories.Count == 0 && result["path"] is JsonValue pathValue
            && pathValue.TryGetValue<string>(out var path) && !string.IsNullOrWhiteSpace(path))
        {
            repositories.Add(new JsonObject
            {
                ["name"] = result["name"]?.DeepClone() ?? path,
                ["localPath"] = path,
                ["remoteUrl"] = result["repositoryUrl"]?.DeepClone() ?? string.Empty,
                ["defaultBranch"] = result["defaultBranch"]?.DeepClone() ?? string.Empty,
            });
        }

        result["repositories"] = repositories;
        return result;
    }

    private static JsonObject MigrateRepository(JsonObject repository)
    {
        var result = (JsonObject)repository.DeepClone();
        if (!result.ContainsKey("localPath") && Take(result, "path") is { } path)
            result["localPath"] = path;
        return result;
    }

    /// <summary>Removes and returns a property, matching its name without regard to case.</summary>
    private static JsonNode? Take(JsonObject obj, string name)
    {
        var key = obj.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, name, StringComparison.OrdinalIgnoreCase));
        if (key == null)
            return null;
        var value = obj[key];
        obj.Remove(key);
        return value;
    }
}
