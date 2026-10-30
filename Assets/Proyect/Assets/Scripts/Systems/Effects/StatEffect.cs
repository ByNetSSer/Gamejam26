using System;

public sealed class StatEffect
{
    readonly StatValue stat;
    readonly StatModifierType type;
    readonly float value, duration;
    Timer timer;
    bool active;
    public bool IsActive => active;
    public event Action<StatEffect> Finished;
    public StatEffect(StatValue stat, StatModifierType type, float value, float duration)
    {
        if (float.IsNaN(duration) || float.IsInfinity(duration)) throw new ArgumentOutOfRangeException(nameof(duration));
        this.stat = stat ?? throw new ArgumentNullException(nameof(stat)); this.type = type; this.value = value; this.duration = duration;
    }
    public void Start()
    {
        if (active) return;
        active = true;
        stat.Add(this, type, value);
        if (duration > 0f)
        {
            timer = new Timer(duration);
            timer.Completed += Cancel;
            timer.Start();
        }
        else if (duration == 0f) Cancel();
        // duration < 0 means presence/permanent until explicitly removed.
    }
    public void Cancel()
    {
        if (!active) return;
        active = false;
        if (timer != null) { timer.Completed -= Cancel; timer.Dispose(); timer = null; }
        stat.RemoveSource(this);
        Finished?.Invoke(this);
    }
}
