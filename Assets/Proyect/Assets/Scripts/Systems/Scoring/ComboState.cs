using System;

public readonly struct ScoreAward
{
    public readonly string Reason;
    public readonly int BasePoints;
    public readonly int Points;
    public readonly int ComboCount;
    public readonly float Multiplier;
    public ScoreAward(string reason, int basePoints, int points, int comboCount, float multiplier)
    {
        Reason = reason; BasePoints = basePoints; Points = points; ComboCount = comboCount; Multiplier = multiplier;
    }
}

// Pure combo state: time is provided by the caller so it can be tested independently of Unity.
public sealed class ComboState
{
    public int Count { get; private set; }
    public float ExpiresAt { get; private set; }
    public float WindowDuration { get; private set; }
    public bool Expire(float now)
    {
        if (Count == 0 || now < ExpiresAt) return false;
        Count = 0; ExpiresAt = 0f; WindowDuration = 0f; return true;
    }
    public ScoreAward Register(string reason, int basePoints, float now, float duration, float growth, float amplification)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("El motivo no puede estar vacío.", nameof(reason));
        if (basePoints < 0) throw new ArgumentOutOfRangeException(nameof(basePoints));
        Expire(now);
        Count++;
        WindowDuration = Math.Max(0f, duration);
        ExpiresAt = now + WindowDuration;
        float multiplier = Math.Max(0f, (1f + (Count - 1) * Math.Max(0f, growth)) * Math.Max(0f, amplification));
        double awarded = Math.Round((double)basePoints * multiplier, MidpointRounding.AwayFromZero);
        int points = awarded >= int.MaxValue ? int.MaxValue : (int)awarded;
        return new ScoreAward(reason, basePoints, points, Count, multiplier);
    }
    public void Reset() { Count = 0; ExpiresAt = 0f; WindowDuration = 0f; }
}
