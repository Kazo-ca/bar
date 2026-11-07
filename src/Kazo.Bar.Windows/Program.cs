using System.Windows;
using Kazo.Bar.Core.Events;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.Infrastructure.Auth;
using Kazo.Bar.Infrastructure.Providers;
using Kazo.Bar.Infrastructure.Secrets;
using Kazo.Bar.Infrastructure.Configuration;
using Kazo.Bar.Infrastructure.Storage;
using Kazo.Bar.Infrastructure.TimeTracking;
using Kazo.Bar.Maui.Platforms.Windows.Services;
using Kazo.Bar.Maui.Services;
using Kazo.Bar.Plugins.Git;
using Kazo.Bar.Plugins.GithubAgents;
using Kazo.Bar.Plugins.Services;
using Kazo.Bar.Plugins.TimeTracker;
using Kazo.Bar.Plugins.WorkItems;
using Kazo.Bar.UI.Services;
using Kazo.Bar.Windows.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kazo.Bar.Windows;

/// <summary>WPF entry point: builds the service container and shows the single toolbar window.</summary>
public static class Program
{
    /// <summary>%LOCALAPPDATA%\Kazo\Bar\logs\windows-host.log — startup and crash diagnostics.</summary>
    public static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Kazo", "Bar", "logs", "windows-host.log");

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
        MessageBox.Show($"{ex.GetType().Name}: {ex.Message}\n\nDetails: {LogPath}", "Kazo Bar — startup error",
            MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// Copies %APPDATA%\DevToolbar to %APPDATA%\Kazo\Bar on the first start after the rename.
    /// If the copy fails, this run keeps using the old folder so the user never sees an empty configuration.
    /// </summary>
    private static AppDataPaths ResolveAppDataPaths()
    {
        try
        {
            var outcome = LegacyAppDataMigration.Migrate(AppDataPaths.LegacyCurrentUserRoot, AppDataPaths.CurrentUserRoot);
            Log($"User data folder: {AppDataPaths.CurrentUserRoot} (legacy migration: {outcome}).");
            return AppDataPaths.ForCurrentUser();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log($"Could not copy {AppDataPaths.LegacyCurrentUserRoot} to {AppDataPaths.CurrentUserRoot}; using the old folder for now. {ex}");
            return new AppDataPaths(AppDataPaths.LegacyCurrentUserRoot);
        }
    }

    private static CredentialManagerSecretStore CreateCredentialStore()
    {
        var store = new CredentialManagerSecretStore();
        MigrateLegacyCredentials(store);
        return store;
    }

    /// <summary>Moves Credential Manager secrets saved as DevToolbar:{ref} to Kazo.Bar:{ref}.</summary>
    private static void MigrateLegacyCredentials(CredentialManagerSecretStore store)
    {
        try
        {
            var moved = store.MigrateLegacySecrets();
            if (moved > 0)
                Log($"Moved {moved} secret(s) from the DevToolbar: to the Kazo.Bar: Credential Manager prefix.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            Log($"Could not move the DevToolbar: Credential Manager secrets; retried on next start. {ex.Message}");
        }
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
        // Silent update check every 30 minutes; installs go through Kazo.Bar.Updater.exe.
        services.AddSingleton<BarUpdateService>();
        // Dialogs (Settings, Time Report, Git...) open in their own windows sharing these singletons.
        services.AddSingleton<Kazo.Bar.Core.Dialogs.IDialogService, WpfDialogService>();

        // Native services shared with the MAUI host (linked sources).
        services.AddSingleton<IProcessService, WindowsProcessService>();
        services.AddSingleton<IFileSystemService, WindowsFileSystemService>();
        services.AddSingleton<IScriptService, Kazo.Bar.Infrastructure.Processes.ProcessScriptService>();

        // Single per-user configuration file and versioned JSON data in %APPDATA%\Kazo\Bar (US2.3, US2.5, US8.3).
        services.AddSingleton(ResolveAppDataPaths());
        services.AddSingleton<UserConfigurationStore>();
        services.AddSingleton<ISettingsService, UserConfigSettingsService>();
        services.AddSingleton<IGlobalSettingsService, UserConfigGlobalSettingsService>();
        services.AddSingleton<IStorageService, JsonStorageService>();

        // Access tokens: Windows Credential Manager or a DPAPI-encrypted file, as chosen in Settings (US2.4).
        services.AddSingleton(sp => new SelectableSecretStore(
            new Dictionary<SecretStoreKind, ISecretStore>
            {
                [SecretStoreKind.CredentialManager] = CreateCredentialStore(),
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
        services.AddSingleton<Kazo.Bar.Infrastructure.Git.GitService>();
        services.AddSingleton<IGitService>(sp => sp.GetRequiredService<Kazo.Bar.Infrastructure.Git.GitService>());
        services.AddSingleton<IGitRepositoryManager>(sp => sp.GetRequiredService<Kazo.Bar.Infrastructure.Git.GitService>());
        services.AddSingleton<IPlugin, GitPlugin>();
        // Real providers (US5.2, US7.1): Azure DevOps and GitHub, configured per repository.
        services.AddSingleton(_ => new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(20) });
        // OAuth sign-in (US2.4): device flow; access tokens renewed with the saved refresh token.
        services.AddSingleton(sp => new OAuthTokenClient(
            sp.GetRequiredService<System.Net.Http.HttpClient>(), sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton(sp => new AccessTokenResolver(
            sp.GetRequiredService<ISecretStore>(), sp.GetRequiredService<OAuthTokenClient>(),
            () => sp.GetRequiredService<IGlobalSettingsService>().Current.OAuth, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IOAuthSignInService>(sp => new DeviceCodeSignInService(
            sp.GetRequiredService<OAuthTokenClient>(), sp.GetRequiredService<ISecretStore>(),
            sp.GetRequiredService<AccessTokenResolver>(),
            () => sp.GetRequiredService<IGlobalSettingsService>().Current.OAuth, sp.GetRequiredService<TimeProvider>()));
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
        var updates = provider.GetRequiredService<BarUpdateService>();
        updates.UpdateAvailable += update => app.Dispatcher.BeginInvoke(() => tray.ShowUpdateAvailable(update));
        window.SourceInitialized += (_, _) =>
        {
            tray.Show();
            updates.Start();
        };
        using var updaterShutdown = ListenForUpdaterShutdown(app, tray);
        app.Exit += (_, _) =>
        {
            tray.Dispose();
            try
            {
                updates.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex)
            {
                Log($"Update check shutdown failed: {ex.InnerException}");
            }
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

    /// <summary>
    /// Kazo.Bar.Updater sets a named event to ask the toolbar to quit normally (time tracking saved)
    /// before it replaces the program files. Returns null when the event cannot be created.
    /// </summary>
    private static IDisposable? ListenForUpdaterShutdown(Application app, TrayIconService tray)
    {
        try
        {
            var signal = new EventWaitHandle(false, EventResetMode.ManualReset, Kazo.Bar.Updates.UpdateConstants.ShutdownEventName);
            // A previous updater may have left it set: do not quit at once.
            signal.Reset();
            var registration = ThreadPool.RegisterWaitForSingleObject(signal, (_, _) =>
                app.Dispatcher.BeginInvoke(() =>
                {
                    Log("Closing: Kazo Bar Updater is installing a new version.");
                    tray.Dispose();
                    app.Shutdown();
                }), null, Timeout.Infinite, executeOnlyOnce: true);
            return new UpdaterShutdownListener(signal, registration);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            Log($"Updater shutdown signal unavailable: {ex.Message}");
            return null;
        }
    }

    private sealed class UpdaterShutdownListener(EventWaitHandle signal, RegisteredWaitHandle registration) : IDisposable
    {
        public void Dispose()
        {
            registration.Unregister(null);
            signal.Dispose();
        }
    }
}
