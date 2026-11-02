using System.IO;
using System.Text.Json;
using System.Windows;

namespace DevToolbar.Feasibility;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var options = Options.Parse(args);
        var output = Path.GetFullPath(options.LogDirectory);
        Directory.CreateDirectory(output);
        var path = Path.Combine(output, $"{options.Mode}-{options.Renderer}-{Environment.ProcessId}.jsonl");
        void Log(string name, object? data) => File.AppendAllText(path,
            JsonSerializer.Serialize(new { utc = DateTimeOffset.UtcNow, pid = Environment.ProcessId,
                mode = options.Mode, renderer = options.Renderer, name, data }) + Environment.NewLine);
        try
        {
            Log("environment", new { options, snapshot = EnvironmentSnapshot.Capture(),
                clr = Environment.Version.ToString(), os = Environment.OSVersion.ToString() });
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (_, e) => Log("unhandled-error", e.Exception.ToString());
            Window window = options.Mode == "witness"
                ? new WitnessWindow(Log)
                : new ProbeWindow(options, Log, options.Mode == "split" ? "bar" : "single");
            app.MainWindow = window;
            app.Run(window);
            Log("exit", null);
            return 0;
        }
        catch (Exception ex)
        {
            Log("fatal", ex.ToString());
            MessageBox.Show(ex.Message, "DevToolbar feasibility — error");
            return 1;
        }
    }
}

internal sealed record Options(string Mode, string Renderer, string LogDirectory, double Left, double Top, bool NoShadow)
{
    public static Options Parse(string[] args)
    {
        string Read(string key, string fallback)
        {
            var index = Array.IndexOf(args, key);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : fallback;
        }
        var mode = Read("--mode", "single");
        var renderer = Read("--renderer", "composition");
        if (!new[] { "single", "split", "native", "opaque", "witness" }.Contains(mode))
            throw new ArgumentException("--mode: single | split | native | opaque | witness");
        if (!new[] { "composition", "hwnd" }.Contains(renderer))
            throw new ArgumentException("--renderer: composition | hwnd");
        return new(mode, renderer, Read("--log-dir", Path.Combine(Path.GetTempPath(), "DevToolbar-feasibility")),
            double.Parse(Read("--left", "120"), System.Globalization.CultureInfo.InvariantCulture),
            double.Parse(Read("--top", "180"), System.Globalization.CultureInfo.InvariantCulture),
            args.Contains("--no-shadow"));
    }
}
