using System.Windows;
using DevToolbar.Core.Events;
using DevToolbar.Core.Interfaces;
using DevToolbar.Core.Models;
using DevToolbar.Infrastructure.Providers;
using DevToolbar.Infrastructure.Secrets;
using DevToolbar.Infrastructure.Configuration;
using DevToolbar.Infrastructure.Storage;
using DevToolbar.Infrastructure.TimeTracking;
using DevToolbar.Maui.Platforms.Windows.Services;
using DevToolbar.Maui.Services;
using DevToolbar.Plugins.Git;
using DevToolbar.Plugins.GithubAgents;
using DevToolbar.Plugins.Services;
using DevToolbar.Plugins.TimeTracker;
using DevToolbar.Plugins.WorkItems;
using DevToolbar.UI.Services;
using DevToolbar.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DevToolbar.Windows;

/// <summary>WPF entry point: builds the service container and shows the single toolbar window.</summary>
public static class Program
{
    /// <summary>%LOCALAPPDATA%\DevToolbar\logs\windows-host.log — startup and crash diagnostics.</summary>
    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DevToolbar", "logs", "windows-host.log");

    public static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} [{Environment.ProcessId}] {message}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void Fatal(string where, Exception ex)
    {
        Log($"FATAL ({where}): {ex}");
        MessageBox.Show($"{ex.GetType().Name}: {ex.Message}\n\nDetails: {LogPath}", "DevToolbar — startup error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    [STAThread]
    public static int Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log($"FATAL (AppDomain): {e.ExceptionObject}");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log($"Unobserved task exception: {e.Exception}");
            e.SetObserved();
        };

        try
        {
            Log($"Starting. OS={Environment.OSVersion}, CLR={Environment.Version}, base={AppContext.BaseDirectory}");
            return Run();
        }
        catch (Exception ex)
        {
            Fatal("Main", ex);
            return 1;
        }
    }

    private static int Run()
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
        services.AddLogging(logging => logging.AddProvider(new FileLoggerProvider()));
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        // Host window bridge (HWND, dismissal of popups on native events).
        services.AddSingleton<ToolbarHost>();
        services.AddSingleton<PopupDismissService>();
        services.AddSingleton<TrayIconService>();

        // Native services shared with the MAUI host (linked sources).
        services.AddSingleton<IProcessService, WindowsProcessService>();
        services.AddSingleton<IFileSystemService, WindowsFileSystemService>();
        services.AddSingleton<IScriptService, DevToolbar.Infrastructure.Processes.ProcessScriptService>();

        // Single per-user configuration file and versioned JSON data in %APPDATA%\DevToolbar (US2.3, US2.5, US8.3).
        services.AddSingleton(AppDataPaths.ForCurrentUser());
        services.AddSingleton<UserConfigurationStore>();
        services.AddSingleton<ISettingsService, UserConfigSettingsService>();
        services.AddSingleton<IGlobalSettingsService, UserConfigGlobalSettingsService>();
        services.AddSingleton<IStorageService, JsonStorageService>();

        // Access tokens: Windows Credential Manager or a DPAPI-encrypted file, as chosen in Settings (US2.4).
        services.AddSingleton(sp => new SelectableSecretStore(
            new Dictionary<SecretStoreKind, ISecretStore>
            {
                [SecretStoreKind.CredentialManager] = new CredentialManagerSecretStore(),
                [SecretStoreKind.EncryptedFile] = new EncryptedFileSecretStore(sp.GetRequiredService<AppDataPaths>(), new DpapiSecretProtector()),
            },
            sp.GetRequiredService<IGlobalSettingsService>()));
        services.AddSingleton<ISecretStore>(sp => sp.GetRequiredService<SelectableSecretStore>());
        services.AddSingleton<ISecretStoreSelector>(sp => sp.GetRequiredService<SelectableSecretStore>());
        services.AddSingleton<WindowsWindowService>();
        services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<WindowsWindowService>());

        // WPF-specific services (no MAUI, no SetWindowRgn).
        services.AddSingleton<INativeService, WpfNativeService>();
        services.AddSingleton<WpfAppBarService>();
        services.AddSingleton<IDockingService>(sp => sp.GetRequiredService<WpfAppBarService>());
        services.AddSingleton<IWindowRegionService, NoOpWindowRegionService>();
        services.AddSingleton<IWindowDragService, WpfWindowDragService>();
        services.AddSingleton<IFolderPickerService, WpfFolderPickerService>();
        // Two-way sizing: components declare the room they need, the window grows within the work area.
        services.AddSingleton<WpfHostSurfaceService>();
        services.AddSingleton<IHostSurfaceService>(sp => sp.GetRequiredService<WpfHostSurfaceService>());

        // Plugins.
        services.AddSingleton<IGitService, DevToolbar.Infrastructure.Git.GitService>();
        services.AddSingleton<IPlugin, GitPlugin>();
        // Real providers (US5.2, US7.1): Azure DevOps and GitHub, configured per repository.
        services.AddSingleton(_ => new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) });
        services.AddSingleton<GitHubApi>();
        services.AddSingleton<IWorkItemSourceClient, AzureDevOpsWorkItemsClient>();
        services.AddSingleton<IWorkItemSourceClient, GitHubIssuesClient>();
        services.AddSingleton<IWorkItemProvider, RepositoryWorkItemProvider>();
        services.AddSingleton<IPlugin, WorkItemsPlugin>();
        // Real time tracking: session lock and keyboard/mouse inactivity, JSON per month (US5.3, US5.5).
        services.AddSingleton<WindowsUserPresenceService>();
        services.AddSingleton<IUserPresenceService>(sp => sp.GetRequiredService<WindowsUserPresenceService>());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TimeTrackingService>();
        services.AddSingleton<ITimeTrackingService>(sp => sp.GetRequiredService<TimeTrackingService>());
        services.AddSingleton<IPlugin, TimeTrackerPlugin>();
        services.AddSingleton<ICiCdService, GitHubActionsService>();
        services.AddSingleton<IPlugin, GithubAgentPlugin>();
        services.AddSingleton<IPluginLoader, PluginLoader>();

        // UI services.
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<EventAggregator>();

        using var provider = services.BuildServiceProvider();

        // Load today and close an entry a crash left open, off the UI thread (no dispatcher to deadlock on).
        var timeTracking = provider.GetRequiredService<TimeTrackingService>();
        Task.Run(() => timeTracking.InitializeAsync()).GetAwaiter().GetResult();

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        app.DispatcherUnhandledException += (_, e) =>
        {
            Fatal("UI thread", e.Exception);
            e.Handled = true;
            app.Shutdown(1);
        };
        var window = new ToolbarWindow(provider);
        app.MainWindow = window;
        // Tray icon: show / hide / quit even when the toolbar is hidden or transparent.
        var tray = provider.GetRequiredService<TrayIconService>();
        window.SourceInitialized += (_, _) => tray.Show();
        app.Exit += (_, _) =>
        {
            tray.Dispose();
            // Closing the toolbar ends the work day: stop the timer and record the time (US5.5).
            try
            {
                Task.Run(timeTracking.ShutdownAsync).Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                Log($"Time tracking shutdown failed: {ex.InnerException}");
            }
        };
        var code = app.Run(window);
        Log($"Exited with code {code}.");
        return code;
    }
}
