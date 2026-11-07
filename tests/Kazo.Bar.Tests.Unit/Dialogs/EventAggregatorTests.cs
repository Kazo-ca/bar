namespace Kazo.Bar.Tests.Unit.Dialogs;

using Kazo.Bar.Core.Events;

[TestFixture]
public class EventAggregatorTests
{
    private sealed record CommitCreated(string Sha);

    [Test]
    public void Publish_ReachesEverySubscriberOfThatType()
    {
        var bus = new EventAggregator();
        var received = new List<string>();
        bus.Subscribe<CommitCreated>(e => received.Add("toolbar " + e.Sha));
        bus.Subscribe<CommitCreated>(e => received.Add("dialog " + e.Sha));
        bus.Subscribe<string>(_ => received.Add("other type"));

        bus.Publish(new CommitCreated("abc"));

        Assert.That(received, Is.EqualTo(new[] { "toolbar abc", "dialog abc" }));
    }

    [Test]
    public void DisposingTheSubscription_StopsDelivery()
    {
        var bus = new EventAggregator();
        var count = 0;
        var subscription = bus.Subscribe<CommitCreated>(_ => count++);

        bus.Publish(new CommitCreated("1"));
        subscription.Dispose();
        subscription.Dispose();
        bus.Publish(new CommitCreated("2"));

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void HandlerMayUnsubscribeWhilePublishing()
    {
        var bus = new EventAggregator();
        IDisposable? subscription = null;
        var count = 0;
        subscription = bus.Subscribe<CommitCreated>(_ =>
        {
            count++;
            subscription!.Dispose();
        });

        bus.Publish(new CommitCreated("1"));
        bus.Publish(new CommitCreated("2"));

        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public async Task ConcurrentSubscribeAndPublish_DoNotThrow()
    {
        var bus = new EventAggregator();
        var total = 0;

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                using var subscription = bus.Subscribe<CommitCreated>(_ => Interlocked.Increment(ref total));
                bus.Publish(new CommitCreated(i.ToString()));
            }
        })));

        Assert.That(total, Is.GreaterThanOrEqualTo(8 * 500));
    }
}
