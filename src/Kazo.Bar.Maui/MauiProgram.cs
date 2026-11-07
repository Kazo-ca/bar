using Kazo.Bar.Core.Events;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Maui.Services;
using Kazo.Bar.Plugins.Git;
using Kazo.Bar.Plugins.GithubAgents;
using Kazo.Bar.Plugins.Services;
using Kazo.Bar.Plugins.TimeTracker;
using Kazo.Bar.Plugins.WorkItems;
using Kazo.Bar.UI.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
#if WINDOWS
using Kazo.Bar.Maui.Platforms.Windows;
using Kazo.Bar.Maui.Platforms.Windows.Services;
#endif

namespace Kazo.Bar.Maui;

/// <summary>
/// MAUI application entry point with real service registrations.
/// </summary>
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
            })
            .ConfigureLifecycleEvents(events =>
            {
#if WINDOWS
                events.AddWindows(wle =>
                {
                    // Track whether the first (Blazor) window has been configured
                    bool blazorConfigured = false;
                    wle.OnWindowCreated(nativeWindow =>
                    {
                        if (!blazorConfigured)
                        {
                            blazorConfigured = true;
                            // Configure only the Blazor toolbar window (kept for compatibility)
                            WindowHelper.ConfigureToolbarWindow(nativeWindow);
                        }
                        // The XAML toolbar window configures itself in NativeToolbarPage.xaml.cs
                    });
                });
#endif
            });

        builder.Services.AddMauiBlazorWebView();

#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        // Register REAL native services
        builder.Services.AddSingleton<IProcessService, WindowsProcessService>();
        builder.Services.AddSingleton<INativeService, WindowsNativeService>();
        // TODO: Add SQLiteStorageService when SQLite package is added
        // builder.Services.AddSingleton<IStorageService, SqliteStorageService>();
        builder.Services.AddSingleton<IFileSystemService, WindowsFileSystemService>();
        builder.Services.AddSingleton<ISettingsService, JsonSettingsService>();
        builder.Services.AddSingleton<IScriptService, WindowsScriptService>();

        // Register window management & docking services
        builder.Services.AddSingleton<IGlobalSettingsService, JsonGlobalSettingsService>();
#if WINDOWS
        builder.Services.AddSingleton<WindowsWindowService>();
        builder.Services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<WindowsWindowService>());
        builder.Services.AddSingleton<WindowsAppBarService>();
        builder.Services.AddSingleton<IDockingService>(sp => sp.GetRequiredService<WindowsAppBarService>());
        builder.Services.AddSingleton<IWindowRegionService, WindowsRegionService>();
#endif

        // Register plugins (MAUI is frozen: only what the shared plugins need to start).
        builder.Services.AddSingleton<IGitService, Kazo.Bar.Infrastructure.Git.GitService>();
        builder.Services.AddSingleton<IPlugin, GitPlugin>();
        builder.Services.AddSingleton<IWorkItemProvider, MockWorkItemProvider>();
        builder.Services.AddSingleton<IPlugin, WorkItemsPlugin>();
        builder.Services.AddSingleton<ITimeTrackingService, MockTimeTrackingService>();
        builder.Services.AddSingleton<IPlugin, TimeTrackerPlugin>();
        builder.Services.AddSingleton<ICiCdService, MockCiCdService>();
        builder.Services.AddSingleton<IPlugin, GithubAgentPlugin>();
        builder.Services.AddSingleton<IPluginLoader, PluginLoader>();

        // Register UI services
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<ThemeService>();
        builder.Services.AddSingleton<EventAggregator>();
        // Frozen host: dialogs stay in the page (overlay root component in MainPage).
        builder.Services.AddScoped<Kazo.Bar.Core.Dialogs.IDialogService, Kazo.Bar.UI.Dialogs.InPageDialogService>();

        return builder.Build();
    }
}
