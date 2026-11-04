namespace Kazo.Bar.Tests.Unit.Processes;

using System.Collections.Concurrent;
using System.Diagnostics;
using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Infrastructure.Processes;
using Kazo.Bar.Tests.Unit.Infrastructure;

/// <summary>US4.3: real script runs (with <c>sh</c>, so these run on Linux/macOS only).</summary>
[TestFixture]
public class ProcessScriptServiceTests
{
    private TempDirectory _temp = null!;
    private readonly ProcessScriptService _service = new();

    [SetUp]
    public void SetUp()
    {
        Assume.That(OperatingSystem.IsWindows(), Is.False, "Uses sh scripts.");
        _temp = new TempDirectory();
    }

    [TearDown]
    public void TearDown() => _temp?.Dispose();

    private string Script(string body) => _temp.Write("my script.sh", body.Replace("\r\n", "\n"));

    [Test]
    public async Task Lines_ArriveWhileTheScriptIsStillRunning()
    {
        var path = Script("echo one\nsleep 1\necho two\n");
        var firstLine = new TaskCompletionSource<ScriptOutputLine>(TaskCreationOptions.RunContinuationsAsynchronously);

        var run = _service.ExecuteAsync("sh", path, "", line => firstLine.TrySetResult(line));
        var first = await firstLine.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.That(first, Is.EqualTo(new ScriptOutputLine("one", IsError: false)));
        Assert.That(run.IsCompleted, Is.False, "the first line must be reported before the script ends");
        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.That(result.Output, Is.EqualTo("one\ntwo\n"));
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task StandardError_IsFlagged_AndTheExitCodeIsKept()
    {
        var path = Script("echo fine\necho broken 1>&2\nexit 3\n");
        var lines = new ConcurrentQueue<ScriptOutputLine>();

        var result = await _service.ExecuteAsync("sh", path, "", lines.Enqueue);

        Assert.That(result.ExitCode, Is.EqualTo(3));
        Assert.That(result.Success, Is.False);
        Assert.That(result.Error, Is.EqualTo("broken\n"));
        Assert.That(lines, Does.Contain(new ScriptOutputLine("broken", IsError: true)));
        Assert.That(lines, Does.Contain(new ScriptOutputLine("fine", IsError: false)));
    }

    [Test]
    public async Task Arguments_AreAppendedAfterTheQuotedScriptPath()
    {
        var path = Script("echo \"$1-$2\"\n");

        var result = await _service.ExecuteAsync("sh", path, "alpha beta");

        Assert.That(result.Output, Is.EqualTo("alpha-beta\n"));
    }

    [Test]
    public async Task Cancelling_StopsTheScript()
    {
        var path = Script("echo start\nsleep 30\necho never\n");
        using var cancel = new CancellationTokenSource();
        var lines = new ConcurrentQueue<ScriptOutputLine>();
        var watch = Stopwatch.StartNew();

        var result = await _service.ExecuteAsync("sh", path, "", line =>
        {
            lines.Enqueue(line);
            cancel.Cancel();
        }, cancel.Token).WaitAsync(TimeSpan.FromSeconds(15));

        Assert.That(result.WasCancelled, Is.True);
        Assert.That(result.Success, Is.False);
        Assert.That(watch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(15)));
        Assert.That(lines.Select(l => l.Text), Is.EqualTo(new[] { "start" }));
    }

    [Test]
    public async Task MissingInterpreter_ReturnsAFailure_InsteadOfThrowing()
    {
        var lines = new List<ScriptOutputLine>();

        var result = await _service.ExecuteAsync("devtoolbar-no-such-interpreter", "x.ps1", "", lines.Add);

        Assert.That(result.ExitCode, Is.EqualTo(-1));
        Assert.That(result.Error, Does.StartWith("Failed to execute"));
        Assert.That(lines.Single().IsError, Is.True);
    }
}

[TestFixture]
public class ScriptContractTests
{
    [TestCase("build.ps1", "build.ps1")]
    [TestCase(@"C:\My Scripts\build.ps1", "\"C:\\My Scripts\\build.ps1\"")]
    [TestCase("say\"hi.ps1", "\"say\\\"hi.ps1\"")]
    public void Quote_OnlyQuotesWhenNeeded(string path, string expected)
    {
        Assert.That(ProcessScriptService.Quote(path), Is.EqualTo(expected));
    }

    [Test]
    public void StartInfo_HidesTheWindow_AndRedirectsBothStreams()
    {
        var info = ProcessScriptService.CreateStartInfo("pwsh", @"C:\a b\build.ps1", "-Configuration Release");

        Assert.That(info.Arguments, Is.EqualTo("\"C:\\a b\\build.ps1\" -Configuration Release"));
        Assert.That(info.CreateNoWindow && info.RedirectStandardOutput && info.RedirectStandardError, Is.True);
        Assert.That(info.UseShellExecute, Is.False);
    }

    [Test]
    public void Split_HandlesCrLf_AndDropsTheTrailingEmptyLine()
    {
        var lines = ScriptOutputLine.Split("a\r\nb\n", isError: true).ToList();

        Assert.That(lines, Is.EqualTo(new[] { new ScriptOutputLine("a", true), new ScriptOutputLine("b", true) }));
        Assert.That(ScriptOutputLine.Split("", false), Is.Empty);
    }

    [Test]
    public async Task ServiceWithoutStreaming_ReportsItsLinesAtTheEnd()
    {
        IScriptService service = new BufferedOnly();
        var lines = new List<ScriptOutputLine>();

        var result = await service.ExecuteAsync("pwsh", "x.ps1", "", lines.Add);

        Assert.That(result.ExitCode, Is.EqualTo(1));
        Assert.That(lines, Is.EqualTo(new[] { new ScriptOutputLine("out", false), new ScriptOutputLine("err", true) }));
    }

    private sealed class BufferedOnly : IScriptService
    {
        public Task<ScriptResult> ExecuteAsync(string interpreter, string scriptPath, string arguments = "") =>
            Task.FromResult(new ScriptResult { ExitCode = 1, Output = "out\n", Error = "err\n" });
    }
}
