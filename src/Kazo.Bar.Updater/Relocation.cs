using System.Diagnostics;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Updater;

/// <summary>
/// The updater ships in the install folder it replaces. Before touching anything it copies that folder
/// to %LOCALAPPDATA%\Kazo\Bar\updates\runner and restarts from there, so no file it runs from (or loads
/// later) is ever moved or swapped for a newer version mid-update.
/// </summary>
internal static class Relocation
{
    public static string RunnerFolder => Path.Combine(UpdateConstants.DefaultWorkFolder, "runner");

    /// <summary>True when a relocated copy was started and this process must exit.</summary>
    public static bool TryRelocate(UpdaterArguments arguments)
    {
        if (arguments.Relocated) return false;
        var baseFolder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(AppContext.BaseDirectory));
        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(arguments.InstallFolder));
        if (!string.Equals(baseFolder, install, StringComparison.OrdinalIgnoreCase)) return false;

        try
        {
            var runner = RunnerFolder;
            if (Directory.Exists(runner)) Directory.Delete(runner, recursive: true);
            CopyFolder(baseFolder, runner);
            var exe = Path.Combine(runner, UpdateConstants.UpdaterExecutableName);
            var start = new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = runner };
            foreach (var arg in (arguments with { InstallFolder = install, Relocated = true }).ToCommandLine())
                start.ArgumentList.Add(arg);
            using var process = Process.Start(start);
            UpdaterLog.Write($"Relocated to {runner} (process {process?.Id}).");
            return process != null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // Another updater still running from the runner folder, disk full...: update from here instead.
            UpdaterLog.Write($"Could not relocate the updater, running in place: {ex.Message}");
            return false;
        }
    }

    private static void CopyFolder(string source, string target)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            if (relative.StartsWith(UpdateInstaller.BackupFolderPrefix, StringComparison.OrdinalIgnoreCase)) continue;
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }
}
