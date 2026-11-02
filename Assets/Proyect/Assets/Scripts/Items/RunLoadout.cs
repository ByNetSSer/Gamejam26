using System;
using System.Collections.Generic;
using UnityEngine;

public enum RunLoadoutKind { Item, Curse }

[Serializable]
public readonly struct RunStatEffect
{
    public readonly PlayerStat Stat;
    public readonly StatModifierType Type;
    public readonly float Value;

    public RunStatEffect(PlayerStat stat, StatModifierType type, float value)
    {
        Stat = stat;
        Type = type;
        Value = value;
    }
}

public sealed class RunLoadoutEntry
{
    readonly List<object> sources = new List<object>();
    readonly List<RunStatEffect> effects = new List<RunStatEffect>();

    public string Id { get; internal set; }
    public string DisplayName { get; internal set; }
    public string Description { get; internal set; }
    public Sprite Icon { get; internal set; }
    public ItemTier Tier { get; internal set; }
    public RunLoadoutKind Kind { get; internal set; }
    public EnemyCurse EnemyCurse { get; internal set; }
    public int Count { get; internal set; }
    public IReadOnlyList<object> Sources => sources;
    public IReadOnlyList<RunStatEffect> Effects => effects;

    internal void AddSource(object source)
    {
        if (source != null) sources.Add(source);
    }

    internal void SetEffects(IReadOnlyList<RunStatEffect> newEffects)
    {
        if (effects.Count > 0 || newEffects == null) return;
        for (int i = 0; i < newEffects.Count; i++) effects.Add(newEffects[i]);
    }
}

// Runtime ledger for everything that was actually granted during this run.
// It does not infer ownership from modifiers, so conditional and enemy effects are retained too.
public sealed class RunLoadout : MonoBehaviour
{
    readonly List<RunLoadoutEntry> entries = new List<RunLoadoutEntry>();
    readonly Dictionary<string, RunLoadoutEntry> byId = new Dictionary<string, RunLoadoutEntry>();

    public IReadOnlyList<RunLoadoutEntry> Entries => entries;
    public event Action Changed;

    public static RunLoadout Ensure(PlayerController player)
    {
        if (player == null) return null;
        var loadout = player.GetComponent<RunLoadout>();
        return loadout != null ? loadout : player.gameObject.AddComponent<RunLoadout>();
    }

    public void Record(string displayName, string description, Sprite icon, ItemTier tier,
        RunLoadoutKind kind, ItemAcquisitionSource source, object modifierSource,
        IReadOnlyList<RunStatEffect> effects = null, EnemyCurse enemyCurse = EnemyCurse.None)
    {
        string id = BuildId(displayName, kind, enemyCurse);
        if (!byId.TryGetValue(id, out var entry))
        {
            entry = new RunLoadoutEntry
            {
                Id = id,
                DisplayName = CanonicalCurseName(displayName, enemyCurse),
                Description = description ?? string.Empty,
                Icon = icon,
                Tier = tier,
                Kind = kind,
                EnemyCurse = enemyCurse
            };
            entries.Add(entry);
            byId.Add(id, entry);
        }
        else
        {
            if (entry.Icon == null && icon != null) entry.Icon = icon;
            if (!string.IsNullOrWhiteSpace(description)) entry.Description = description;
            if ((int)tier > (int)entry.Tier) entry.Tier = tier;
        }

        entry.Count++;
        entry.AddSource(modifierSource);
        entry.SetEffects(effects);
        Changed?.Invoke();
    }

    static string BuildId(string displayName, RunLoadoutKind kind, EnemyCurse enemyCurse)
    {
        if (kind == RunLoadoutKind.Curse && enemyCurse != EnemyCurse.None)
            return "curse:enemy:" + enemyCurse;
        return (kind == RunLoadoutKind.Curse ? "curse:" : "item:") + (displayName ?? string.Empty).Trim().ToLowerInvariant();
    }

    static string CanonicalCurseName(string fallback, EnemyCurse curse)
    {
        return curse switch
        {
            EnemyCurse.Flupin => "Flupin",
            EnemyCurse.MaskCharger => "Máscara",
            EnemyCurse.Dontsee => "Dontsee",
            EnemyCurse.CorridorChaser => "Perseguidor",
            EnemyCurse.Guilt => "Guilt",
            _ => fallback
        };
    }
}
