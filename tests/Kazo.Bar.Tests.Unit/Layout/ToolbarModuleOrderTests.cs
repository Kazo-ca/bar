namespace Kazo.Bar.Tests.Unit.Layout;

using Kazo.Bar.Core.Layout;
using Kazo.Bar.Core.Models;

[TestFixture]
public class ToolbarModuleOrderTests
{
    private static readonly string[] Main = ["git-tools", "core.actions"];
    private static readonly string[] System = ["work-items", "ci-cd", "core.timer"];

    [Test]
    public void NoSavedOrder_KeepsTheDefaultOrder()
    {
        Assert.That(ToolbarModuleOrder.Arrange(System, null), Is.EqualTo(System));
        Assert.That(ToolbarModuleOrder.Arrange(System, []), Is.EqualTo(System));
    }

    [Test]
    public void SavedOrder_IsApplied_UnknownIdsIgnored_NewModulesLast()
    {
        var saved = new List<string> { "core.timer", "gone-plugin", "work-items" };

        Assert.That(ToolbarModuleOrder.Arrange(System, saved), Is.EqualTo(new[] { "core.timer", "work-items", "ci-cd" }));
    }

    [Test]
    public void Move_SwapsInsideTheZone_AndKeepsOtherZones()
    {
        var saved = new List<string> { "core.actions", "git-tools" };
        var arranged = ToolbarModuleOrder.Arrange(System, saved);

        var result = ToolbarModuleOrder.Move(arranged, saved, "core.timer", -1);

        Assert.That(result, Is.EqualTo(new[] { "core.actions", "git-tools", "work-items", "core.timer", "ci-cd" }));
        Assert.That(ToolbarModuleOrder.Arrange(Main, result), Is.EqualTo(new[] { "core.actions", "git-tools" }));
        Assert.That(saved, Is.EqualTo(new[] { "core.actions", "git-tools" }), "the saved list is not changed in place");
    }

    [TestCase("work-items", -1, false)]
    [TestCase("core.timer", 1, false)]
    [TestCase("ci-cd", 1, true)]
    [TestCase("ci-cd", 0, false)]
    [TestCase("missing", 1, false)]
    public void CanMove_StaysInsideTheZone(string id, int offset, bool expected)
    {
        Assert.That(ToolbarModuleOrder.CanMove(System, id, offset), Is.EqualTo(expected));
    }

    [Test]
    public void ImpossibleMove_ReturnsTheSavedOrderUnchanged()
    {
        var saved = new List<string> { "ci-cd" };

        Assert.That(ToolbarModuleOrder.Move(System, saved, "work-items", -1), Is.EqualTo(saved));
    }

    [Test]
    public void PluginLayout_ClampsWidths()
    {
        var layout = PluginLayout.ResizableWidth(defaultWidth: 900, minWidth: 300, maxWidth: 100);

        Assert.That((layout.MinWidth, layout.MaxWidth, layout.DefaultWidth), Is.EqualTo((100, 300, 300)));
        Assert.That(layout.EffectiveWidth(null), Is.EqualTo(300));
        Assert.That(layout.EffectiveWidth(50), Is.EqualTo(100));
        Assert.That(layout.EffectiveWidth(200), Is.EqualTo(200));
        Assert.That(PluginLayout.Fixed.Resizable, Is.False);
    }
}
