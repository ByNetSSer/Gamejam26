using System;
using System.Collections.Generic;
using UnityEngine;

// Runtime-only stat, adapted from the RPG project. Modifiers belong to their owner/source.
public sealed class StatValue
{
    struct Modifier { public object source; public StatModifierType type; public float value; }
    readonly List<Modifier> modifiers = new List<Modifier>();
    public void Add(object source, StatModifierType type, float value)
    {
        if (source == null) throw new ArgumentNullException(nameof(source));
        modifiers.Add(new Modifier { source = source, type = type, value = value });
    }
    public bool RemoveSource(object source) => modifiers.RemoveAll(m => ReferenceEquals(m.source, source)) > 0;
    public float Calculate(float baseValue)
    {
        return CalculateExcluding(baseValue, null);
    }
    public float CalculateExcluding(float baseValue, IReadOnlyList<object> excludedSources)
    {
        float flat = 0f, additive = 0f, multiplicative = 1f;
        foreach (var modifier in modifiers)
        {
            if (ContainsReference(excludedSources, modifier.source)) continue;
            switch (modifier.type)
            {
                case StatModifierType.Flat: flat += modifier.value; break;
                case StatModifierType.Percent: additive += modifier.value; break;
                case StatModifierType.PercentMult: multiplicative *= 1f + modifier.value; break;
            }
        }
        return Mathf.Max(0f, (baseValue + flat) * (1f + additive) * multiplicative);
    }
    static bool ContainsReference(IReadOnlyList<object> sources, object candidate)
    {
        if (sources == null) return false;
        for (int i = 0; i < sources.Count; i++)
            if (ReferenceEquals(sources[i], candidate)) return true;
        return false;
    }
}
