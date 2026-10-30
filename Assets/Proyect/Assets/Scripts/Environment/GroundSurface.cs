using System;
using System.Collections.Generic;
using UnityEngine;

// Surface regions are explicitly queried by GroundSurfaceSensor, independent of trigger callbacks.
[RequireComponent(typeof(Collider))]
public sealed class GroundSurface : MonoBehaviour
{
    [Serializable] public struct Modifier { public PlayerStat stat; public StatModifierType type; public float value; }
    [SerializeField] Modifier[] modifiers;
    [SerializeField, Min(0f)] float damagePerTick;
    [SerializeField, Min(0.01f)] float damageInterval = 1f;
    readonly Dictionary<PlayerController, float> contacts = new Dictionary<PlayerController, float>();
    public void Configure(Modifier[] effects, float damage, float interval = 1f)
    {
        modifiers = effects;
        damagePerTick = Mathf.Max(0f, damage);
        damageInterval = Mathf.Max(0.01f, interval);
    }
    public void Enter(PlayerController player)
    {
        if (!isActiveAndEnabled || player == null || contacts.ContainsKey(player)) return;
        contacts.Add(player, Time.time + Mathf.Max(0.01f, damageInterval));
        if (modifiers != null)
            foreach (var modifier in modifiers) player.Stats.AddModifier(this, modifier.stat, modifier.type, modifier.value);
    }
    public void Stay(PlayerController player)
    {
        if (player == null || !contacts.TryGetValue(player, out float nextDamage) || damagePerTick <= 0f || Time.time < nextDamage) return;
        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.TakeDamage(damagePerTick);
        contacts[player] = Time.time + Mathf.Max(0.01f, damageInterval);
    }
    public void Exit(PlayerController player)
    {
        if (ReferenceEquals(player, null) || !contacts.Remove(player)) return;
        if (player != null && player.Stats != null) player.Stats.RemoveModifiers(this);
    }
    void OnDisable()
    {
        var players = new List<PlayerController>(contacts.Keys);
        foreach (var player in players) Exit(player);
        contacts.Clear();
    }
}
