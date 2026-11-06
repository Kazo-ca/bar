namespace Kazo.Bar.Tests.Unit.Layout;

using Kazo.Bar.Core.Layout;

[TestFixture]
public class ToolbarModuleLayoutTests
{
    private const string Pinned = "core.timer";
    private static readonly string[] MainDefaults = { "git-tools", "core.actions" };
    private static readonly string[] SystemDefaults = { "work-items", "ci", Pinned };

    private static ToolbarArrangement Default() =>
        ToolbarModuleLayout.Arrange(MainDefaults, SystemDefaults, null, null, Pinned);

    [Test]
    public void Arrange_WithNothingSaved_KeepsDefaultZonesAndOrder()
    {
        var arrangement = Default();

        Assert.That(arrangement.Main, Is.EqualTo(MainDefaults));
        Assert.That(arrangement.System, Is.EqualTo(SystemDefaults));
    }

    [Test]
    public void Arrange_PutsModulesInTheirSavedZone_AndIgnoresUnknownZones()
    {
        var zones = new Dictionary<string, string> { ["git-tools"] = "system", ["ci"] = "nowhere" };
        var order = new[] { "work-items", "git-tools", "ci" };

        var arrangement = ToolbarModuleLayout.Arrange(MainDefaults, SystemDefaults, order, zones, Pinned);

        Assert.That(arrangement.Main, Is.EqualTo(new[] { "core.actions" }));
        Assert.That(arrangement.System, Is.EqualTo(new[] { "work-items", "git-tools", "ci", Pinned }));
    }

    [Test]
    public void Arrange_KeepsThePinnedModuleLast_InItsDefaultZone()
    {
        var zones = new Dictionary<string, string> { [Pinned] = "main" };
        var order = new[] { Pinned, "work-items" };

        var arrangement = ToolbarModuleLayout.Arrange(MainDefaults, SystemDefaults, order, zones, Pinned);

        Assert.That(arrangement.Main, Does.Not.Contain(Pinned));
        Assert.That(arrangement.System[^1], Is.EqualTo(Pinned));
    }

    [Test]
    public void DropOn_InSameZone_WorksLikeASortableList()
    {
        var right = ToolbarModuleLayout.DropOn(Default(), "work-items", "ci", Pinned);
        Assert.That(right.System, Is.EqualTo(new[] { "ci", "work-items", Pinned }));

        var left = ToolbarModuleLayout.DropOn(Default(), "core.actions", "git-tools", Pinned);
        Assert.That(left.Main, Is.EqualTo(new[] { "core.actions", "git-tools" }));
    }

    [Test]
    public void DropOn_AnotherZone_GoesBeforeTheTarget()
    {
        var arrangement = ToolbarModuleLayout.DropOn(Default(), "git-tools", "ci", Pinned);

        Assert.That(arrangement.Main, Is.EqualTo(new[] { "core.actions" }));
        Assert.That(arrangement.System, Is.EqualTo(new[] { "work-items", "git-tools", "ci", Pinned }));
    }

    [Test]
    public void DropOn_ThePinnedModule_GoesJustBeforeIt()
    {
        var arrangement = ToolbarModuleLayout.DropOn(Default(), "core.actions", Pinned, Pinned);

        Assert.That(arrangement.System, Is.EqualTo(new[] { "work-items", "ci", "core.actions", Pinned }));
    }

    [Test]
    public void ThePinnedModule_CannotBeMoved()
    {
        var original = Default();

        Assert.That(ToolbarModuleLayout.CanDrag(original, Pinned, Pinned), Is.False);
        Assert.That(ToolbarModuleLayout.DropOn(original, Pinned, "git-tools", Pinned), Is.SameAs(original));
        Assert.That(ToolbarModuleLayout.DropAtEnd(original, Pinned, "main", Pinned), Is.SameAs(original));
    }

    [Test]
    public void DropAtEnd_AppendsToTheZone_AndAllowsEmptyingAZone()
    {
        var once = ToolbarModuleLayout.DropAtEnd(Default(), "git-tools", "system", Pinned);
        var twice = ToolbarModuleLayout.DropAtEnd(once, "core.actions", "system", Pinned);

        Assert.That(twice.Main, Is.Empty);
        Assert.That(twice.System, Is.EqualTo(new[] { "work-items", "ci", "git-tools", "core.actions", Pinned }));

        var back = ToolbarModuleLayout.DropAtEnd(twice, "ci", "main", Pinned);
        Assert.That(back.Main, Is.EqualTo(new[] { "ci" }));
    }

    [Test]
    public void UnknownModulesOrZones_LeaveTheArrangementUnchanged()
    {
        var original = Default();

        Assert.That(ToolbarModuleLayout.DropOn(original, "nope", "ci", Pinned), Is.SameAs(original));
        Assert.That(ToolbarModuleLayout.DropOn(original, "ci", "nope", Pinned), Is.SameAs(original));
        Assert.That(ToolbarModuleLayout.MoveTo(original, "ci", "elsewhere", 0, Pinned), Is.SameAs(original));
    }

    [Test]
    public void OrderAndZonesOf_RoundTripThroughArrange_AndKeepHiddenModules()
    {
        var moved = ToolbarModuleLayout.DropOn(Default(), "git-tools", "ci", Pinned);
        var previousOrder = new[] { "disabled-plugin", "core.actions" };
        var previousZones = new Dictionary<string, string> { ["disabled-plugin"] = "system" };

        var order = ToolbarModuleLayout.OrderOf(moved, previousOrder);
        var zones = ToolbarModuleLayout.ZonesOf(moved, previousZones);

        Assert.That(order[0], Is.EqualTo("disabled-plugin"));
        Assert.That(zones["disabled-plugin"], Is.EqualTo("system"));
        Assert.That(zones["git-tools"], Is.EqualTo("system"));
        Assert.That(previousZones, Has.Count.EqualTo(1), "inputs are not mutated");
        var again = ToolbarModuleLayout.Arrange(MainDefaults, SystemDefaults, order, zones, Pinned);
        Assert.That(again.SequenceEqual(moved), Is.True);
    }
}
