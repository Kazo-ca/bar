namespace DevToolbar.Tests.Unit.Ui;

using DevToolbar.UI.Components;
using DevToolbar.UI.Services;

[TestFixture]
public class ActionOverflowTests
{
    [TestCase(4, null, 4, 0, TestName = "Not measured yet: everything visible")]
    [TestCase(4, 170, 4, 0, TestName = "Exactly four buttons fit (4x38 + 3x6)")]
    [TestCase(4, 169, 2, 2, TestName = "One px short: two buttons and the overflow")]
    [TestCase(5, 100, 1, 4, TestName = "Room for one button and the overflow")]
    [TestCase(3, 30, 0, 3, TestName = "No room at all: only the overflow")]
    [TestCase(0, 10, 0, 0, TestName = "No actions")]
    public void Fit_RoundButtons(int count, int? width, int visible, int hidden)
    {
        Assert.That(ActionOverflow.Fit(count, width), Is.EqualTo(new ActionOverflow.Layout(visible, hidden)));
    }

    [Test]
    public void Fit_UsesMeasuredWidths()
    {
        // A wide "Smart Focus" button (120 px) first, then round ones.
        int? Width(int i) => i == 0 ? 120 : null;

        Assert.That(ActionOverflow.Fit(3, 120 + 38 + 38 + 12, Width), Is.EqualTo(new ActionOverflow.Layout(3, 0)));
        // 38 ("…") + 120 + 6 = 164: the wide one fits, the next does not.
        Assert.That(ActionOverflow.Fit(3, 200, Width), Is.EqualTo(new ActionOverflow.Layout(1, 2)));
        Assert.That(ActionOverflow.Fit(3, 150, Width), Is.EqualTo(new ActionOverflow.Layout(0, 3)));
    }

    [Test]
    public void DebugFeatures_FollowTheBuild_OrTheHost()
    {
        Assert.That(new DebugFeatures(enabledByHost: true).Enabled, Is.True);
        Assert.That(new DebugFeatures().Enabled, Is.EqualTo(DebugFeatures.IsDebugBuild));
    }
}
