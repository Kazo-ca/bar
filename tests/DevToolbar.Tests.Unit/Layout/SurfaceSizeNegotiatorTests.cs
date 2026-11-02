namespace DevToolbar.Tests.Unit.Layout;

using DevToolbar.Core.Layout;
using DevToolbar.Core.Models;

[TestFixture]
public class SurfaceSizeNegotiatorTests
{
    private static readonly SurfaceSize Base = new(960, 500);

    [Test]
    public void Target_is_base_size_without_requests()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);

        Assert.That(negotiator.Target, Is.EqualTo(Base));
    }

    [Test]
    public void Target_grows_to_largest_request()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);

        negotiator.Request("settings", new SurfaceSize(800, 1400));
        negotiator.Request("popup", new SurfaceSize(1100, 600));

        Assert.That(negotiator.Target, Is.EqualTo(new SurfaceSize(1100, 1400)));
    }

    [Test]
    public void Target_is_clamped_to_available_space()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);
        negotiator.SetAvailable(new SurfaceSize(1920, 1040));

        negotiator.Request("settings", new SurfaceSize(800, 2400));

        Assert.That(negotiator.Target, Is.EqualTo(new SurfaceSize(960, 1040)));
    }

    [Test]
    public void Target_never_goes_below_base_size_even_when_little_space_is_available()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);
        negotiator.SetAvailable(new SurfaceSize(700, 300));

        negotiator.Request("settings", new SurfaceSize(800, 2400));

        Assert.That(negotiator.Target, Is.EqualTo(Base));
    }

    [Test]
    public void Release_returns_to_base_size()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);
        negotiator.Request("settings", new SurfaceSize(800, 1400));

        var released = negotiator.Release("settings");

        Assert.Multiple(() =>
        {
            Assert.That(released, Is.True);
            Assert.That(negotiator.Target, Is.EqualTo(Base));
            Assert.That(negotiator.Release("settings"), Is.False);
        });
    }

    [Test]
    public void Request_replaces_previous_request_of_same_component()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);
        negotiator.Request("settings", new SurfaceSize(800, 1400));

        negotiator.Request("settings", new SurfaceSize(800, 700));

        Assert.That(negotiator.Target, Is.EqualTo(new SurfaceSize(960, 700)));
    }

    [Test]
    public void Fractional_sizes_are_rounded_up()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);

        negotiator.Request("settings", new SurfaceSize(10, 700.2));

        Assert.That(negotiator.Target.Height, Is.EqualTo(701));
    }

    [TestCase("")]
    [TestCase("  ")]
    public void Request_with_empty_id_is_ignored(string id)
    {
        var negotiator = new SurfaceSizeNegotiator(Base);

        Assert.That(negotiator.Request(id, new SurfaceSize(800, 1400)), Is.False);
        Assert.That(negotiator.Target, Is.EqualTo(Base));
    }

    [Test]
    public void Request_with_invalid_size_is_ignored()
    {
        var negotiator = new SurfaceSizeNegotiator(Base);

        Assert.Multiple(() =>
        {
            Assert.That(negotiator.Request("a", new SurfaceSize(double.NaN, 10)), Is.False);
            Assert.That(negotiator.Request("b", new SurfaceSize(10, -1)), Is.False);
            Assert.That(negotiator.Requests, Is.Empty);
        });
    }

    [Test]
    public void Constructor_rejects_infinite_base_size()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SurfaceSizeNegotiator(SurfaceSize.Unbounded));
    }
}
