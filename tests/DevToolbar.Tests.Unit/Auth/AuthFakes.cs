namespace DevToolbar.Tests.Unit.Auth;

using System.Net;
using System.Text;

/// <summary>A clock whose delays complete at once and move the time forward by the delay.</summary>
internal sealed class InstantClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    private readonly object _gate = new();

    public List<TimeSpan> Delays { get; } = new();

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate) _now += by;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        if (dueTime != Timeout.InfiniteTimeSpan)
        {
            lock (_gate)
            {
                Delays.Add(dueTime);
                _now += dueTime;
            }
            ThreadPool.QueueUserWorkItem(_ => callback(state));
        }
        return new NoopTimer();
    }

    private sealed class NoopTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}

/// <summary>Answers each URL fragment with its responses in order; the last one repeats.</summary>
internal sealed class SequenceHttp : HttpMessageHandler
{
    private readonly List<(string Fragment, Queue<(HttpStatusCode Status, string Json)> Answers)> _rules = new();

    public List<(string Url, string? Authorization, string? Body)> Requests { get; } = new();

    public SequenceHttp On(string urlFragment, params (HttpStatusCode Status, string Json)[] answers)
    {
        _rules.Add((urlFragment, new Queue<(HttpStatusCode, string)>(answers)));
        return this;
    }

    public SequenceHttp On(string urlFragment, params string[] okAnswers) =>
        On(urlFragment, okAnswers.Select(json => (HttpStatusCode.OK, json)).ToArray());

    public HttpClient Client() => new(this);

    public int Count(string urlFragment) => Requests.Count(r => r.Url.Contains(urlFragment, StringComparison.Ordinal));

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var url = request.RequestUri!.AbsoluteUri;
        lock (Requests) Requests.Add((url, request.Headers.Authorization?.ToString(), body));

        var rule = _rules.LastOrDefault(r => url.Contains(r.Fragment, StringComparison.Ordinal));
        if (rule.Answers == null || rule.Answers.Count == 0)
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        var (status, json) = rule.Answers.Count > 1 ? rule.Answers.Dequeue() : rule.Answers.Peek();
        return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
