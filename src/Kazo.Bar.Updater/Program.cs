using System.Windows;
using Kazo.Bar.Updates;

namespace Kazo.Bar.Updater;

/// <summary>
/// Kazo Bar Updater: closes Kazo Bar, installs the newest GitHub release, starts Kazo Bar again.
/// Exit codes: 0 updated or up to date, 1 failed (previous version restored), 2 cancelled, 3 bad arguments, 4 already running.
/// </summary>
public static class Program
{
    private const string SingleInstanceName = @"Local\Kazo.Bar.Updater";

    [STAThread]
    public static int Main(string[] args)
    {
        UpdaterArguments arguments;
        try
        {
            arguments = UpdaterArguments.Parse(args, AppContext.BaseDirectory);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show($"{ex.Message}\n\nUsage: Kazo.Bar.Updater.exe [--install-dir <folder>] [--tag <v0.2.0>] [--force] [--no-start] [--stable-only]",
                "Kazo Bar Updater", MessageBoxButton.OK, MessageBoxImage.Warning);
            return 3;
        }

        if (Relocation.TryRelocate(arguments)) return 0;

        using var single = new Mutex(initiallyOwned: true, SingleInstanceName, out var isFirst);
        if (!isFirst)
        {
            UpdaterLog.Write("Another updater is already running; exiting.");
            return 4;
        }

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            UpdaterLog.Write($"FATAL (UI thread): {e.Exception}");
            e.Handled = true;
            app.Shutdown(1);
        };
        var window = new UpdaterWindow(arguments);
        app.Run(window);
        return window.ExitCode;
    }
}
