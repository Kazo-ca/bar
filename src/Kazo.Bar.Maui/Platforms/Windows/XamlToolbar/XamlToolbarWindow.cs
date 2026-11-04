using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using WinRT.Interop;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Controls;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.Themes;
using Kazo.Bar.Maui.Platforms.Windows.XamlToolbar.ViewModels;

namespace Kazo.Bar.Maui.Platforms.Windows.XamlToolbar;

/// <summary>
/// Native WinUI3 toolbar window. Pure XAML rendering — no Blazor/WebView.
/// Uses SetWindowRgn for per-pixel transparency and click-through.
/// Flyouts escape the region for dropdowns.
/// </summary>
public class XamlToolbarWindow : Microsoft.UI.Xaml.Window
{
    private readonly ToolbarViewModel _viewModel;
    private readonly CancellationTokenSource _cts = new();

    // Window dimensions (CSS/logical pixels — scaled by DPI at runtime)
    private const int ToolbarWidth = 960;
    private const int ToolbarHeight = 56;
    private const int ShellPadding = 6;
    private const int TotalHeight = ToolbarHeight + ShellPadding * 2; // 68
    private const int CornerRadius = 34; // half of total height for perfect pill

    private nint _hWnd;
    private AppWindow? _appWindow;

    // For window dragging
    private bool _isDragging;
    private global::Windows.Graphics.PointInt32 _dragStartWindowPos;
    private global::Windows.Foundation.Point _dragStartCursorPos;

    public XamlToolbarWindow()
    {
        _viewModel = new ToolbarViewModel();
        Title = "Kazo Bar XAML";

        // Build the UI
        Content = BuildToolbarShell();

        // Configure window after activation
        Activated += OnFirstActivated;
    }

    private void OnFirstActivated(object sender, WindowActivatedEventArgs args)
    {
        Activated -= OnFirstActivated;

        _hWnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hWnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        if (_appWindow == null) return;

        // 1. Borderless, always on top
        if (_appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
            presenter.IsAlwaysOnTop = true;
            presenter.IsResizable = false;
            presenter.IsMinimizable = false;
        }

        // 2. No system backdrop
        SystemBackdrop = null;

        // 3. Size and position — DPI-aware
        double scale = GetDpiScale();
        int physW = (int)(ToolbarWidth * scale);
        int physH = (int)(500 * scale); // tall enough for flyout anchor
        int physVisibleH = (int)(TotalHeight * scale);
        int physRadius = (int)(CornerRadius * scale);

        _appWindow.Resize(new global::Windows.Graphics.SizeInt32(physW, physH));

        // Center horizontally at top of screen
        var display = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
        int x = (display.WorkArea.Width - physW) / 2;
        _appWindow.Move(new global::Windows.Graphics.PointInt32(x, 0));

        // 4. Clip to pill shape
        nint hRgn = CreateRoundRectRgn(0, 0, physW + 1, physVisibleH + 1, physRadius, physRadius);
        SetWindowRgn(_hWnd, hRgn, true);

        // 5. Start timer loop
        _ = _viewModel.StartTimerLoopAsync(_cts.Token);
    }

