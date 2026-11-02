using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerStat { MoveSpeed, Acceleration, Braking, AirControl, JumpSpeed, PoundSpeed, ImpactDamage, CameraControl, ComboDuration, ComboMultiplierGrowth, ScoreAmplification, TurnAcceleration, Gravity, MaxFallSpeed, SpeedPerRoom, PoundSpeedPerRoom, CoyoteTime, JumpBufferTime, GroundStickSpeed, CameraSensitivity, CameraPenaltySpeed, CameraMinimumControl, CameraPitchLimit, ShopDiscount, GroundControl }
public sealed class PlayerStats : MonoBehaviour
{
    readonly Dictionary<PlayerStat, StatValue> values = new Dictionary<PlayerStat, StatValue>();
    public event Action Changed;
    public StatValue Get(PlayerStat stat)
    {
        if (!values.TryGetValue(stat, out var value)) values.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, PlayerStat stat, StatModifierType type, float value)
    {
        Get(stat).Add(source, type, value);
        Changed?.Invoke();
    }
    public void RemoveModifiers(object source)
    {
        if (source == null) return;
        bool removed = false;
        foreach (var stat in values.Values) removed |= stat.RemoveSource(source);
        if (removed) Changed?.Invoke();
    }
    public float Evaluate(PlayerStat stat, float baseValue) => Get(stat).Calculate(baseValue);
    public float EvaluateExcluding(PlayerStat stat, float baseValue, IReadOnlyList<object> excludedSources) =>
        Get(stat).CalculateExcluding(baseValue, excludedSources);
}
