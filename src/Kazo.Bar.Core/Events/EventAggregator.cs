namespace Kazo.Bar.Core.Events;

/// <summary>
/// Simple publish/subscribe event aggregator.
/// </summary>
public class EventAggregator
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();

    /// <summary>Subscribe to an event type.</summary>
    public void Subscribe<TEvent>(Action<TEvent> handler)
    {
        var type = typeof(TEvent);
        if (!_subscriptions.ContainsKey(type))
            _subscriptions[type] = new List<Delegate>();
        _subscriptions[type].Add(handler);
    }

    /// <summary>Unsubscribe from an event type.</summary>
    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        var type = typeof(TEvent);
        if (_subscriptions.ContainsKey(type))
            _subscriptions[type].Remove(handler);
    }

    /// <summary>Publish an event to all subscribers.</summary>
    public void Publish<TEvent>(TEvent eventData)
    {
        var type = typeof(TEvent);
        if (_subscriptions.TryGetValue(type, out var handlers))
        {
            foreach (var handler in handlers.ToList())
            {
                ((Action<TEvent>)handler)(eventData);
            }
        }
    }
}
