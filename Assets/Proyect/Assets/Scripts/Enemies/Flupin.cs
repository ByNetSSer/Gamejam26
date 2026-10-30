using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Serialization;

public enum FlupinState { Waiting, Approaching, KnockedAway, Returning }
public enum FlupinStat
{
    ApproachStartDistance, InitialDelay, RadialApproachSpeed, MaxTangentialSpeed, OrbitDegreesPerSecond,
    OrbitCorrection, FlightAcceleration, VerticalWobble, VerticalCorrection, TimeBetweenAttacks,
    RecoveryDuration, ContactRadius, CurrentHealthFraction, RepelSpeed, HitRecoilSpeed, TravelDistance,
    HitTravelMultiplier, KnockAttraction, MaxKnockTime, ReturnSpeed, Aggressiveness
}

// World-space steering: inertia and tangential motion survive transitions between phases.
[DefaultExecutionOrder(50)]
public sealed class Flupin : EnemyAgent
{
    [Header("Agresividad")]
    [Tooltip("1 = normal. Aumenta la velocidad de acercamiento y reduce las esperas antes de atacar.")]
    [SerializeField, Min(0.1f)] float aggressiveness = 1f;
    [Header("Acercamiento en espiral")]
    [SerializeField, Min(1f)] float approachStartDistance = 12f;
    [SerializeField, Min(0f)] float initialDelay = 0.8f;
    [SerializeField, Min(0.1f)] float radialApproachSpeed = 4f;
    [SerializeField, Min(0f)] float maxTangentialSpeed = 8f;
    [SerializeField, Min(0f)] float orbitDegreesPerSecond = 70f;
    [SerializeField, Min(0f)] float orbitCorrection = 3f;
    [SerializeField, Min(0f)] float flightAcceleration = 24f;
    [SerializeField, Min(0f)] float verticalWobble = 0.4f;
    [SerializeField, Min(0f)] float verticalCorrection = 3f;
    [SerializeField, Min(0.1f)] float timeBetweenAttacks = 4f;
    [FormerlySerializedAs("warningDuration")]
    [SerializeField, Min(0.1f)] float recoveryDuration = 1.2f;
    [Header("Ataque y expulsiÃƒÂ³n")]
    [SerializeField, Min(0.1f)] float contactRadius = 0.7f;
    [SerializeField, Range(0f, 1f)] float currentHealthFraction = 1f / 3f;
    [SerializeField, Min(0f)] float repelSpeed = 8f;
    [SerializeField, Min(0f)] float hitRecoilSpeed = 14f;
    [SerializeField, Min(0.1f)] float travelDistance = 8f;
    [SerializeField, Min(1f)] float hitTravelMultiplier = 1.5f;
    [SerializeField, Min(0f)] float knockAttraction = 0.8f;
    [SerializeField, Min(0.1f)] float maxKnockTime = 4f;
    [SerializeField, Min(0.1f)] float returnSpeed = 3f;
    [Header("FEEL opcional")]
    [SerializeField] MMF_Player warningFeedback;
    [SerializeField] MMF_Player repelFeedback;
    [SerializeField] MMF_Player hitFeedback;
    public override AttackRequirement Requirement => AttackRequirement.Any;
    public override bool CanRequestAttack => state == FlupinState.Waiting;
    public FlupinState State => state;
    public float Aggressiveness => Mathf.Max(0.1f, Evaluate(FlupinStat.Aggressiveness, aggressiveness));
    public float SpawnDistance => Mathf.Max(1f, Evaluate(FlupinStat.ApproachStartDistance, approachStartDistance));
    public override bool CanBeRepelled => Player != null && state != FlupinState.KnockedAway && state != FlupinState.Returning;
    readonly Dictionary<FlupinStat, StatValue> modifiers = new Dictionary<FlupinStat, StatValue>();
    public StatValue GetStat(FlupinStat stat)
    {
        if (!modifiers.TryGetValue(stat, out var value)) modifiers.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, FlupinStat stat, StatModifierType type, float value) => GetStat(stat).Add(source, type, value);
    public void RemoveModifiers(object source)
    {
        if (source == null) return;
        foreach (var value in modifiers.Values) value.RemoveSource(source);
    }
    public float Evaluate(FlupinStat stat, float baseValue) => GetStat(stat).Calculate(baseValue);
    public event Action<FlupinState> StateChanged;

