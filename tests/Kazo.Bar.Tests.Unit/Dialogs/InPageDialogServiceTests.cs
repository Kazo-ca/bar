namespace Kazo.Bar.Tests.Unit.Dialogs;

using Kazo.Bar.Core.Dialogs;
using Kazo.Bar.UI.Dialogs;
using Microsoft.AspNetCore.Components;

[TestFixture]
public class InPageDialogServiceTests
{
    private sealed class RecordingNavigationManager : NavigationManager
    {
        public RecordingNavigationManager() => Initialize("http://localhost/", "http://localhost/");

        public List<string> Visited { get; } = new();

        protected override void NavigateToCore(string uri, bool forceLoad) => Visited.Add(uri);
    }

    private sealed class FakePanel;

    private sealed class OtherPanel;

    private RecordingNavigationManager _navigation = null!;
    private InPageDialogService _dialogs = null!;

    [SetUp]
    public void SetUp()
    {
        _navigation = new RecordingNavigationManager();
        _dialogs = new InPageDialogService(_navigation);
    }

    [Test]
    public async Task Open_ShowsTheComponentWithItsParametersAndTitle()
    {
        var changes = 0;
        _dialogs.Changed += () => changes++;

        var dialog = await _dialogs.OpenAsync<FakePanel>(
            new Dictionary<string, object?> { ["RepositoryPath"] = @"C:\repo" },
            new DialogOptions { Title = "Commit" });

        Assert.That(_dialogs.OpensWindows, Is.False);
        Assert.That(_dialogs.Dialogs, Has.Count.EqualTo(1));
        var shown = _dialogs.Dialogs[0];
        Assert.That(shown.ComponentType, Is.EqualTo(typeof(FakePanel)));
        Assert.That(shown.Parameters["RepositoryPath"], Is.EqualTo(@"C:\repo"));
        Assert.That(shown.Context.Title, Is.EqualTo("Commit"));
        Assert.That(shown.Context.IsWindow, Is.False);
        Assert.That(dialog.Key, Is.EqualTo(typeof(FakePanel).FullName));
        Assert.That(_dialogs.IsOpen(dialog.Key), Is.True);
        Assert.That(changes, Is.EqualTo(1));
    }

    [Test]
    public async Task OpenTwice_SameKey_KeepsOneDialogAndBringsItToTheFront()
    {
        var first = await _dialogs.OpenAsync<FakePanel>();
        await _dialogs.OpenAsync<OtherPanel>();

        var again = await _dialogs.OpenAsync<FakePanel>();

        Assert.That(again, Is.SameAs(first));
        Assert.That(_dialogs.Dialogs.Select(d => d.ComponentType), Is.EqualTo(new[] { typeof(OtherPanel), typeof(FakePanel) }));
    }

    [Test]
    public async Task DistinctKeys_OpenOneDialogEach()
    {
        await _dialogs.OpenAsync<FakePanel>(options: new DialogOptions { Key = "commit:a" });
        await _dialogs.OpenAsync<FakePanel>(options: new DialogOptions { Key = "commit:b" });

        Assert.That(_dialogs.Dialogs, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task CloseFromTheDialog_RemovesItAndGivesTheDataToTheOpener()
    {
        var dialog = await _dialogs.OpenAsync<FakePanel>();
        var context = _dialogs.Dialogs[0].Context;

        await context.CloseAsync("abc123");
        var result = await dialog.Result;

        Assert.That(result.Cancelled, Is.False);
        Assert.That(result.GetData<string>(), Is.EqualTo("abc123"));
        Assert.That(_dialogs.Dialogs, Is.Empty);
        Assert.That(_dialogs.IsOpen(dialog.Key), Is.False);
    }

    [Test]
    public async Task CloseByKey_CancelsAndASecondCloseIsIgnored()
    {
        var dialog = await _dialogs.OpenAsync<FakePanel>();

        await _dialogs.CloseAsync(dialog.Key);
        await dialog.CloseAsync(DialogResult.Ok("late"));

        var result = await dialog.Result;
        Assert.That(result.Cancelled, Is.True);
        Assert.That(result.GetData<string>(), Is.Null);
    }

    [Test]
    public async Task FallbackRoute_NavigatesInsteadOfShowingAnOverlay()
    {
        var dialog = await _dialogs.OpenAsync<FakePanel>(options: new DialogOptions { FallbackRoute = "/settings" });

        Assert.That(_navigation.Visited, Is.EqualTo(new[] { "/settings" }));
        Assert.That(_dialogs.Dialogs, Is.Empty);
        Assert.That(dialog.Result.IsCompleted, Is.True);
    }

    [Test]
    public async Task SetTitle_UpdatesTheOverlayHeader()
    {
        await _dialogs.OpenAsync<FakePanel>(options: new DialogOptions { Title = "Git" });
        var changes = 0;
        _dialogs.Changed += () => changes++;

        _dialogs.Dialogs[0].Context.SetTitle("Git — main");

        Assert.That(_dialogs.Dialogs[0].Context.Title, Is.EqualTo("Git — main"));
        Assert.That(changes, Is.EqualTo(1));
    }
}
