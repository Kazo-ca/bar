namespace Kazo.Bar.Core.Events;

/// <summary>
/// Application-wide publish/subscribe bus, registered as a singleton.
///
/// Every window of a desktop host (the toolbar and each dialog window) shares the same
/// instance, so it is how two windows send messages to each other: one publishes a
/// message record, the others receive it. Thread-safe. Handlers run synchronously on the
/// publisher's thread: a Blazor component must wrap its UI work in <c>InvokeAsync</c>.
/// Dispose the value returned by <see cref="Subscribe{TEvent}"/> (or call
/// <see cref="Unsubscribe{TEvent}"/>) when the component goes away.
/// </summary>
public class EventAggregator
{
    private readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private readonly object _gate = new();

    /// <summary>Subscribe to an event type. Dispose the result to unsubscribe.</summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var handlers))
            {
                handlers = new List<Delegate>();
                _subscriptions[typeof(TEvent)] = handlers;
            }
            handlers.Add(handler);
        }
        return new Subscription(() => Unsubscribe(handler));
    }

    /// <summary>Unsubscribe from an event type.</summary>
    public void Unsubscribe<TEvent>(Action<TEvent> handler)
    {
        lock (_gate)
        {
            if (_subscriptions.TryGetValue(typeof(TEvent), out var handlers))
                handlers.Remove(handler);
        }
    }

    /// <summary>Publish an event to all subscribers of exactly <typeparamref name="TEvent"/>.</summary>
    public void Publish<TEvent>(TEvent eventData)
    {
        Delegate[] snapshot;
        lock (_gate)
        {
            if (!_subscriptions.TryGetValue(typeof(TEvent), out var handlers)) return;
            snapshot = handlers.ToArray();
        }
        foreach (var handler in snapshot)
        {
            ((Action<TEvent>)handler)(eventData);
        }
    }

    private sealed class Subscription(Action unsubscribe) : IDisposable
    {
        private Action? _unsubscribe = unsubscribe;

        public void Dispose() => Interlocked.Exchange(ref _unsubscribe, null)?.Invoke();
    }
}
