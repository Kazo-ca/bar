namespace Kazo.Bar.Tests.Unit.Ui;

using Kazo.Bar.Core.Interfaces;
using Kazo.Bar.Core.Models;
using Kazo.Bar.UI.Services;

[TestFixture]
public class ToolbarLayoutControllerTests
{
    private sealed class InMemoryGlobalSettings : IGlobalSettingsService
    {
        public GlobalSettings Current { get; } = new();
        public int Saves { get; private set; }
        public event Action<GlobalSettings>? OnSettingsChanged;
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;

        public Task UpdateAsync(Action<GlobalSettings> updateAction)
        {
            updateAction(Current);
            Saves++;
            OnSettingsChanged?.Invoke(Current);
            return Task.CompletedTask;
        }
    }

    private static readonly PluginLayout Resizable = PluginLayout.ResizableWidth(300, 170, 560);

    [Test]
    public async Task Width_IsClampedSaved_AndReset()
    {
        var settings = new InMemoryGlobalSettings();
        var controller = new ToolbarLayoutController(settings);
        var changed = 0;
        controller.Changed += () => changed++;

        await controller.SetWidthAsync("git-tools", Resizable, 9999);
        Assert.That(controller.SavedWidth("git-tools"), Is.EqualTo(560));
        Assert.That(settings.Current.ToolbarLayout.ModuleWidths["git-tools"], Is.EqualTo(560));

        await controller.ResetWidthAsync("git-tools");
        Assert.That(controller.SavedWidth("git-tools"), Is.Null);
        Assert.That(changed, Is.EqualTo(2));
    }

    [Test]
    public async Task FixedModules_AreNeverGivenAWidth()
    {
        var settings = new InMemoryGlobalSettings();
        var controller = new ToolbarLayoutController(settings);

        await controller.SetWidthAsync("core.timer", PluginLayout.Fixed, 200);

        Assert.That(controller.HasCustomWidths, Is.False);
        Assert.That(settings.Saves, Is.Zero);
    }

    [Test]
    public async Task Move_ThenResetOrder_KeepsWidths()
    {
        var controller = new ToolbarLayoutController(new InMemoryGlobalSettings());
        await controller.SetWidthAsync("git-tools", Resizable, 400);
        var zone = controller.Arrange(["git-tools", "core.actions"]);

        await controller.MoveAsync(zone, "git-tools", 1);
        Assert.That(controller.Arrange(["git-tools", "core.actions"]), Is.EqualTo(new[] { "core.actions", "git-tools" }));

        await controller.ResetOrderAsync();
        Assert.That(controller.HasCustomOrder, Is.False);
        Assert.That(controller.SavedWidth("git-tools"), Is.EqualTo(400));
    }

    private static readonly string[] Main = { "git-tools", "core.actions" };
    private static readonly string[] System = { "work-items", "core.timer" };

    [Test]
    public async Task Reorganize_DropsChangeADraft_ThatIsSavedOnAccept()
    {
        var settings = new InMemoryGlobalSettings();
        var controller = new ToolbarLayoutController(settings);
        controller.Arrange(Main, System);

        controller.BeginReorganize();
        controller.StartDrag("git-tools");
        controller.DropOn("work-items");

        var draft = controller.Arrange(Main, System);
        Assert.That(draft.System, Is.EqualTo(new[] { "git-tools", "work-items", "core.timer" }));
        Assert.That(controller.HasPendingChanges, Is.True);
        Assert.That(settings.Saves, Is.Zero, "nothing is saved before ✅");

        await controller.CommitReorganizeAsync();

        Assert.That(controller.IsReorganizing, Is.False);
        Assert.That(settings.Saves, Is.EqualTo(1));
        Assert.That(settings.Current.ToolbarLayout.ModuleZones["git-tools"], Is.EqualTo("system"));
        Assert.That(controller.Arrange(Main, System).Main, Is.EqualTo(new[] { "core.actions" }));
    }

    [Test]
    public void Reorganize_Cancel_DropsTheDraft()
    {
        var settings = new InMemoryGlobalSettings();
        var controller = new ToolbarLayoutController(settings);
        controller.Arrange(Main, System);

        controller.BeginReorganize();
        controller.StartDrag("core.actions");
        controller.DropAtEnd("system");
        Assert.That(controller.Arrange(Main, System).Main, Is.EqualTo(new[] { "git-tools" }));

        controller.CancelReorganize();

        Assert.That(controller.IsReorganizing, Is.False);
        Assert.That(controller.Arrange(Main, System).Main, Is.EqualTo(Main));
        Assert.That(settings.Saves, Is.Zero);
    }

    [Test]
    public async Task Reorganize_AcceptWithoutChanges_SavesNothing()
    {
        var settings = new InMemoryGlobalSettings();
        var controller = new ToolbarLayoutController(settings);
        controller.Arrange(Main, System);

        controller.BeginReorganize();
        await controller.CommitReorganizeAsync();

        Assert.That(settings.Saves, Is.Zero);
        Assert.That(controller.IsReorganizing, Is.False);
    }

    [Test]
    public void TheTimer_IsPinned_CannotBeDraggedOrMovedPast()
    {
        var controller = new ToolbarLayoutController(new InMemoryGlobalSettings());
        var arrangement = controller.Arrange(Main, System);
        controller.BeginReorganize();

        Assert.That(controller.CanDrag("core.timer"), Is.False);
        controller.StartDrag("core.timer");
        Assert.That(controller.DraggedModuleId, Is.Null);

        Assert.That(controller.CanMove(arrangement.System, "core.timer", -1), Is.False);
        Assert.That(controller.CanMove(arrangement.System, "work-items", 1), Is.False);
        Assert.That(controller.CanMove(arrangement.Main, "git-tools", 1), Is.True);
    }

    [Test]
    public async Task ResetOrder_AlsoResetsZones()
    {
        var settings = new InMemoryGlobalSettings();
        var controller = new ToolbarLayoutController(settings);
        controller.Arrange(Main, System);
        controller.BeginReorganize();
        controller.StartDrag("git-tools");
        controller.DropAtEnd("system");
        await controller.CommitReorganizeAsync();
        Assert.That(controller.HasCustomOrder, Is.True);

        await controller.ResetOrderAsync();

        Assert.That(controller.HasCustomOrder, Is.False);
        Assert.That(controller.Arrange(Main, System).Main, Is.EqualTo(Main));
    }
}
