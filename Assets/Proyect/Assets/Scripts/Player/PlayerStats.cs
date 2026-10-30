using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerStat { MoveSpeed, Acceleration, Braking, AirControl, JumpSpeed, PoundSpeed, ImpactDamage, CameraControl, ComboDuration, ComboMultiplierGrowth, ScoreAmplification, TurnAcceleration, Gravity, MaxFallSpeed, SpeedPerRoom, PoundSpeedPerRoom, CoyoteTime, JumpBufferTime, GroundStickSpeed, CameraSensitivity, CameraPenaltySpeed, CameraMinimumControl, CameraPitchLimit, ShopDiscount, GroundControl }
public sealed class PlayerStats : MonoBehaviour
{
    readonly Dictionary<PlayerStat, StatValue> values = new Dictionary<PlayerStat, StatValue>();
    public StatValue Get(PlayerStat stat)
    {
        if (!values.TryGetValue(stat, out var value)) values.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, PlayerStat stat, StatModifierType type, float value) => Get(stat).Add(source, type, value);
    public void RemoveModifiers(object source)
    {
        if (source == null) return;
        foreach (var stat in values.Values) stat.RemoveSource(source);
    }
    public float Evaluate(PlayerStat stat, float baseValue) => Get(stat).Calculate(baseValue);
}
