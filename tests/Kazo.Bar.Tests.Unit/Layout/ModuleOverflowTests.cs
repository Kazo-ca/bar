namespace Kazo.Bar.Tests.Unit.Layout;

using Kazo.Bar.Core.Layout;

[TestFixture]
public class ModuleOverflowTests
{
    private static readonly int[] Widths = [90, 60, 80];

    [TestCase(null, 3, TestName = "Not measured yet: every module shown")]
    [TestCase(254, 3, TestName = "Exactly enough room: 90+60+80 + 3 gaps")]
    [TestCase(253, 2, TestName = "One px short: two modules and the button")]
    [TestCase(140, 1, TestName = "Room for the button and the first module")]
    [TestCase(20, 0, TestName = "No room: only the button")]
    public void VisibleCount_KeepsTheOrder_AndRoomForTheButton(int? room, int expected)
    {
        Assert.That(ModuleOverflow.VisibleCount(room, Widths, gap: 8, buttonWidth: 30), Is.EqualTo(expected));
    }

    [Test]
    public void NoModules_NothingToHide()
    {
        Assert.That(ModuleOverflow.VisibleCount(0, [], 8, 30), Is.Zero);
    }
}
