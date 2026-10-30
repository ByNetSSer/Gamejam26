using UnityEngine;
using System.Collections.Generic;

public enum DontseeEndReason { RoomExited, HitPlayer, Cancelled, Disabled }
public enum DontseeStat { Aggressiveness, ApproachSpeed, CurrentHealthFraction, WarningDuration, ContactRadius }

// Scene references are valid during dispatch; Position and room index are snapshots.
public readonly struct DontseeSpawnedEvent
{
    public readonly Dontsee Enemy; public readonly PlayerController Player; public readonly RoomContext Room; public readonly Vector3 Position;
    public DontseeSpawnedEvent(Dontsee enemy, PlayerController player, RoomContext room, Vector3 position)
    { Enemy = enemy; Player = player; Room = room; Position = position; }
}
public readonly struct DontseeActivatedEvent
{
    public readonly Dontsee Enemy; public readonly PlayerController Player; public readonly RoomContext Room; public readonly Vector3 Position;
    public DontseeActivatedEvent(Dontsee enemy, PlayerController player, RoomContext room, Vector3 position)
    { Enemy = enemy; Player = player; Room = room; Position = position; }
}
public readonly struct DontseeGazeChangedEvent
{
    public readonly Dontsee Enemy; public readonly PlayerController Player; public readonly RoomContext Room; public readonly Vector3 Position;
    public readonly bool Watched; public readonly bool IsInitial;
    public DontseeGazeChangedEvent(Dontsee enemy, PlayerController player, RoomContext room, Vector3 position, bool watched, bool isInitial)
    { Enemy = enemy; Player = player; Room = room; Position = position; Watched = watched; IsInitial = isInitial; }
}
public readonly struct DontseeApproachChangedEvent
{
    public readonly Dontsee Enemy; public readonly PlayerController Player; public readonly RoomContext Room; public readonly Vector3 Position; public readonly bool Approaching;
    public DontseeApproachChangedEvent(Dontsee enemy, PlayerController player, RoomContext room, Vector3 position, bool approaching)
    { Enemy = enemy; Player = player; Room = room; Position = position; Approaching = approaching; }
}
public readonly struct DontseeHitEvent
{
    public readonly Dontsee Enemy; public readonly PlayerController Player; public readonly RoomContext Room; public readonly Vector3 Position; public readonly float Damage;
    public DontseeHitEvent(Dontsee enemy, PlayerController player, RoomContext room, Vector3 position, float damage)
    { Enemy = enemy; Player = player; Room = room; Position = position; Damage = damage; }
}
public readonly struct DontseeEndedEvent
{
    public readonly Dontsee Enemy; public readonly PlayerController Player; public readonly RoomContext Room; public readonly Vector3 Position; public readonly DontseeEndReason Reason;
    public DontseeEndedEvent(Dontsee enemy, PlayerController player, RoomContext room, Vector3 position, DontseeEndReason reason)
    { Enemy = enemy; Player = player; Room = room; Position = position; Reason = reason; }
}

