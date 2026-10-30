using System;
using UnityEngine;

// Owned by one manager/session; listeners must dispose their subscriptions.
public sealed class GameEventBus : MonoBehaviour
{
    EventStream stream;
    EventStream Stream => stream ?? (stream = new EventStream(Debug.LogException));
    public IDisposable Subscribe<T>(Action<T> listener) => Stream.Subscribe(listener);
    public void Publish<T>(T message) => Stream.Publish(message);
    void OnDestroy() { stream?.Clear(); }
}
