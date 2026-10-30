using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Runner/Item de estadísticas", fileName = "RunnerItem")]
public sealed class PlayerStatItem : ScriptableObject
{
    [Serializable] public struct Effect { public PlayerStat stat; public StatModifierType type; public float value; }
    [SerializeField] Effect[] effects;
    // Pass a unique source for each acquired copy. Keep it to remove that copy later.
    public void Apply(PlayerStats stats, object source)
    {
        if (stats == null || effects == null) return;
        foreach (var effect in effects) stats.AddModifier(source, effect.stat, effect.type, effect.value);
    }
    public void Remove(PlayerStats stats, object source) { if (stats != null) stats.RemoveModifiers(source); }
}
