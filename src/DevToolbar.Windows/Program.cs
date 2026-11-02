using System.Windows;
using DevToolbar.Core.Events;
using DevToolbar.Core.Interfaces;
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

namespace DevToolbar.Windows;

/// <summary>WPF entry point: builds the service container and shows the single toolbar window.</summary>
public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var services = new ServiceCollection();
        services.AddWpfBlazorWebView();
#if DEBUG
        services.AddBlazorWebViewDeveloperTools();
#endif

        // Host window bridge (HWND, dismissal of popups on native events).
        services.AddSingleton<ToolbarHost>();
        services.AddSingleton<PopupDismissService>();

        // Native services shared with the MAUI host (linked sources).
        services.AddSingleton<IProcessService, WindowsProcessService>();
        services.AddSingleton<IFileSystemService, WindowsFileSystemService>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IScriptService, WindowsScriptService>();
        services.AddSingleton<IGlobalSettingsService, JsonGlobalSettingsService>();
        services.AddSingleton<WindowsWindowService>();
        services.AddSingleton<IWindowService>(sp => sp.GetRequiredService<WindowsWindowService>());

        // WPF-specific services (no MAUI, no SetWindowRgn).
        services.AddSingleton<INativeService, WpfNativeService>();
        services.AddSingleton<WpfAppBarService>();
        services.AddSingleton<IDockingService>(sp => sp.GetRequiredService<WpfAppBarService>());
        services.AddSingleton<IWindowRegionService, NoOpWindowRegionService>();

        // Plugins — the work item / time / CI providers are still the placeholder
        // implementations used by the MAUI host (not real services yet).
        services.AddSingleton<IPlugin, GitPlugin>();
        services.AddSingleton<IWorkItemProvider, MockWorkItemProvider>();
        services.AddSingleton<IPlugin, WorkItemsPlugin>();
        services.AddSingleton<ITimeTrackingService, MockTimeTrackingService>();
        services.AddSingleton<IPlugin, TimeTrackerPlugin>();
        services.AddSingleton<ICiCdService, MockCiCdService>();
        services.AddSingleton<IPlugin, GithubAgentPlugin>();
        services.AddSingleton<IPluginLoader, PluginLoader>();

        // UI services.
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<ThemeService>();
        services.AddSingleton<EventAggregator>();

        using var provider = services.BuildServiceProvider();

        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new ToolbarWindow(provider);
        app.MainWindow = window;
        return app.Run(window);
    }
}
