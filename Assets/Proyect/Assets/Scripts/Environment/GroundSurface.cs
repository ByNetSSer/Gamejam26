using System;
using System.Collections.Generic;
using UnityEngine;

public enum SurfaceAudioKind { Normal, Ice, Tar, Lava }

// Several floor colliders can describe one continuous hazard. This coordinator
// keeps one grace timer and one damage cadence per player across all of them.
public sealed class GroundSurfaceDamageGroup
{
    sealed class ContactState
    {
        public int Count;
        public float NextDamage;
    }

    readonly Dictionary<PlayerController, ContactState> contacts = new Dictionary<PlayerController, ContactState>();

    public void Enter(PlayerController player, float graceTime)
    {
        if (player == null) return;
        if (contacts.TryGetValue(player, out var state))
        {
            state.Count++;
            return;
        }
        contacts.Add(player, new ContactState { Count = 1, NextDamage = Time.time + Mathf.Max(0f, graceTime) });
    }

    public void Stay(PlayerController player, float damage, float interval)
    {
        if (player == null || damage <= 0f || !contacts.TryGetValue(player, out var state) || Time.time < state.NextDamage) return;
        var health = player.GetComponent<PlayerHealth>();
        if (health != null) health.TakeDamage(damage, RunLossReason.HazardousSurface);
        state.NextDamage = Time.time + Mathf.Max(0.01f, interval);
    }

    public void Exit(PlayerController player)
    {
        if (ReferenceEquals(player, null) || !contacts.TryGetValue(player, out var state)) return;
        state.Count--;
        if (state.Count <= 0) contacts.Remove(player);
    }

    public void Clear() => contacts.Clear();
}

// Surface regions are explicitly queried by GroundSurfaceSensor, independent of trigger callbacks.
[RequireComponent(typeof(Collider))]
public sealed class GroundSurface : MonoBehaviour
{
    [Serializable] public struct Modifier { public PlayerStat stat; public StatModifierType type; public float value; }
    [SerializeField] Modifier[] modifiers;
    [SerializeField] bool tarSurface;
    [SerializeField] SurfaceAudioKind audioKind;
    public SurfaceAudioKind AudioKind => tarSurface ? SurfaceAudioKind.Tar : damagePerTick > 0f ? SurfaceAudioKind.Lava : audioKind;
    public void SetAudioKind(SurfaceAudioKind kind) { audioKind = kind; tarSurface = kind == SurfaceAudioKind.Tar; }
    public bool IsTar => AudioKind == SurfaceAudioKind.Tar;
    public void SetTarSurface(bool value) => tarSurface = value;
    [SerializeField, Min(0f)] float damagePerTick;
    [SerializeField, Min(0f)] float damageGraceTime = 1.25f;
    [SerializeField, Min(0.01f)] float damageInterval = 1f;
    readonly HashSet<PlayerController> contacts = new HashSet<PlayerController>();
    GroundSurfaceDamageGroup damageGroup;
    public void Configure(Modifier[] effects, float damage, float interval = 1f, float graceTime = -1f, GroundSurfaceDamageGroup sharedDamageGroup = null)
    {
        modifiers = effects;
        damagePerTick = Mathf.Max(0f, damage);
        damageInterval = Mathf.Max(0.01f, interval);
        damageGraceTime = graceTime < 0f ? damageInterval : Mathf.Max(0f, graceTime);
        damageGroup = sharedDamageGroup ?? new GroundSurfaceDamageGroup();
    }
    public void Enter(PlayerController player)
    {
        if (!isActiveAndEnabled || player == null || !contacts.Add(player)) return;
        if (damagePerTick > 0f) EnsureDamageGroup().Enter(player, damageGraceTime);
        if (modifiers != null)
            foreach (var modifier in modifiers) player.Stats.AddModifier(this, modifier.stat, modifier.type, modifier.value);
    }
    public void Stay(PlayerController player)
    {
        if (player == null || !contacts.Contains(player) || damagePerTick <= 0f) return;
        EnsureDamageGroup().Stay(player, damagePerTick, damageInterval);
    }
    public void Exit(PlayerController player)
    {
        if (ReferenceEquals(player, null) || !contacts.Remove(player)) return;
        if (damagePerTick > 0f) EnsureDamageGroup().Exit(player);
        if (player != null && player.Stats != null) player.Stats.RemoveModifiers(this);
    }
    GroundSurfaceDamageGroup EnsureDamageGroup() => damageGroup ??= new GroundSurfaceDamageGroup();
    void OnDisable()
    {
        var players = new List<PlayerController>(contacts);
        foreach (var player in players) Exit(player);
        contacts.Clear();
    }
}
