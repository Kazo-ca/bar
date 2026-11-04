namespace Kazo.Bar.Tests.Unit.Processes;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;

/// <summary>US4.2: FocusWindowByTitle is built on FindWindowsByTitle + FocusWindow.</summary>
[TestFixture]
public class ProcessServiceContractTests
{
    [Test]
    public void FocusWindowByTitle_FocusesTheFirstMatch()
    {
        var service = new FakeWindows(new WindowMatch(1, "A - Edge", "msedge"), new WindowMatch(2, "B - Edge", "msedge"));

        Assert.That(((IProcessService)service).FocusWindowByTitle("Edge"), Is.True);
        Assert.That(service.Focused?.Handle, Is.EqualTo((nint)1));
    }

    [Test]
    public void FocusWindowByTitle_ReturnsFalse_WhenNothingMatches()
    {
        var service = new FakeWindows();

        Assert.That(((IProcessService)service).FocusWindowByTitle("Edge"), Is.False);
        Assert.That(service.Focused, Is.Null);
    }

    private sealed class FakeWindows(params WindowMatch[] windows) : IProcessService
    {
        public WindowMatch? Focused { get; private set; }
        public Task<int> StartProcessAsync(string path, string arguments = "") => Task.FromResult(0);
        public IReadOnlyList<WindowMatch> FindWindowsByTitle(string titleRegex) => windows;
        public bool FocusWindow(WindowMatch window) { Focused = window; return true; }
    }
}
