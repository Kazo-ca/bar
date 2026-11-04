namespace Kazo.Bar.Core.Interfaces;

/// <summary>
/// Abstracts file system operations.
/// </summary>
public interface IFileSystemService
{
    /// <summary>Check if a file exists.</summary>
    bool FileExists(string path);

    /// <summary>Check if a directory exists.</summary>
    bool DirectoryExists(string path);

    /// <summary>Read all text from a file.</summary>
    Task<string> ReadAllTextAsync(string path);

    /// <summary>Write text to a file.</summary>
    Task WriteAllTextAsync(string path, string content);
}
