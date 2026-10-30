using System;
using System.Collections.Generic;

// Local typed event stream; no global singleton and no dependency on Unity.
public sealed class EventStream
{
    interface ISubscription { bool Active { get; } void Invoke(object value); void Deactivate(); }
    sealed class Subscription<T> : ISubscription, IDisposable
    {
        readonly EventStream owner;
        readonly Action<T> callback;
        public bool Active { get; private set; } = true;
        public Subscription(EventStream owner, Action<T> callback) { this.owner = owner; this.callback = callback; }
        public void Invoke(object value) { if (Active) callback((T)value); }
        public void Deactivate() => Active = false;
        public void Dispose()
        {
            if (!Active) return;
            Active = false;
            owner.Remove(typeof(T), this);
        }
    }
    readonly Dictionary<Type, List<ISubscription>> subscriptions = new Dictionary<Type, List<ISubscription>>();
    readonly Action<Exception> reportException;
    readonly Queue<(Type type, object value)> pending = new Queue<(Type, object)>();
    bool dispatching;
    public EventStream(Action<Exception> reportException = null) { this.reportException = reportException; }
    public IDisposable Subscribe<T>(Action<T> callback)
    {
        if (callback == null) throw new ArgumentNullException(nameof(callback));
        if (!subscriptions.TryGetValue(typeof(T), out var list)) subscriptions.Add(typeof(T), list = new List<ISubscription>());
        var subscription = new Subscription<T>(this, callback);
        list.Add(subscription);
        return subscription;
    }
    public void Publish<T>(T value)
    {
        pending.Enqueue((typeof(T), value));
        if (dispatching) return;
        dispatching = true;
        try
        {
            while (pending.Count > 0)
            {
                var message = pending.Dequeue();
                if (!subscriptions.TryGetValue(message.type, out var list)) continue;
                var snapshot = list.ToArray();
                foreach (var listener in snapshot)
                {
                    if (!listener.Active) continue;
                    try { listener.Invoke(message.value); }
                    catch (Exception exception)
                    {
                        try { reportException?.Invoke(exception); } catch { /* Reporting must not stop dispatch. */ }
                    }
                }
            }
        }
        finally { dispatching = false; }
    }
    void Remove(Type type, ISubscription subscription)
    {
        if (!subscriptions.TryGetValue(type, out var list)) return;
        list.Remove(subscription);
        if (list.Count == 0) subscriptions.Remove(type);
    }
    public void Clear()
    {
        foreach (var list in subscriptions.Values)
            foreach (var subscription in list) subscription.Deactivate();
        subscriptions.Clear();
        pending.Clear();
    }
}
