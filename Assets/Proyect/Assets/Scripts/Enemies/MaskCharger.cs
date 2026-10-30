using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;

public enum MaskChargerState { Following, Telegraph, Charging, Recovery, Braking }
public enum MaskChargerStat { Aggressiveness, FollowSpeed, CatchUpBonus, AttackRange, AttackInterval, TelegraphDuration, ChargeSpeed, ChargeDuration, ContactRadius, CurrentHealthFraction, RecoveryDuration, BrakingDuration }

public readonly struct MaskChargerStateChangedEvent
{
    public readonly MaskCharger Enemy;
    public readonly MaskChargerState State;
    public readonly Vector3 Position;
    public MaskChargerStateChangedEvent(MaskCharger enemy, MaskChargerState state, Vector3 position)
    { Enemy = enemy; State = state; Position = position; }
}
public readonly struct MaskChargerHitEvent
{
    public readonly MaskCharger Enemy;
    public readonly PlayerController Player;
    public readonly Vector3 Position;
    public readonly float Damage;
    public MaskChargerHitEvent(MaskCharger enemy, PlayerController player, Vector3 position, float damage)
    { Enemy = enemy; Player = player; Position = position; Damage = damage; }
}

// Persistent world-space pursuer. Charge direction is sampled once at launch.
[DefaultExecutionOrder(50)]
public sealed class MaskCharger : EnemyAgent
{
    [Header("Agresividad")]
    [SerializeField, Min(0.1f)] float aggressiveness = 1f;
    [Header("Seguimiento")]
    [SerializeField, Min(0f)] float followSpeed = 3f;
    [SerializeField, Min(0f)] float catchUpBonus = 4f;
    [SerializeField, Min(0.1f)] float attackRange = 10f;
    [SerializeField, Min(0f)] float preferredDistance = 8f;
    [Tooltip("Velocidad angular máxima al rondar y recuperar una posición adelantada.")]
    [SerializeField, Min(0f)] float orbitDegreesPerSecond = 70f;
    [Tooltip("Ángulo máximo a cada lado de la dirección de avance del jugador.")]
    [SerializeField, Range(0f, 85f)] float stalkingArc = 65f;
    [SerializeField, Min(0f)] float attackInterval = 4f;
    [Header("Embestida")]
    [SerializeField, Min(0f)] float telegraphDuration = 1f;
    [SerializeField, Min(0.1f)] float chargeSpeed = 16f;
    [SerializeField, Min(0.1f)] float chargeDuration = 1.2f;
    [SerializeField, Min(0.1f)] float contactRadius = 0.8f;
    [SerializeField, Range(0f, 1f)] float currentHealthFraction = 0.33f;
    [SerializeField, Min(0f)] float recoveryDuration = 1.5f;
    [SerializeField, Min(0f)] float overshootDistance = 4f;
    [Tooltip("Tiempo de frenada al finalizar la carrera. El recorrido extra aparece en la línea de aviso.")]
    [SerializeField, Min(0.01f)] float brakingDuration = 0.35f;
    [Header("Línea de aviso")]
    [SerializeField, Min(0.01f)] float lineWidth = 0.08f;
    [SerializeField] Color lineColor = new Color(1f, 0.2f, 0.1f, 1f);
    [Header("FEEL opcional")]
    [SerializeField] MMF_Player warningFeedback;
    [SerializeField] MMF_Player chargeFeedback;
    [SerializeField] MMF_Player brakingFeedback;
    [SerializeField] MMF_Player hitFeedback;
    readonly Dictionary<MaskChargerStat, StatValue> modifiers = new Dictionary<MaskChargerStat, StatValue>();
    EventStream events;
    MaskChargerState state;
    float aimSoundAt;
    bool aimSoundPlayed;
    float timer, chargeRemaining, lockedChargeSpeed, lockedBrakingDuration, brakingElapsed, orbitPhase;
    float orbitSide = 1f;
    Vector3 chargeDirection, previousPlayer;
    bool hasPrevious, hitPlayer;
    SphereCollider damageTrigger;
    LineRenderer trajectory;
    Material lineMaterial;
    Collider[] modelColliders;
    bool[] colliderStates;
    Rigidbody[] bodies;
    bool[] bodyKinematic, bodyGravity;
    public EventStream Events => events ?? (events = new EventStream(Debug.LogException));
    public MaskChargerState State => state;
    public Vector3 ChargeDirection => chargeDirection;
    public float Aggressiveness => Mathf.Max(0.1f, Evaluate(MaskChargerStat.Aggressiveness, aggressiveness));
    public override AttackRequirement Requirement => AttackRequirement.Any;
    public override bool CanRequestAttack => state == MaskChargerState.Following && timer <= 0f && Player != null &&
        (PlayerCenter() - transform.position).sqrMagnitude <= Mathf.Pow(Evaluate(MaskChargerStat.AttackRange, attackRange), 2f) &&
        IsAhead(PlayerCenter());
    public override bool CanBeRepelled => false;
    // The distance is fixed at launch, so a long charge must retain its turn.
    public override bool UsesAttackTimeout => false;
    public override bool TryRepel(float power) => false;
    public StatValue GetStat(MaskChargerStat stat)
    {
        if (!modifiers.TryGetValue(stat, out var value)) modifiers.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, MaskChargerStat stat, StatModifierType type, float value) => GetStat(stat).Add(source, type, value);
    public void RemoveModifiers(object source) { if (source != null) foreach (var value in modifiers.Values) value.RemoveSource(source); }
    public float Evaluate(MaskChargerStat stat, float value) => GetStat(stat).Calculate(value);
    protected override void OnEnable()
    {
        base.OnEnable();
        PrepareCollision();
        state = MaskChargerState.Following;
        timer = Evaluate(MaskChargerStat.AttackInterval, attackInterval);
        hasPrevious = false;
        orbitPhase = 0f;
        orbitSide = 1f;
    }
    protected override void OnDisable()
    {
        if (trajectory != null) trajectory.enabled = false;
        if (damageTrigger != null) damageTrigger.enabled = false;
        if (modelColliders != null) for (int i = 0; i < modelColliders.Length; i++)
            if (modelColliders[i] != null) modelColliders[i].enabled = colliderStates[i];
        if (bodies != null) for (int i = 0; i < bodies.Length; i++)
            if (bodies[i] != null) { bodies[i].isKinematic = bodyKinematic[i]; bodies[i].useGravity = bodyGravity[i]; }
        base.OnDisable();
    }
    void OnDestroy() { events?.Clear(); if (lineMaterial != null) Destroy(lineMaterial); }
    Vector3 PlayerCenter() => Player.transform.position + Vector3.up * 0.5f;
    void Update()
    {
        if (Player == null || !Player.enabled || Time.deltaTime <= 0f) { hasPrevious = false; return; }
        if (transform.IsChildOf(Player.transform)) transform.SetParent(null, true);
        float dt = Time.deltaTime;
        Vector3 center = PlayerCenter();
        if (!hasPrevious) { previousPlayer = center; hasPrevious = true; }
        switch (state)
        {
            case MaskChargerState.Following:
                Follow(center, dt);
                timer = Mathf.Max(0f, timer - dt * Aggressiveness);
                if (CanRequestAttack) RequestAttack();
                break;
            case MaskChargerState.Telegraph:
                timer -= dt;
                if (!aimSoundPlayed && timer <= aimSoundAt)
                {
                    aimSoundPlayed = true;
                    GameSfx.PlayFollowing("mask.aim", transform);
                }
                if (timer <= 0f)
                {
                    if (trajectory != null) trajectory.enabled = false;
                    if (damageTrigger != null) damageTrigger.enabled = true;
                    SetState(MaskChargerState.Charging);
                    if (chargeFeedback != null) chargeFeedback.PlayFeedbacks(transform.position);
                }
                break;
            case MaskChargerState.Charging:
                Vector3 before = transform.position;
                float fullSpeedTime = Mathf.Min(dt, chargeRemaining / lockedChargeSpeed);
                Vector3 playerAtBrake = Vector3.Lerp(previousPlayer, center, fullSpeedTime / dt);
                float travel = lockedChargeSpeed * fullSpeedTime;
                transform.position += chargeDirection * travel;
                chargeRemaining -= travel;
                if (!hitPlayer && Flupin.TouchesPlayer(before, transform.position, previousPlayer, playerAtBrake, Evaluate(MaskChargerStat.ContactRadius, contactRadius))) HitPlayer();
                if (state == MaskChargerState.Charging && chargeRemaining <= 0.0001f)
                {
                    brakingElapsed = 0f;
                    SetState(MaskChargerState.Braking);
                    if (brakingFeedback != null) brakingFeedback.PlayFeedbacks(transform.position);
                    if (state == MaskChargerState.Braking) Brake(dt - fullSpeedTime, playerAtBrake, center);
                }
                break;
            case MaskChargerState.Braking:
                Brake(dt, previousPlayer, center);
                break;
            case MaskChargerState.Recovery:
                timer -= dt;
                if (timer <= 0f)
                {
                    timer = Evaluate(MaskChargerStat.AttackInterval, attackInterval);
                    SetState(MaskChargerState.Following);
                }
                break;
        }
        previousPlayer = center;
    }
    void Brake(float dt, Vector3 playerBefore, Vector3 playerAfter)
    {
        float beforeTime = brakingElapsed;
        float afterTime = Mathf.Min(lockedBrakingDuration, beforeTime + Mathf.Max(0f, dt));
        // Integrate linear deceleration exactly, including frames that cross the end.
        float travel = lockedChargeSpeed * ((afterTime - beforeTime) -
            (afterTime * afterTime - beforeTime * beforeTime) / (2f * lockedBrakingDuration));
        Vector3 before = transform.position;
        transform.position += chargeDirection * Mathf.Max(0f, travel);
        brakingElapsed = afterTime;
        if (!hitPlayer && Flupin.TouchesPlayer(before, transform.position, playerBefore, playerAfter, Evaluate(MaskChargerStat.ContactRadius, contactRadius))) HitPlayer();
        if (state == MaskChargerState.Braking && brakingElapsed >= lockedBrakingDuration) Recover();
    }
    void Follow(Vector3 center, float dt)
    {
        Vector3 forward = TravelForward();
        Vector3 offset = Vector3.ProjectOnPlane(transform.position - center, Vector3.up);
        Vector3 radial = offset.sqrMagnitude > 0.001f ? offset.normalized : -forward;
        float radius = Mathf.Min(Mathf.Max(0.5f, preferredDistance), Mathf.Max(0.5f, Evaluate(MaskChargerStat.AttackRange, attackRange) * 0.8f));
        orbitPhase += dt * Mathf.Max(0f, orbitDegreesPerSecond) * Mathf.Deg2Rad * 0.35f;
        float angle = orbitSide * Mathf.Clamp(stalkingArc, 0f, 85f) * (0.65f + 0.35f * Mathf.Sin(orbitPhase));
        Vector3 desiredRadial = Quaternion.AngleAxis(angle, Vector3.up) * forward;
        float signedAngle = Vector3.SignedAngle(radial, desiredRadial, Vector3.up);
        // Resolve the ambiguous directly-behind case consistently to the chosen side.
        if (Mathf.Abs(signedAngle) > 179f) signedAngle = 180f * orbitSide;
        float turn = Mathf.Clamp(signedAngle, -orbitDegreesPerSecond * dt, orbitDegreesPerSecond * dt);
        Vector3 nextRadial = Quaternion.AngleAxis(turn, Vector3.up) * radial;
        Vector3 target = center + nextRadial * radius;
        float playerSpeed = Vector3.ProjectOnPlane(Player.Velocity, Vector3.up).magnitude;
        float speed = Mathf.Max(0f, Evaluate(MaskChargerStat.FollowSpeed, followSpeed)) + playerSpeed;
        if (offset.magnitude > radius * 1.15f || Vector3.Dot(radial, forward) < 0f)
            speed += Mathf.Max(0f, Evaluate(MaskChargerStat.CatchUpBonus, catchUpBonus));
        transform.position = Vector3.MoveTowards(transform.position, target, speed * dt);
        Face(center - transform.position);
    }
    Vector3 TravelForward()
    {
        Vector3 forward = Vector3.ProjectOnPlane(Player.Velocity, Vector3.up);
        if (forward.sqrMagnitude < 0.25f) forward = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up);
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }
    bool IsAhead(Vector3 center)
    {
        Vector3 offset = Vector3.ProjectOnPlane(transform.position - center, Vector3.up);
        return offset.sqrMagnitude > 0.01f && Vector3.Dot(offset.normalized, TravelForward()) > 0.1f;
    }
    void Face(Vector3 direction) { if (direction.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up); }
    void PrepareCollision()
    {
        var found = new List<Collider>();
        foreach (var collider in GetComponentsInChildren<Collider>(true))
            if (collider != damageTrigger) found.Add(collider);
        modelColliders = found.ToArray(); colliderStates = new bool[modelColliders.Length];
        for (int i = 0; i < modelColliders.Length; i++) { colliderStates[i] = modelColliders[i].enabled; modelColliders[i].enabled = false; }
        bodies = GetComponentsInChildren<Rigidbody>(true);
        bodyKinematic = new bool[bodies.Length]; bodyGravity = new bool[bodies.Length];
        for (int i = 0; i < bodies.Length; i++)
        {
            bodyKinematic[i] = bodies[i].isKinematic; bodyGravity[i] = bodies[i].useGravity;
            bodies[i].isKinematic = true; bodies[i].useGravity = false;
        }
        if (damageTrigger == null) damageTrigger = gameObject.AddComponent<SphereCollider>();
        damageTrigger.isTrigger = true;
        damageTrigger.enabled = false;
    }
    void ShowTrajectory(float length)
    {
        if (trajectory == null)
        {
            var go = new GameObject("Trayectoria de embestida");
            go.transform.SetParent(transform, false);
            trajectory = go.AddComponent<LineRenderer>();
            trajectory.useWorldSpace = true; trajectory.positionCount = 2;
            trajectory.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trajectory.receiveShadows = false;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader != null) { lineMaterial = new Material(shader); trajectory.sharedMaterial = lineMaterial; }
        }
        if (lineMaterial != null && lineMaterial.HasProperty("_BaseColor")) lineMaterial.SetColor("_BaseColor", lineColor);
        trajectory.startColor = trajectory.endColor = lineColor;
        trajectory.startWidth = trajectory.endWidth = Mathf.Max(0.01f, lineWidth);
        trajectory.SetPosition(0, transform.position);
        trajectory.SetPosition(1, transform.position + chargeDirection * length);
        trajectory.enabled = true;
    }
    void OnTriggerEnter(Collider other)
    {
        if ((state == MaskChargerState.Charging || state == MaskChargerState.Braking) && !hitPlayer && Player != null && other.transform.IsChildOf(Player.transform)) HitPlayer();
    }
    void HitPlayer()
    {
        if (hitPlayer || Player == null || !Player.enabled) return;
        hitPlayer = true;
        var health = Player.GetComponent<PlayerHealth>();
        float damage = health != null ? health.DamageForCurrentHealthFraction(Evaluate(MaskChargerStat.CurrentHealthFraction, currentHealthFraction)) : 0f;
        Events.Publish(new MaskChargerHitEvent(this, Player, transform.position, damage));
        if (hitFeedback != null) hitFeedback.PlayFeedbacks(transform.position);
        if (health != null) health.TakeDamage(damage, RunLossReason.MaskCharger);
    }
    public override void AttackGranted()
    {
        if (!CanRequestAttack) { ReleaseAttack(); return; }
        Vector3 target = PlayerCenter() - transform.position;
        chargeDirection = target.sqrMagnitude > 0.0001f ? target.normalized : transform.forward;
        Face(chargeDirection);
        lockedChargeSpeed = Mathf.Max(0.1f, Evaluate(MaskChargerStat.ChargeSpeed, chargeSpeed) * Aggressiveness);
        chargeRemaining = Mathf.Max(target.magnitude + Mathf.Max(0f, overshootDistance), lockedChargeSpeed * Mathf.Max(0.1f, Evaluate(MaskChargerStat.ChargeDuration, chargeDuration)));
        lockedBrakingDuration = Mathf.Max(0.01f, Evaluate(MaskChargerStat.BrakingDuration, brakingDuration));
        hitPlayer = false;
        if (damageTrigger != null)
        {
            Vector3 scale = transform.lossyScale;
            float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            damageTrigger.radius = Evaluate(MaskChargerStat.ContactRadius, contactRadius) / Mathf.Max(0.0001f, maxScale);
        }
        ShowTrajectory(chargeRemaining + 0.5f * lockedChargeSpeed * lockedBrakingDuration);
        timer = Evaluate(MaskChargerStat.TelegraphDuration, telegraphDuration);
        SetState(MaskChargerState.Telegraph);
        if (warningFeedback != null) warningFeedback.PlayFeedbacks(transform.position);
    }
    public override void AttackCancelled() { if (state == MaskChargerState.Telegraph || state == MaskChargerState.Charging || state == MaskChargerState.Braking) Recover(); }
    void Recover()
    {
        if (trajectory != null) trajectory.enabled = false;
        if (damageTrigger != null) damageTrigger.enabled = false;
        ReleaseAttack();
        orbitSide = -orbitSide;
        timer = Evaluate(MaskChargerStat.RecoveryDuration, recoveryDuration);
        SetState(MaskChargerState.Recovery);
    }
    void SetState(MaskChargerState next)
    {
        if (state == next) return;
        state = next;
        if (next == MaskChargerState.Telegraph)
        {
            aimSoundAt = timer * 0.5f;
            aimSoundPlayed = false;
            GameSfx.PlayFollowing("mask.start", transform);
        }
        if (next == MaskChargerState.Charging) GameSfx.PlayFollowing("mask.charge", transform);
        Events.Publish(new MaskChargerStateChangedEvent(this, next, transform.position));
    }
}
