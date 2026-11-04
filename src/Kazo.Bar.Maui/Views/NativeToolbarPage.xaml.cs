using Kazo.Bar.Maui.ViewModels;

namespace Kazo.Bar.Maui.Views;

/// <summary>
/// Native XAML toolbar page. No Blazor — pure MAUI XAML controls.
/// On Windows, configures the host window as borderless, always-on-top,
/// and clips it to a pill shape using SetWindowRgn.
/// </summary>
public partial class NativeToolbarPage : ContentPage
{
    private readonly ToolbarViewModel _viewModel = new();
    private readonly CancellationTokenSource _cts = new();

    public NativeToolbarPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object? sender, EventArgs e)
    {
        Loaded -= OnPageLoaded;

#if WINDOWS
        // Defer to ensure the native window is fully created
        Dispatcher.Dispatch(ConfigureToolbarWindow);
#endif

        // Start the timer loop
        _ = _viewModel.StartTimerLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Handle project selection from the dropdown overlay.
    /// </summary>
    private void OnProjectSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ProjectItem item)
        {
            _viewModel.SelectProjectCommand.Execute(item);

            // Reset selection so the same item can be re-selected
            if (sender is CollectionView cv)
                cv.SelectedItem = null;
        }
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _cts.Cancel();
    }

#if WINDOWS
    // ── Constants ────────────────────────────────────────────────
    private const int ToolbarWidthCss = 960;
    private const int ToolbarHeightCss = 68;
    private const int CornerRadiusCss = 34;

    private nint _hWnd;

    private void ConfigureToolbarWindow()
    {
        if (this.Window?.Handler?.PlatformView is not Microsoft.UI.Xaml.Window nativeWindow)
            return;

        _hWnd = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
        var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(_hWnd);
        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
        if (appWindow == null) return;

        // 1. Borderless, always-on-top
        if (appWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
        }

        // 2. Remove system backdrop
        nativeWindow.SystemBackdrop = null;

        // 3. DPI-aware sizing
        double scale = GetDpiScale();
        int physW = (int)(ToolbarWidthCss * scale);
        int physH = (int)(500 * scale);         // tall enough for dropdown
        int physVisibleH = (int)(ToolbarHeightCss * scale);
        int physRadius = (int)(CornerRadiusCss * scale);

        appWindow.Resize(new global::Windows.Graphics.SizeInt32(physW, physH));

        // 4. Center horizontally at top of work area
        var display = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(
            windowId, Microsoft.UI.Windowing.DisplayAreaFallback.Primary);
        int x = (display.WorkArea.Width - physW) / 2;
        appWindow.Move(new global::Windows.Graphics.PointInt32(x, 0));

        // 5. Clip to pill shape (areas outside are invisible + click-through)
        nint hRgn = CreateRoundRectRgn(0, 0, physW + 1, physVisibleH + 1, physRadius, physRadius);
        SetWindowRgn(_hWnd, hRgn, true);

        // 6. Listen for dropdown open/close to update the region
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolbarViewModel.IsDropdownOpen))
            {
                MainThread.BeginInvokeOnMainThread(() =>
                    UpdateWindowRegion(physW, physVisibleH, physRadius, scale));
            }
        };
    }

    /// <summary>
    /// Expands or collapses the window region to include/exclude the dropdown.
    /// </summary>
    private void UpdateWindowRegion(int physW, int physVisibleH, int physRadius, double scale)
    {
        if (_hWnd == 0) return;

        if (_viewModel.IsDropdownOpen)
        {
            // Expand region to include dropdown area (toolbar + dropdown below)
            int dropdownHeight = (int)(200 * scale); // estimated dropdown max height
            int totalH = physVisibleH + dropdownHeight;
            nint hRgn = CreateRoundRectRgn(0, 0, physW + 1, totalH + 1, physRadius, physRadius);
            SetWindowRgn(_hWnd, hRgn, true);
        }
        else
        {
            // Collapse back to pill shape
            nint hRgn = CreateRoundRectRgn(0, 0, physW + 1, physVisibleH + 1, physRadius, physRadius);
            SetWindowRgn(_hWnd, hRgn, true);
        }
    }

    private double GetDpiScale()
    {
        uint dpi = GetDpiForWindow(_hWnd);
        return dpi / 96.0;
    }

    // ── P/Invoke ─────────────────────────────────────────────────

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hWnd, nint hRgn,
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)] bool bRedraw);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);
#endif
}
