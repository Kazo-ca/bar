namespace Kazo.Bar.Web.Mocks;

using Kazo.Bar.Core.Interfaces;

/// <summary>
/// Mock file system service for web testing.
/// </summary>
public class MockFileSystemService : IFileSystemService
{
    public bool FileExists(string path) => false;
    public bool DirectoryExists(string path) => false;
    public Task<string> ReadAllTextAsync(string path) => Task.FromResult("{}");
    public Task WriteAllTextAsync(string path, string content) => Task.CompletedTask;
}
