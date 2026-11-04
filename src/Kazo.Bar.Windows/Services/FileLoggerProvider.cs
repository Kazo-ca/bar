using Microsoft.Extensions.Logging;

namespace Kazo.Bar.Windows.Services;

/// <summary>Writes warnings and errors (Blazor renderer, WebView) to the host log file.</summary>
public sealed class FileLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName);

    public void Dispose() { }

    private sealed class FileLogger(string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel)) return;
            Program.Log($"{logLevel} {category}: {formatter(state, exception)}{(exception != null ? Environment.NewLine + exception : "")}");
        }
    }
}