// One encounter owns the director's turn until contact, cancellation or room exit.
public sealed class Dontsee : EnemyAgent
{
    [Header("Agresividad")]
    [SerializeField, Min(0.1f)] float aggressiveness = 1f;
    [SerializeField, Min(0f)] float warningDuration = 1.5f;
    [SerializeField, Min(0f)] float approachSpeed = 7f;
    [SerializeField, Min(0.1f)] float contactRadius = 0.7f;
    [SerializeField, Range(0f, 1f)] float currentHealthFraction = 0.33f;
    [Tooltip("Capas que impiden considerar que se mira a Dontsee. Nothing ignora paredes y objetos.")]
    [SerializeField] LayerMask sightObstacles = 0;
    [Header("Apariencia")]
    [SerializeField] Material stalkingMaterial;
    [SerializeField] Material watchedMaterial;
    Renderer visual;
    Camera view;
    bool attacking;
    bool ended, hasGaze, lastWatched, approaching;
    GameEventBus eventBus;
    System.IDisposable roomExited;
    RoomContext room;
    float warningRemaining;
    Vector3 previousPlayer;
    readonly Dictionary<DontseeStat, StatValue> modifiers = new Dictionary<DontseeStat, StatValue>();
    public StatValue GetStat(DontseeStat stat)
    {
        if (!modifiers.TryGetValue(stat, out var value)) modifiers.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, DontseeStat stat, StatModifierType type, float value) => GetStat(stat).Add(source, type, value);
    public void RemoveModifiers(object source)
    {
        if (source == null) return;
        foreach (var value in modifiers.Values) value.RemoveSource(source);
    }
    public float Evaluate(DontseeStat stat, float baseValue) => GetStat(stat).Calculate(baseValue);
    public float Aggressiveness => Mathf.Max(0.1f, Evaluate(DontseeStat.Aggressiveness, aggressiveness));
    public override AttackRequirement Requirement => AttackRequirement.Any;
    public override bool CanRequestAttack => !attacking;
    public override bool CanBeRepelled => false;
    public override bool UsesAttackTimeout => false;
    public override bool TryRepel(float power) => false;
    void Awake() { visual = GetComponentInChildren<Renderer>(); if (visual != null) visual.enabled = false; }
    public void Initialize(GameEventBus bus, RoomContext context)
    {
        roomExited?.Dispose();
        eventBus = bus; room = context;
        if (eventBus != null)
            roomExited = eventBus.Subscribe<RoomExitedEvent>(e =>
            {
                if (e.Context.Room == room.Room) Finish(DontseeEndReason.RoomExited);
            });
    }
    void Update()
    {
        if (Player == null || !Player.enabled) return;
        if (!attacking) { RequestAttack(); return; }
        if (view == null) view = Player.GetComponentInChildren<Camera>();
        if (view != null) transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
        Vector3 center = Player.transform.position + Vector3.up * 0.5f;
        Vector3 before = transform.position;
        bool watched = IsWatched();
        if (!hasGaze || watched != lastWatched)
        {
            bool initial = !hasGaze;
            hasGaze = true; lastWatched = watched;
            eventBus?.Publish(new DontseeGazeChangedEvent(this, Player, room, transform.position, watched, initial));
            if (!isActiveAndEnabled || ended) return;
        }
        Material desiredMaterial = watched ? watchedMaterial : stalkingMaterial;
        if (visual != null && desiredMaterial != null && visual.sharedMaterial != desiredMaterial)
            visual.sharedMaterial = desiredMaterial;
        bool shouldApproach = warningRemaining <= 0f && !watched;
        if (shouldApproach != approaching)
        {
            approaching = shouldApproach;
            eventBus?.Publish(new DontseeApproachChangedEvent(this, Player, room, transform.position, approaching));
            if (!isActiveAndEnabled || ended) return;
        }
        if (warningRemaining > 0f) warningRemaining -= Time.deltaTime * Aggressiveness;
        else if (shouldApproach && Time.deltaTime > 0f)
            transform.position = Vector3.MoveTowards(before, center, Evaluate(DontseeStat.ApproachSpeed, approachSpeed) * Aggressiveness * Time.deltaTime);
        if (Flupin.TouchesPlayer(before, transform.position, previousPlayer, center, Evaluate(DontseeStat.ContactRadius, contactRadius)))
        {
            var health = Player.GetComponent<PlayerHealth>();
            float damage = health != null ? health.CurrentHealth * Mathf.Clamp01(Evaluate(DontseeStat.CurrentHealthFraction, currentHealthFraction)) : 0f;
            eventBus?.Publish(new DontseeHitEvent(this, Player, room, transform.position, damage));
            Finish(DontseeEndReason.HitPlayer);
            if (health != null) health.TakeDamage(damage);
            return;
        }
        previousPlayer = center;
    }
    bool IsWatched()
    {
        if (view == null || visual == null) return false;
        Vector3 target = visual.bounds.center;
        Vector3 screen = view.WorldToViewportPoint(target);
        if (screen.z <= 0f || screen.x < 0f || screen.x > 1f || screen.y < 0f || screen.y > 1f) return false;
        Vector3 direction = target - view.transform.position;
        var hits = Physics.RaycastAll(view.transform.position, direction.normalized, direction.magnitude, sightObstacles, QueryTriggerInteraction.Ignore);
        foreach (var hit in hits)
        {
            if (hit.transform.IsChildOf(Player.transform) || hit.transform.IsChildOf(transform)) continue;
            return false;
        }
        return true;
    }
    public override void AttackGranted()
    {
        if (Player == null) { ReleaseAttack(); return; }
        attacking = true;
        previousPlayer = Player.transform.position + Vector3.up * 0.5f;
        warningRemaining = Evaluate(DontseeStat.WarningDuration, warningDuration);
        if (visual != null) visual.enabled = true;
        eventBus?.Publish(new DontseeActivatedEvent(this, Player, room, transform.position));
    }
    public override void AttackCancelled()
    {
        Finish(DontseeEndReason.Cancelled);
    }
    public void Finish(DontseeEndReason reason)
    {
        if (ended) return;
        ended = true; attacking = false;
        eventBus?.Publish(new DontseeEndedEvent(this, Player, room, transform.position, reason));
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
    protected override void OnDisable()
    {
        roomExited?.Dispose(); roomExited = null;
        if (!ended && eventBus != null)
        {
            ended = true;
            eventBus.Publish(new DontseeEndedEvent(this, Player, room, transform.position, DontseeEndReason.Disabled));
        }
        base.OnDisable();
    }
}
