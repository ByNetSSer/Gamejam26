using System;

public sealed class Timer : IDisposable
{
    readonly float duration;
    readonly float interval;
    double elapsed, nextTick;
    bool running;
    public bool IsRunning => running;
    public event Action Tick;
    public event Action Completed;
    public Timer(float duration, float interval = 0f)
    {
        if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
        if ((interval > 0f && interval < 0.001f) || interval < 0f || float.IsNaN(interval) || float.IsInfinity(interval)) throw new ArgumentOutOfRangeException(nameof(interval));
        this.duration = duration; this.interval = interval;
    }
    public void Start()
    {
        elapsed = 0f; nextTick = interval;
        if (!running) { running = true; TimerScheduler.Add(this); }
    }
    internal void Advance(float deltaTime)
    {
        if (!running) return;
        elapsed = Math.Min(duration, elapsed + deltaTime);
        int processed = 0;
        while (running && interval > 0f && nextTick <= elapsed && nextTick <= duration && processed++ < 256)
        {
            // Double precision prevents small intervals from becoming stuck on long timers.
            nextTick += interval;
            Tick?.Invoke();
        }
        if (!running || elapsed < duration || (interval > 0f && nextTick <= elapsed && nextTick <= duration)) return;
        Cancel();
        Completed?.Invoke();
    }
    // Cancellation is not completion. Effects decide how to clean up.
    public void Cancel() { if (!running) return; running = false; TimerScheduler.Remove(this); }
    public void Dispose() { Cancel(); Tick = null; Completed = null; }
}
