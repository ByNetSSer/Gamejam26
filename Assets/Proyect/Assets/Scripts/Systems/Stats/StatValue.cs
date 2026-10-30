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
    public void RemoveSource(object source) => modifiers.RemoveAll(m => ReferenceEquals(m.source, source));
    public float Calculate(float baseValue)
    {
        float flat = 0f, additive = 0f, multiplicative = 1f;
        foreach (var modifier in modifiers)
        {
            switch (modifier.type)
            {
                case StatModifierType.Flat: flat += modifier.value; break;
                case StatModifierType.Percent: additive += modifier.value; break;
                case StatModifierType.PercentMult: multiplicative *= 1f + modifier.value; break;
            }
        }
        return Mathf.Max(0f, (baseValue + flat) * (1f + additive) * multiplicative);
    }
}
