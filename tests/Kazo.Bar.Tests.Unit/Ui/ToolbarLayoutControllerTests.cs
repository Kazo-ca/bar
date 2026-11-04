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
}
