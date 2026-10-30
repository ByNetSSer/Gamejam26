using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Runner/Item de estadísticas", fileName = "RunnerItem")]
public sealed class PlayerStatItem : ScriptableObject
{
    [Serializable] public struct Effect { public PlayerStat stat; public StatModifierType type; public float value; }
    [SerializeField] Effect[] effects;
    [SerializeField] ItemDefinition definition;
    // Pass a unique source for each acquired copy. Keep it to remove that copy later.
    public void Apply(PlayerStats stats, object source, ItemAcquisitionSource acquisitionSource = ItemAcquisitionSource.Pickup)
    {
        if (stats == null || effects == null) return;
        foreach (var effect in effects) stats.AddModifier(source, effect.stat, effect.type, effect.value);
        if (effects.Length == 0) return;
        var descriptions = new System.Collections.Generic.List<string>();
        foreach (var effect in effects) descriptions.Add(ItemAcquisitionHud.Describe(effect.stat, effect.type, effect.value));
        ItemAcquisitionHud.Show(stats.GetComponent<PlayerController>(), definition != null ? definition.DisplayName : name,
            string.Join("\n", descriptions), definition != null ? definition.Icon : null, acquisitionSource);
    }
    public void Remove(PlayerStats stats, object source) { if (stats != null) stats.RemoveModifiers(source); }
}
