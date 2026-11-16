namespace Kazo.Bar.Tests.Unit.Layout;

using Kazo.Bar.Core.Layout;
using Kazo.Bar.Core.Models;

[TestFixture]
public class ScreenFitTests
{
    private static readonly SurfaceSize Surface = new(960, 500);

    [TestCase(1.0, 960, 500)]
    [TestCase(1.5, 1440, 750)]
    [TestCase(1.25, 1200, 625)]
    [TestCase(0.0, 960, 500, TestName = "Invalid scale falls back to 100%")]
    public void ToPixels_ScalesTheLogicalSurface(double scale, int width, int height)
    {
        Assert.That(ScreenFit.ToPixels(Surface, scale), Is.EqualTo((width, height)));
    }

    [Test]
    public void NeedsResize_WhenWindowKeptTheOldMonitorSize()
    {
        // Moved from 100% to 150%: the window is still 960x500 physical pixels.
        var window = new PixelRect(2000, 0, 2960, 500);

        Assert.That(ScreenFit.NeedsResize(window, Surface, 1.5), Is.True);
        Assert.That(ScreenFit.NeedsResize(window, Surface, 1.0), Is.False);
    }

    [Test]
    public void NeedsResize_ToleratesOnePixelOfRounding()
    {
        var window = new PixelRect(0, 0, 1201, 624);

        Assert.That(ScreenFit.NeedsResize(window, Surface, 1.25), Is.False);
    }

    [Test]
    public void Resize_KeepsHorizontalCenterAndTopEdge()
    {
        var window = new PixelRect(1000, 100, 1960, 600);

        var resized = ScreenFit.Resize(window, 1440, 750, anchorBottom: false);

        Assert.That(resized, Is.EqualTo(new PixelRect(760, 100, 2200, 850)));
    }

    [Test]
    public void Resize_KeepsBottomEdgeWhenDockedAtTheBottom()
    {
        var window = new PixelRect(1000, 580, 1960, 1080);

        var resized = ScreenFit.Resize(window, 1440, 750, anchorBottom: true);

        Assert.That(resized.Bottom, Is.EqualTo(1080));
        Assert.That(resized.Top, Is.EqualTo(330));
    }

    [Test]
    public void ToScreen_ConvertsCssPixelsWithTheWindowScale()
    {
        var window = new PixelRect(100, 50, 1540, 800);

        var pill = ScreenFit.ToScreen(window, 10.2, 4, 900.1, 48, 1.5);

        Assert.That(pill, Is.EqualTo(new PixelRect(115, 56, 1451, 122)));
    }

    [Test]
    public void OffsetIntoWorkArea_NothingWhenInside()
    {
        var work = new PixelRect(0, 0, 1920, 1040);

        Assert.That(ScreenFit.OffsetIntoWorkArea(new PixelRect(10, 10, 900, 60), work), Is.EqualTo((0, 0)));
    }

    [Test]
    public void OffsetIntoWorkArea_PullsBackAPillPastTheRightEdgeOfASecondMonitor()
    {
        // Second monitor to the right of the primary; the pill sticks out by 200 px.
        var work = new PixelRect(1920, 0, 4480, 1400);
        var pill = new PixelRect(3800, 20, 4680, 90);

        Assert.That(ScreenFit.OffsetIntoWorkArea(pill, work), Is.EqualTo((-200, 0)));
    }

    [Test]
    public void OffsetIntoWorkArea_PullsDownAPillAboveTheTop()
    {
        var work = new PixelRect(0, 0, 1920, 1040);

        Assert.That(ScreenFit.OffsetIntoWorkArea(new PixelRect(100, -30, 900, 40), work), Is.EqualTo((0, 30)));
    }

    [Test]
    public void OffsetIntoWorkArea_LeftEdgeWinsWhenThePillIsWiderThanTheScreen()
    {
        var work = new PixelRect(0, 0, 1280, 1000);
        var pill = new PixelRect(200, 0, 1700, 60);

        Assert.That(ScreenFit.OffsetIntoWorkArea(pill, work), Is.EqualTo((-200, 0)));
    }
}
