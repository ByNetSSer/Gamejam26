using System;
using UnityEngine;

[Flags] public enum PropSize { Small = 1, Medium = 2, Large = 4 }

[CreateAssetMenu(menuName = "Runner/Pool de props", fileName = "PropSpawnPool")]
public sealed class PropSpawnPool : ScriptableObject
{
    [Serializable] public struct Entry
    {
        public GameObject prefab;
        public PropSize size;
        [Min(0f)] public float weight;
    }
    [SerializeField] Entry[] entries;

    public GameObject Choose(System.Random random, PropSize allowed)
    {
        if (random == null || entries == null) return null;
        double total = 0;
        foreach (var entry in entries)
            if (entry.prefab != null && (entry.size & allowed) != 0 && entry.weight > 0f && !float.IsInfinity(entry.weight)) total += entry.weight;
        if (total <= 0) return null;
        double roll = random.NextDouble() * total;
        foreach (var entry in entries)
        {
            if (entry.prefab == null || (entry.size & allowed) == 0 || entry.weight <= 0f || float.IsNaN(entry.weight) || float.IsInfinity(entry.weight)) continue;
            roll -= entry.weight;
            if (roll < 0) return entry.prefab;
        }
        return null;
    }
}
