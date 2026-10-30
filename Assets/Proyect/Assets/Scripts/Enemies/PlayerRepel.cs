using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public readonly struct EnemyRepelAvailabilityChangedEvent
{
    public readonly PlayerController Player;
    public readonly EnemyAgent Enemy;
    public readonly bool Available;
    public EnemyRepelAvailabilityChangedEvent(PlayerController player, EnemyAgent enemy, bool available)
    { Player = player; Enemy = enemy; Available = available; }
}
public readonly struct EnemyRepelledEvent
{
    public readonly PlayerController Player;
    public readonly EnemyAgent Enemy;
    public readonly Vector3 Position;
    public readonly float Power;
    public EnemyRepelledEvent(PlayerController player, EnemyAgent enemy, Vector3 position, float power)
    { Player = player; Enemy = enemy; Position = position; Power = power; }
}

// Read the repel input before enemies advance into contact during this frame.
[DefaultExecutionOrder(-5)]
[RequireComponent(typeof(EnemyDirector))]
public sealed class PlayerRepel : MonoBehaviour
{
    [SerializeField, Min(0f)] float radius = 4.5f;
    [SerializeField, Min(0f)] float cooldown = 0.65f;
    [SerializeField, Min(0f)] float repelPower = 1f;
    [Header("Golpe a destructibles")]
    [SerializeField, Min(0f)] float propHitRange = 3f;
    [SerializeField, Min(0f)] float propHitDamage = 20f;
    [Tooltip("Daño adicional por unidad de velocidad hacia el objetivo.")]
    [SerializeField, Min(0f)] float propDamagePerForwardSpeed = 0.6f;
    [SerializeField] LayerMask propHitLayers = ~0;
    EnemyDirector director;
    float nextUse;
    readonly HashSet<EnemyAgent> available = new HashSet<EnemyAgent>();
    EventStream events;
    // Listeners subscribe to the player's own stream and dispose subscriptions when disabled.
    public EventStream Events => events ?? (events = new EventStream(Debug.LogException));
    void Awake() { director = GetComponent<EnemyDirector>(); if (GetComponent<ParryFeedback>() == null) gameObject.AddComponent<ParryFeedback>(); if (GetComponent<ParryAccents>() == null) gameObject.AddComponent<ParryAccents>(); }
    void OnDisable() => ClearAvailability();
    void OnDestroy() => events?.Clear();
    void Update()
    {
        if (director == null || director.Player == null || !director.Player.enabled || Time.deltaTime <= 0f)
        { ClearAvailability(); return; }
        RefreshAvailability();
        if (Time.time < nextUse || Mouse.current == null || !Mouse.current.rightButton.wasPressedThisFrame || Cursor.lockState != CursorLockMode.Locked) return;
        nextUse = Time.time + Mathf.Max(0f, cooldown);
        HitDestructible();
        var snapshot = new List<EnemyAgent>(director.Enemies);
        foreach (var enemy in snapshot)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || !enemy.CanBeRepelled ||
                (enemy.transform.position - transform.position).sqrMagnitude > radius * radius) continue;
            Vector3 impactPosition = enemy.transform.position;
            if (enemy.TryRepel(repelPower))
            {
                GameSfx.PlayAt("parry.success", impactPosition);
                Events.Publish(new EnemyRepelledEvent(director.Player, enemy, impactPosition, repelPower));
            }
        }
        RefreshAvailability();
    }
    void HitDestructible()
    {
        var view = director.Player.GetComponentInChildren<Camera>();
        if (view == null || propHitRange <= 0f || propHitDamage <= 0f) return;
        var ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out var hit, propHitRange, propHitLayers, QueryTriggerInteraction.Ignore)) return;
        var target = hit.collider.GetComponentInParent<ImpactTarget>();
        if (target != null)
        {
            float forwardSpeed = Mathf.Max(0f, Vector3.Dot(director.Player.Velocity, ray.direction));
            target.ReceivePunch(director.Player, propHitDamage + forwardSpeed * propDamagePerForwardSpeed, ray.direction);
        }
    }
    void LateUpdate() => RefreshAvailability();
    void RefreshAvailability()
    {
        var next = new HashSet<EnemyAgent>();
        if (director != null && director.Player != null && director.Player.enabled && Time.deltaTime > 0f &&
            Time.time >= nextUse && Mouse.current != null)
        {
            foreach (var enemy in new List<EnemyAgent>(director.Enemies))
                if (enemy != null && enemy.isActiveAndEnabled && enemy.CanBeRepelled &&
                    (enemy.transform.position - transform.position).sqrMagnitude <= radius * radius)
                    next.Add(enemy);
        }
        var previous = new List<EnemyAgent>(available);
        available.Clear();
        available.UnionWith(next);
        foreach (var enemy in previous)
            if (enemy != null && !next.Contains(enemy))
                Events.Publish(new EnemyRepelAvailabilityChangedEvent(director != null ? director.Player : null, enemy, false));
        foreach (var enemy in next)
            if (enemy != null && !previous.Contains(enemy))
            {
                if (enemy is Flupin) GameSfx.PlayFollowing("flupin.available", enemy.transform);
                Events.Publish(new EnemyRepelAvailabilityChangedEvent(director.Player, enemy, true));
            }
    }
    void ClearAvailability()
    {
        var previous = new List<EnemyAgent>(available);
        available.Clear();
        foreach (var enemy in previous)
            if (enemy != null) Events.Publish(new EnemyRepelAvailabilityChangedEvent(director != null ? director.Player : null, enemy, false));
    }
}