    FlupinState state;
    float timer, orbitPhase, targetDistance, launchRadius, knockElapsed;
    Vector3 flightVelocity, previousCenter, centerVelocity;
    bool hasCenter, placedAtDistance;
    static readonly Vector3 CenterOffset = new Vector3(0f, 0.5f, 0f);

    protected override void OnEnable()
    {
        base.OnEnable();
        timer = Mathf.Max(0f, Evaluate(FlupinStat.InitialDelay, initialDelay));
        state = FlupinState.Waiting;
        flightVelocity = Vector3.zero;
        hasCenter = false;
        placedAtDistance = false;
    }
    void Update()
    {
        if (Player == null || !Player.enabled || Time.deltaTime <= 0f) { hasCenter = false; return; }
        // Existing scenes may have Flupin under the player. Keep the registered director,
        // but do not inherit translations or camera yaw after the game starts.
        if (transform.IsChildOf(Player.transform)) transform.SetParent(null, true);
        float dt = Time.deltaTime;
        Vector3 center = Player.transform.position + CenterOffset;
        if (!hasCenter) { previousCenter = center; hasCenter = true; }
        if (!placedAtDistance)
        {
            Vector3 initial = Vector3.ProjectOnPlane(transform.position - center, Vector3.up);
            if (initial.sqrMagnitude < 0.01f) initial = Vector3.ProjectOnPlane(Player.transform.forward, Vector3.up);
            if (initial.sqrMagnitude < 0.01f) initial = Vector3.forward;
            if ((transform.position - center).magnitude < Evaluate(FlupinStat.ApproachStartDistance, approachStartDistance))
                transform.position = center + initial.normalized * Mathf.Max(1f, Evaluate(FlupinStat.ApproachStartDistance, approachStartDistance));
            placedAtDistance = true;
        }
        centerVelocity = (center - previousCenter) / dt;
        Vector3 beforePosition = transform.position;
        // During orbit and pursuit, inherit translation only; keep steering velocity
        // relative to the player so abrupt acceleration cannot leave Flupin behind.
        if (state != FlupinState.KnockedAway) transform.position += center - previousCenter;

        orbitPhase += Evaluate(FlupinStat.OrbitDegreesPerSecond, orbitDegreesPerSecond) * Mathf.Deg2Rad * dt;
        switch (state)
        {
            case FlupinState.Waiting:
                StayDistant(center, dt);
                timer -= dt * Aggressiveness;
                if (timer <= 0f) { RequestAttack(); timer = 0.2f; }
                break;
            case FlupinState.Approaching:
                SteerSpiral(center, dt);
                if (TouchesPlayer(beforePosition, transform.position, previousCenter, center, Evaluate(FlupinStat.ContactRadius, contactRadius))) HitPlayer();
                break;
            case FlupinState.KnockedAway:
                Vector3 towards = center - transform.position;
                if (towards.sqrMagnitude > 0.0001f) flightVelocity += towards.normalized * Evaluate(FlupinStat.KnockAttraction, knockAttraction) * dt;
                Vector3 awayHorizontal = Vector3.ProjectOnPlane(-towards, Vector3.up);
                if (awayHorizontal.sqrMagnitude > 0.0001f)
                {
                    Vector3 tangent = Vector3.Cross(Vector3.up, awayHorizontal.normalized);
                    float desiredTangent = Mathf.Min(Evaluate(FlupinStat.MaxTangentialSpeed, maxTangentialSpeed),
                        awayHorizontal.magnitude * Evaluate(FlupinStat.OrbitDegreesPerSecond, orbitDegreesPerSecond) * Mathf.Deg2Rad);
                    float existingTangent = Vector3.Dot(flightVelocity - centerVelocity, tangent);
                    flightVelocity += tangent * Mathf.Clamp(desiredTangent - existingTangent,
                        -Evaluate(FlupinStat.FlightAcceleration, flightAcceleration) * dt, Evaluate(FlupinStat.FlightAcceleration, flightAcceleration) * dt);
                }
                transform.position += flightVelocity * dt;
                knockElapsed += dt;
                if ((transform.position - center).magnitude - launchRadius >= targetDistance || knockElapsed >= Evaluate(FlupinStat.MaxKnockTime, maxKnockTime))
                { timer = Mathf.Max(0.1f, Evaluate(FlupinStat.RecoveryDuration, recoveryDuration)); flightVelocity -= centerVelocity; SetState(FlupinState.Returning); }
                break;
            case FlupinState.Returning:
                StayDistant(center, dt);
                timer -= dt * Aggressiveness;
                if (timer <= 0f) { timer = Mathf.Max(0.1f, Evaluate(FlupinStat.TimeBetweenAttacks, timeBetweenAttacks)); SetState(FlupinState.Waiting); }
                break;
        }
        previousCenter = center;
    }
    void StayDistant(Vector3 center, float dt)
    {
        Vector3 offset = transform.position - center;
        Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
        Vector3 radial = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.forward;
        float error = horizontal.magnitude - Mathf.Max(1f, Evaluate(FlupinStat.ApproachStartDistance, approachStartDistance));
        float speed = Mathf.Clamp(-error * Evaluate(FlupinStat.OrbitCorrection, orbitCorrection), -Mathf.Max(0.1f, Evaluate(FlupinStat.ReturnSpeed, returnSpeed)), Mathf.Max(0.1f, Evaluate(FlupinStat.ReturnSpeed, returnSpeed)));
        float vertical = Mathf.Clamp((Mathf.Sin(orbitPhase * 2f) * Evaluate(FlupinStat.VerticalWobble, verticalWobble) - offset.y) * Evaluate(FlupinStat.VerticalCorrection, verticalCorrection), -Evaluate(FlupinStat.ReturnSpeed, returnSpeed), Evaluate(FlupinStat.ReturnSpeed, returnSpeed));
        float tangential = Mathf.Min(Evaluate(FlupinStat.MaxTangentialSpeed, maxTangentialSpeed),
            Mathf.Min(horizontal.magnitude * Evaluate(FlupinStat.OrbitDegreesPerSecond, orbitDegreesPerSecond) * Mathf.Deg2Rad,
                Evaluate(FlupinStat.ReturnSpeed, returnSpeed)));
        Vector3 desired = radial * speed + Vector3.Cross(Vector3.up, radial) * tangential + Vector3.up * vertical;
        flightVelocity = Vector3.MoveTowards(flightVelocity, desired, Mathf.Max(0f, Evaluate(FlupinStat.FlightAcceleration, flightAcceleration)) * dt);
        transform.position += flightVelocity * dt;
    }
    void SteerSpiral(Vector3 center, float dt)
    {
        Vector3 offset = transform.position - center;
        float inward = Mathf.Max(0.1f, Evaluate(FlupinStat.RadialApproachSpeed, radialApproachSpeed) * Aggressiveness);
        Vector3 desired = SpiralRelativeVelocity(offset, inward, Evaluate(FlupinStat.OrbitDegreesPerSecond, orbitDegreesPerSecond), Evaluate(FlupinStat.MaxTangentialSpeed, maxTangentialSpeed),
            Mathf.Sin(orbitPhase * 2f) * Evaluate(FlupinStat.VerticalWobble, verticalWobble), Evaluate(FlupinStat.VerticalCorrection, verticalCorrection));
        flightVelocity = Vector3.MoveTowards(flightVelocity, desired, Mathf.Max(0f, Evaluate(FlupinStat.FlightAcceleration, flightAcceleration)) * dt);
        transform.position += flightVelocity * dt;
    }
    // Radial convergence plus tangential movement forms a shrinking spiral around the player.
    public static Vector3 SpiralRelativeVelocity(Vector3 offset, float inwardSpeed, float angularDegrees,
        float maxTangential, float desiredHeight, float heightCorrection)
    {
        Vector3 horizontal = Vector3.ProjectOnPlane(offset, Vector3.up);
        Vector3 radial = horizontal.sqrMagnitude > 0.0001f ? horizontal.normalized : Vector3.forward;
        Vector3 tangent = Vector3.Cross(Vector3.up, radial);
        float inward = Mathf.Max(0f, inwardSpeed);
        float tangential = Mathf.Min(Mathf.Max(0f, maxTangential), horizontal.magnitude * Mathf.Max(0f, angularDegrees) * Mathf.Deg2Rad);
        float vertical = Mathf.Clamp((desiredHeight - offset.y) * Mathf.Max(0f, heightCorrection), -inward, inward);
        return -radial * inward + tangent * tangential + Vector3.up * vertical;
    }
    // Relative swept segment catches a fast crossing even if neither endpoint is inside the radius.
    public static bool TouchesPlayer(Vector3 before, Vector3 after, Vector3 centerBefore, Vector3 centerAfter, float radius)
    {
        Vector3 start = before - centerBefore;
        Vector3 delta = (after - centerAfter) - start;
        float t = delta.sqrMagnitude > 0.000001f ? Mathf.Clamp01(-Vector3.Dot(start, delta) / delta.sqrMagnitude) : 0f;
        return (start + delta * t).sqrMagnitude <= Mathf.Max(0f, radius) * Mathf.Max(0f, radius);
    }
    public override void AttackGranted()
    {
        if (state != FlupinState.Waiting) { ReleaseAttack(); return; }
        SetState(FlupinState.Approaching);
        if (warningFeedback != null) warningFeedback.PlayFeedbacks(transform.position);
    }
    public override void AttackCancelled()
    {
        if (state != FlupinState.Approaching) return;
        timer = Mathf.Max(0.1f, Evaluate(FlupinStat.RecoveryDuration, recoveryDuration));
        SetState(FlupinState.Returning);
    }
    public override bool TryRepel(float power)
    {
        if (!CanBeRepelled) return false;
        if (repelFeedback != null) repelFeedback.PlayFeedbacks(transform.position);
        var score = Player.GetComponent<PlayerScore>();
        if (score != null) score.Award("Flupin repelido", 1);
        ThrowAway(Evaluate(FlupinStat.RepelSpeed, repelSpeed) * Mathf.Max(0f, power), 1f);
        return true;
    }
    void HitPlayer()
    {
        var health = Player.GetComponent<PlayerHealth>();
        if (health != null) health.TakeDamage(health.DamageForCurrentHealthFraction(Evaluate(FlupinStat.CurrentHealthFraction, currentHealthFraction)), RunLossReason.Flupin);
        if (hitFeedback != null) hitFeedback.PlayFeedbacks(transform.position);
        ThrowAway(Evaluate(FlupinStat.HitRecoilSpeed, hitRecoilSpeed), Mathf.Max(1f, Evaluate(FlupinStat.HitTravelMultiplier, hitTravelMultiplier)));
    }
    void ThrowAway(float speed, float distanceMultiplier)
    {
        ReleaseAttack();
        Vector3 center = Player.transform.position + CenterOffset;
        Vector3 direction = transform.position - center;
        if (direction.sqrMagnitude < 0.001f) direction = -Player.transform.forward;
        direction.Normalize();
        flightVelocity = RepelledVelocity(flightVelocity + centerVelocity, centerVelocity, direction, speed,
            Evaluate(FlupinStat.MaxTangentialSpeed, maxTangentialSpeed));
        launchRadius = (transform.position - center).magnitude;
        knockElapsed = 0f;
        targetDistance = Mathf.Max(0.1f, Evaluate(FlupinStat.TravelDistance, travelDistance) * distanceMultiplier);
        SetState(FlupinState.KnockedAway);
    }
    // Keep the tangential component when changing the radial component to an outward impulse.
    public static Vector3 RepelledVelocity(Vector3 velocity, Vector3 centerVelocity, Vector3 direction, float speed, float maxTangential)
    {
        direction = direction.normalized;
        Vector3 tangent = Vector3.Cross(Vector3.up, direction).normalized;
        Vector3 relative = velocity - centerVelocity;
        relative += direction * (Mathf.Max(0.1f, speed) - Vector3.Dot(relative, direction));
        float minimumTangent = Mathf.Min(Mathf.Max(0f, maxTangential), Mathf.Max(0f, speed) * 0.25f);
        relative += tangent * Mathf.Max(0f, minimumTangent - Vector3.Dot(relative, tangent));
        return centerVelocity + relative;
    }
    void SetState(FlupinState next)
    {
        if (state == next) return;
        state = next;
        StateChanged?.Invoke(next);
    }
}
