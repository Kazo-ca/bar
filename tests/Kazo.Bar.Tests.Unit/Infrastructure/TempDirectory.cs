namespace Kazo.Bar.Tests.Unit.Infrastructure;

using Kazo.Bar.Infrastructure.Storage;

/// <summary>A temporary folder standing in for %APPDATA%\DevToolbar, deleted after the test.</summary>
internal sealed class TempDirectory : IDisposable
{
    public TempDirectory()
    {
        Root = Path.Combine(Path.GetTempPath(), "devtoolbar-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Paths = new AppDataPaths(Root);
    }

    public string Root { get; }
    public AppDataPaths Paths { get; }

    public string Write(string relativePath, string content)
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string[] Backups() => Directory.Exists(Paths.BackupDirectory)
        ? Directory.GetFiles(Paths.BackupDirectory).Select(p => Path.GetFileName(p)).ToArray()
        : [];

    public void Dispose()
    {
        if (!Directory.Exists(Root)) return;
        try
        {
            // Git marks .git/objects files read-only; Windows refuses to delete those.
            foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(Root, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
