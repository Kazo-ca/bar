namespace Kazo.Bar.Updates;

/// <summary>
/// Background check used by the toolbar: once after <see cref="UpdateConstants.FirstCheckDelay"/>, then every
/// <see cref="UpdateConstants.CheckInterval"/>. Silent by design: no internet, GitHub down or rate limited
/// only writes a log line and the next tick tries again. It never throws into the application.
/// </summary>
public sealed class PeriodicUpdateCheck : IAsyncDisposable
{
    private readonly Func<CancellationToken, Task<AvailableUpdate?>> _check;
    private readonly Action<string> _log;
    private readonly TimeProvider _time;
    private readonly TimeSpan _firstDelay;
    private readonly TimeSpan _interval;
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private string? _announcedTag;

    public PeriodicUpdateCheck(
        Func<CancellationToken, Task<AvailableUpdate?>> check,
        Action<string> log,
        TimeProvider? time = null,
        TimeSpan? firstDelay = null,
        TimeSpan? interval = null)
    {
        _check = check;
        _log = log;
        _time = time ?? TimeProvider.System;
        _firstDelay = firstDelay ?? UpdateConstants.FirstCheckDelay;
        _interval = interval ?? UpdateConstants.CheckInterval;
    }

    /// <summary>Raised once per new release found (not again for the same tag). Raised on a pool thread.</summary>
    public event Action<AvailableUpdate>? UpdateAvailable;

    /// <summary>The last update found, or null.</summary>
    public AvailableUpdate? Latest { get; private set; }

    public void Start()
    {
        _loop ??= Task.Run(() => RunAsync(_stop.Token));
    }

    private async Task RunAsync(CancellationToken stop)
    {
        try
        {
            await Task.Delay(_firstDelay, _time, stop).ConfigureAwait(false);
            await CheckOnceAsync(stop).ConfigureAwait(false);
            using var timer = new PeriodicTimer(_interval, _time);
            while (await timer.WaitForNextTickAsync(stop).ConfigureAwait(false))
                await CheckOnceAsync(stop).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }
    }

    /// <summary>One check; every failure is logged and swallowed.</summary>
    public async Task CheckOnceAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var update = await _check(cancellationToken).ConfigureAwait(false);
            if (update == null) return;
            Latest = update;
            if (string.Equals(_announcedTag, update.Release.Tag, StringComparison.OrdinalIgnoreCase)) return;
            _announcedTag = update.Release.Tag;
            _log($"Update available: {update.Release.Tag} ({update.Package.Name}).");
            UpdateAvailable?.Invoke(update);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Offline, DNS, timeout, GitHub 403 rate limit, bad JSON, a faulty event handler...
            _log($"Update check skipped: {ex.GetType().Name}: {ex.Message}");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        if (_loop != null)
        {
            try { await _loop.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
        }
        _stop.Dispose();
    }
}