    /// <summary>
    /// Build the complete toolbar visual tree.
    /// </summary>
    private UIElement BuildToolbarShell()
    {
        // ── Create controls ──
        var projectPill = new ProjectPill();
        projectPill.ApplyViewModel(_viewModel);

        var gitBranchPill = new GitBranchPill();
        gitBranchPill.ApplyViewModel(_viewModel);

        var actionBar = new ActionBar();
        actionBar.ApplyViewModel(_viewModel);

        var systemPill = new SystemPill();
        systemPill.ApplyViewModel(_viewModel);

        // ── Assemble in horizontal layout ──
        // Using Grid with columns for precise control
        var layout = new Microsoft.UI.Xaml.Controls.Grid
        {
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Center,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
            ColumnDefinitions =
            {
                new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto },   // Project
                new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto },   // Git
                new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = new Microsoft.UI.Xaml.GridLength(1, Microsoft.UI.Xaml.GridUnitType.Star) }, // Actions (fill)
                new Microsoft.UI.Xaml.Controls.ColumnDefinition { Width = Microsoft.UI.Xaml.GridLength.Auto },   // System
            }
        };

        Microsoft.UI.Xaml.Controls.Grid.SetColumn(projectPill, 0);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(gitBranchPill, 1);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(actionBar, 2);
        Microsoft.UI.Xaml.Controls.Grid.SetColumn(systemPill, 3);

        layout.Children.Add(projectPill);
        layout.Children.Add(gitBranchPill);
        layout.Children.Add(actionBar);
        layout.Children.Add(systemPill);

        // ── Outer shell (glass capsule) ──
        var shell = new Microsoft.UI.Xaml.Controls.Border
        {
            CornerRadius = new Microsoft.UI.Xaml.CornerRadius(CornerRadius),
            Height = TotalHeight,
            Padding = new Microsoft.UI.Xaml.Thickness(ShellPadding),
            BorderThickness = new Microsoft.UI.Xaml.Thickness(1),
            Child = layout,
            VerticalAlignment = Microsoft.UI.Xaml.VerticalAlignment.Top,
            HorizontalAlignment = Microsoft.UI.Xaml.HorizontalAlignment.Stretch,
        };

        // Apply shadow
        shell.Translation = new System.Numerics.Vector3(0, 0, 32);
        shell.Shadow = new ThemeShadow();

        // Apply theme
        ApplyShellTheme(shell, _viewModel.Theme);
        _viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.Theme))
                ApplyShellTheme(shell, _viewModel.Theme);
        };

        // ── Drag support: double-click to cycle theme ──
        shell.DoubleTapped += (_, _) => _viewModel.CycleThemeCommand.Execute(null);

        // ── Drag to move window ──
        shell.PointerPressed += OnShellPointerPressed;
        shell.PointerMoved += OnShellPointerMoved;
        shell.PointerReleased += OnShellPointerReleased;

        // ── Root container (fills the whole window) ──
        var root = new Microsoft.UI.Xaml.Controls.Grid
        {
            Background = null, // transparent (clipped by region)
            Padding = new Microsoft.UI.Xaml.Thickness(0),
        };
        root.Children.Add(shell);

        return root;
    }

    private void ApplyShellTheme(Microsoft.UI.Xaml.Controls.Border shell, ThemeColors theme)
    {
        shell.Background = theme.ShellBackground;
        shell.BorderBrush = theme.ShellBorder;
    }

    // ── Window drag ──────────────────────────────────────────────────

    private void OnShellPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement el)
        {
            _isDragging = true;
            _dragStartCursorPos = e.GetCurrentPoint(null).Position;
            if (_appWindow != null)
                _dragStartWindowPos = _appWindow.Position;
            el.CapturePointer(e.Pointer);
        }
    }

    private void OnShellPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDragging || _appWindow == null) return;
        var current = e.GetCurrentPoint(null).Position;
        double scale = GetDpiScale();

        int dx = (int)((current.X - _dragStartCursorPos.X) * scale);
        int dy = (int)((current.Y - _dragStartCursorPos.Y) * scale);

        _appWindow.Move(new global::Windows.Graphics.PointInt32(
            _dragStartWindowPos.X + dx,
            _dragStartWindowPos.Y + dy));
    }

    private void OnShellPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        _isDragging = false;
        if (sender is UIElement el)
            el.ReleasePointerCapture(e.Pointer);
    }

    private double GetDpiScale()
    {
        uint dpi = GetDpiForWindow(_hWnd);
        return dpi / 96.0;
    }

    // ── P/Invoke ─────────────────────────────────────────────────────

    [DllImport("gdi32.dll")]
    private static extern nint CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(nint hWnd, nint hRgn, [MarshalAs(UnmanagedType.Bool)] bool bRedraw);

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);
}
