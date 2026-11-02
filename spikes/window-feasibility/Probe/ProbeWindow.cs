using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace DevToolbar.Feasibility;

internal sealed class ProbeWindow : Window
{
    private readonly Options options;
    private readonly Action<string, object?> log;
    private readonly string role;
    private readonly ProbeWindow? bar;
    private WebView2CompositionControl? composition;
    private WebView2? hwndView;
    private CoreWebView2? core;
    private ProbeWindow? popup;
    private bool menuOpen;
    private bool closing;
    private int menuGeneration;

    public ProbeWindow(Options options, Action<string, object?> log, string role, ProbeWindow? bar = null)
    {
        this.options = options;
        this.log = log;
        this.role = role;
        this.bar = bar;
        Title = $"DevToolbar feasibility — {options.Mode}/{options.Renderer}/{role}{(options.NoShadow ? " — no-shadow" : "")}";
        Width = role == "popup" ? 320 : 960;
        Height = role == "popup" ? 260 : role == "bar" ? 66 : 500;
        Left = options.Left;
        Top = options.Top;
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        AllowsTransparency = true;
        Background = options.Mode == "opaque" ? Brushes.White : Brushes.Transparent;
        Topmost = true;
        ShowActivated = false;
        ShowInTaskbar = role != "popup";
        if (bar != null) Owner = bar;
        SourceInitialized += (_, _) => Write("native-created");
        Activated += (_, _) => Write("activated");
        Deactivated += (_, _) =>
        {
            Write("deactivated");
            // Wait for normal activation to settle; no polling or forced focus return.
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var root = bar ?? this;
                if (!NativeWindow.ForegroundBelongsToThisProcess()) root.CloseMenu(false);
            }));
        };
        LocationChanged += (_, _) =>
        {
            Write("location");
            if (role == "bar") CloseMenu(false); // declared behavior: moving closes split popup
        };
        DpiChanged += (_, _) =>
        {
            Write("dpi-changed");
            if (role == "bar") CloseMenu(false);
        };
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.F8)
            {
                (bar ?? this).SampleRouting();
                e.Handled = true;
            }
        };
        Closed += (_, _) =>
        {
            closing = true;
            popup?.Close();
            composition?.Dispose();
            hwndView?.Dispose();
            Write("closed");
        };
        Loaded += async (_, _) =>
        {
            try
            {
                if (options.Mode == "native") BuildNativeControl();
                else await InitializeWeb();
            }
            catch (Exception ex)
            {
                log("initialization-failed", new { role, error = ex.ToString() });
                Content = new TextBlock { Text = "Initialization failed; see JSONL. Alt+F4 to close.", Background = Brushes.DarkRed, Foreground = Brushes.White };
            }
        };
    }

    private void Write(string name, object? extra = null) => log(name, new { role, window = NativeWindow.Describe(this), extra });

    private async Task InitializeWeb()
    {
        var profile = Path.Combine(Path.GetTempPath(), "DevToolbar-feasibility", $"wv-{Environment.ProcessId}");
        var environment = await CoreWebView2Environment.CreateAsync(null, profile);
        if (closing) return;
        if (options.Renderer == "composition")
        {
            composition = new WebView2CompositionControl { DefaultBackgroundColor = System.Drawing.Color.Transparent };
            Content = composition;
            await composition.EnsureCoreWebView2Async(environment);
            if (closing) return;
            core = composition.CoreWebView2;
        }
        else
        {
            hwndView = new WebView2 { DefaultBackgroundColor = System.Drawing.Color.Transparent };
            Content = hwndView;
            await hwndView.EnsureCoreWebView2Async(environment);
            if (closing) return;
            core = hwndView.CoreWebView2;
        }
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.AreDefaultContextMenusEnabled = true;
        core.WebMessageReceived += OnMessage;
        core.NavigationStarting += (_, e) => Write("navigation-start", new {
            uri = e.Uri.StartsWith("data:", StringComparison.Ordinal) ? "data:[local HTML]" : e.Uri, e.NavigationId });
        core.NewWindowRequested += (_, e) => e.Handled = true;
        core.NavigationCompleted += async (_, e) =>
        {
            if (closing) return;
            Write("navigation", new { e.IsSuccess, e.WebErrorStatus, version = environment.BrowserVersionString });
            if (role == "popup")
            {
                Activate();
                composition?.Focus();
                hwndView?.Focus();
                await Script("window.spike.setMenu(true)");
            }
        };
        var html = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "index.html"))
            .Replace("__ROLE__", role).Replace("__OPAQUE__", options.Mode == "opaque" ? "true" : "false")
            .Replace("__NO_SHADOW__", options.NoShadow ? "true" : "false");
        if (composition != null) composition.NavigateToString(html);
        else hwndView!.NavigateToString(html);
        Write("web-created", new { version = environment.BrowserVersionString, profile });
    }

    private void OnMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        using var document = JsonDocument.Parse(e.TryGetWebMessageAsString());
        var message = document.RootElement;
        var action = message.GetProperty("action").GetString();
        log("web-message", new { role, message = message.Clone() });
        switch (action)
        {
            case "open-menu": _ = ToggleMenu(); break;
            case "close-menu": (bar ?? this).CloseMenu(false); break;
            case "escape": (bar ?? this).CloseMenu(true); break;
            case "quit": (bar ?? this).Close(); break;
            case "drag":
                CloseMenu(false);
                if (Mouse.LeftButton == MouseButtonState.Pressed)
                {
                    try { DragMove(); }
                    catch (InvalidOperationException ex) { Write("drag-not-started", ex.Message); }
                }
                else Write("drag-not-started", "button already released");
                break;
        }
    }

    private async Task ToggleMenu()
    {
        if (menuOpen) { CloseMenu(false); return; }
        menuOpen = true;
        var generation = ++menuGeneration;
        Write("menu-open");
        await Script("window.spike.setMenu(true)");
        if (!menuOpen || closing || generation != menuGeneration) return;
        if (role != "bar") return;
        popup = new ProbeWindow(options, log, "popup", this);
        popup.Closed += (_, _) => { popup = null; menuOpen = false; _ = Script("window.spike.setMenu(false)"); };
        popup.Show();
        NativeWindow.PlacePopup(this, popup);
        Write("popup-placed", NativeWindow.Describe(popup));
    }

    private void CloseMenu(bool restoreTrigger)
    {
        if (!menuOpen || closing) return;
        menuOpen = false;
        menuGeneration++;
        popup?.Close();
        popup = null;
        Write("menu-close", new { restoreTrigger });
        if (restoreTrigger)
        {
            Activate();
            composition?.Focus();
            hwndView?.Focus();
        }
        _ = Script(restoreTrigger ? "window.spike.setMenu(false, true)" : "window.spike.setMenu(false)");
    }

    private async Task Script(string script)
    {
        if (core == null || closing) return;
        try { await core.ExecuteScriptAsync(script); }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or System.Runtime.InteropServices.COMException)
        { log("script-failed", new { role, error = ex.Message }); }
    }

    private void BuildNativeControl()
    {
        // T2 sanity control: WPF owns the actual visual; no separately maintained input region.
        var canvas = new Canvas();
        Content = canvas;
        var button = new Button { Content = "T2 native alpha control — click", Width = 300, Height = 54 };
        var pill = new Border { Width = 360, Height = 54, CornerRadius = new CornerRadius(27),
            Background = Brushes.DarkSlateBlue, Child = button };
        Canvas.SetLeft(pill, 20); Canvas.SetTop(pill, 6); canvas.Children.Add(pill);
        button.Click += (_, _) => Write("native-button");
        var close = new Button { Content = "Close", Width = 80, Height = 40 };
        Canvas.SetLeft(close, 420); Canvas.SetTop(close, 12); canvas.Children.Add(close);
        close.Click += (_, _) => Close();
        Write("native-ready");
    }

    private void SampleRouting()
    {
        using var atlas = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "web", "atlas.json")));
        var points = new List<object>();
        foreach (var point in atlas.RootElement.GetProperty("points").EnumerateArray())
        {
            if (point.TryGetProperty("menuMustBeOpen", out var required) && required.GetBoolean() && !menuOpen) continue;
            Window origin = this;
            var coordinate = point.GetProperty("single");
            if (popup != null && point.TryGetProperty("popup", out var popupCoordinate))
            { origin = popup; coordinate = popupCoordinate; }
            var screen = origin.PointToScreen(new Point(coordinate.GetProperty("x").GetDouble(), coordinate.GetProperty("y").GetDouble()));
            points.Add(new { id = point.GetProperty("id").GetString(), screenX = screen.X, screenY = screen.Y,
                target = NativeWindow.TargetAt(screen) });
        }
        Write("routing-sample", new { menuOpen, noShadow = options.NoShadow,
            note = "Read-only WindowFromPoint. No input sent; not proof of click/wheel delivery.", points });
        _ = Script("window.spike.reportLayout()");
        if (popup != null) _ = popup.Script("window.spike.reportLayout()");
    }
}
