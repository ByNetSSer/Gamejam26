using System;
using System.Collections.Generic;
using UnityEngine;

// A target owns effects; each application has a unique modifier source and timer.
[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerStatEffects : MonoBehaviour
{
    readonly List<StatEffect> effects = new List<StatEffect>();
    PlayerStats stats;
    void Awake() => stats = GetComponent<PlayerStats>();
    public StatEffect Apply(PlayerStat stat, StatModifierType type, float value, float duration)
    {
        if (!isActiveAndEnabled) throw new InvalidOperationException("El receptor de efectos está desactivado.");
        if (stats == null) stats = GetComponent<PlayerStats>();
        var effect = new StatEffect(stats.Get(stat), type, value, duration);
        effects.Add(effect);
        effect.Finished += Remove;
        effect.Start();
        return effect;
    }
    void Remove(StatEffect effect) { effect.Finished -= Remove; effects.Remove(effect); }
    void OnDisable()
    {
        var copy = effects.ToArray();
        foreach (var effect in copy) effect.Cancel();
        effects.Clear();
    }
}

