using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Maui.Themes;
#if WINDOWS
using Kazo.Bar.Maui.Platforms.Windows;
using Kazo.Bar.Maui.Platforms.Windows.Services;
#endif

namespace Kazo.Bar.Maui;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App(IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _serviceProvider = serviceProvider;

        // Initialize default theme for XAML toolbar
        ThemeResourceManager.Initialize("Light");
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // ── Blazor toolbar window (kept for compatibility) ──
        var blazorWindow = new Window(new MainPage()) { Title = "Kazo Bar" };

        blazorWindow.Created += async (s, e) =>
        {
#if WINDOWS
            // Initialize global settings and apply docking
            var globalSettings = _serviceProvider.GetRequiredService<IGlobalSettingsService>();
            await globalSettings.LoadAsync();

            // Pass our HWND to WindowsWindowService so it can exclude itself from enumeration
            var windowService = _serviceProvider.GetService<WindowsWindowService>();
            var dockingService = _serviceProvider.GetService<WindowsAppBarService>();
            if (windowService != null && dockingService != null)
            {
                windowService.SetOwnWindowHandle(dockingService.GetWindowHandle());
            }

            // Apply saved toolbar behavior (docking, position, monitor)
            var docking = _serviceProvider.GetService<IDockingService>();
            docking?.ApplySettings(globalSettings.Current);

            // Re-apply when settings change at runtime
            globalSettings.OnSettingsChanged += settings =>
            {
                docking?.ApplySettings(settings);
            };
#endif

            // ── Open the native XAML toolbar as a secondary window ──
            var toolbarWindow = new Window(new Views.NativeToolbarPage())
            {
                Title = "Kazo Bar XAML Toolbar",
            };
            Application.Current?.OpenWindow(toolbarWindow);
        };

        return blazorWindow;
    }
}

