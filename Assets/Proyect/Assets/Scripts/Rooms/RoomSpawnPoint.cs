using System;
using UnityEngine;

[Serializable]
public struct SpawnOption
{
    public GameObject prefab;
    [Min(0f)] public float weight;
    public Vector3 localOffset;
}

// Each marker defines allowed prefabs, weight and probability; no global spawning rules.
public sealed class RoomSpawnPoint : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float chance = 0.6f;
    [Tooltip("Opcional: usa un catálogo por tamaño en lugar de la lista de opciones.")]
    [SerializeField] PropSpawnPool propPool;
    [SerializeField] PropSize allowedSizes = PropSize.Small | PropSize.Medium | PropSize.Large;
    [SerializeField] Vector3 poolLocalOffset;
    [SerializeField] SpawnOption[] options;
    public void Configure(float probability, SpawnOption[] choices) { chance = Mathf.Clamp01(probability); propPool = null; options = choices; }
    public void Spawn(System.Random random, float chanceMultiplier = 1f)
    {
        if (!isActiveAndEnabled || random == null || random.NextDouble() >= Mathf.Clamp01(chance * Mathf.Max(0f, chanceMultiplier))) return;
        if (propPool != null)
        {
            var prefab = propPool.Choose(random, allowedSizes);
            if (prefab != null)
            {
                var spawned = Instantiate(prefab, transform.TransformPoint(poolLocalOffset), transform.rotation, transform);
                PropStackPoint.Populate(spawned, random);
            }
            return;
        }
        if (options == null) return;
        double total = 0;
        foreach (var option in options) if (option.prefab != null && option.weight > 0f) total += option.weight;
        if (total <= 0) return;
        double choice = random.NextDouble() * total;
        foreach (var option in options)
        {
            if (option.prefab == null || option.weight <= 0f) continue;
            choice -= option.weight;
            if (choice >= 0) continue;
            var instance = Instantiate(option.prefab, transform.TransformPoint(option.localOffset), transform.rotation, transform);
            PropStackPoint.Populate(instance, random);
            return;
        }
    }
}
