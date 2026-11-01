using System;
using System.Collections.Generic;
using UnityEngine;

public enum CrusherStat { Aggressiveness, DescentSpeed, CurrentHealthFraction, ReactionMargin }

// Experimental encounter: its box and both end positions must fit inside one straight room.
public sealed class RoomCrusher : EnemyAgent
{
    [SerializeField] Transform top;
    [SerializeField] Transform bottom;
    [SerializeField] Transform safePoint;
    [SerializeField] Transform visual;
    [SerializeField] Vector3 size = new Vector3(6f, 1f, 10f);
    [SerializeField, Min(0f)] float descentSpeed = 1.5f;
    [SerializeField, Min(0f)] float reactionMargin = 2f;
    [SerializeField, Range(0.1f, 1f)] float speedAllowance = 0.7f;
    [SerializeField, Range(0f, 1f)] float currentHealthFraction = 0.5f;
    [SerializeField, Min(0.1f)] float aggressiveness = 1f;
    [SerializeField, Min(1f)] float maxEstimatedSeconds = 20f;
    readonly Dictionary<CrusherStat, StatValue> modifiers = new Dictionary<CrusherStat, StatValue>();
    IDisposable roomExit, roomEnter;
    RoomDefinition room;
    CharacterController playerBody;
    Vector3 previousPlayer;
    float warning;
    SpawnScaleFeedback appearance;
    bool granted, descending, finished, roomActive;
    public string DebugStatus => finished ? "Terminado" : !granted ? "Esperando turno" : !descending ? $"Aviso: {warning:0.0} s" : "Bajando";
    public override AttackRequirement Requirement => AttackRequirement.Any;
    public override bool CanRequestAttack => (appearance == null || !appearance.IsAppearing) && roomActive && Player != null && Player.IsGrounded && !finished && !granted && ValidSetup();
    public override bool CanBeRepelled => false;
    public override bool TryGetEasyParryPoint(Vector3 playerPosition, out Vector3 point)
    {
        point = visual != null ? visual.TransformPoint(new Vector3(Mathf.Clamp(visual.InverseTransformPoint(playerPosition).x, -0.5f, 0.5f), Mathf.Clamp(visual.InverseTransformPoint(playerPosition).y, -0.5f, 0.5f), Mathf.Clamp(visual.InverseTransformPoint(playerPosition).z, -0.5f, 0.5f))) : transform.position;
        return isActiveAndEnabled && granted && descending && !finished && visual != null;
    }

    public override bool UsesAttackTimeout => false;
    public override bool TryRepel(float power) => false;
    public StatValue GetStat(CrusherStat stat)
    {
        if (!modifiers.TryGetValue(stat, out var value)) modifiers.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, CrusherStat stat, StatModifierType type, float value) => GetStat(stat).Add(source, type, value);
    public void RemoveModifiers(object source) { if (source != null) foreach (var stat in modifiers.Values) stat.RemoveSource(source); }
    float Value(CrusherStat stat, float basis) => GetStat(stat).Calculate(basis);
    float Aggressiveness => Mathf.Max(0.1f, Value(CrusherStat.Aggressiveness, aggressiveness));
    public void Configure(Transform start, Transform end, Transform safe, Transform shape, Vector3 dimensions)
    { top = start; bottom = end; safePoint = safe; visual = shape; size = dimensions; }
    bool ValidSetup() => top != null && bottom != null && safePoint != null && visual != null && size.x > 0f && size.y > 0f && size.z > 0f &&
        Vector3.Dot(top.up, Vector3.up) > 0.999f && Vector3.Dot(bottom.up, Vector3.up) > 0.999f &&
        Vector3.Distance(top.position, bottom.position) > 0.01f &&
        Vector3.ProjectOnPlane(bottom.position - top.position, Vector3.up).sqrMagnitude < 0.0001f;
    protected override void Start()
    {
        base.Start();
        room = GetComponentInParent<RoomDefinition>();
        var manager = GetComponentInParent<RoomManager>();
        if (manager != null && manager.Player != null) SetDirector(manager.Player.GetComponent<EnemyDirector>());
        roomActive = room == null || manager == null || manager.CurrentRoom == room && !TowerSafetyRules.IsProtectedRoom(manager.RoomsEntered);
        if (room != null && manager != null)
        {
            roomEnter = manager.Events.Subscribe<RoomEnteredEvent>(e => { if (e.Context.Room == room && !TowerSafetyRules.IsProtectedRoom(e.Context.Index)) roomActive = true; });
            roomExit = manager.Events.Subscribe<RoomExitedEvent>(e => { if (e.Context.Room == room) Finish(); });
        }
        if (!ValidSetup()) { Debug.LogWarning("Aplanadora de prueba: revisa marcadores, dimensiones y descenso vertical.", this); finished = true; return; }
        visual.SetPositionAndRotation(top.position, top.rotation);
        visual.localScale = size;
        appearance = SpawnScaleFeedback.Play(visual);
    }
    void Update()
    {
        if (finished || Player == null || !Player.enabled || Time.deltaTime <= 0f) return;
        if (!granted) { if (CanRequestAttack) RequestAttack(); return; }
        if (playerBody == null) { Finish(); return; }
        if (!descending)
        {
            warning = Mathf.Max(0f, warning - Time.deltaTime);
            previousPlayer = playerBody.bounds.center;
            if (warning <= 0f) descending = true;
            return;
        }
        Vector3 oldPosition = visual.position;
        float speed = Value(CrusherStat.DescentSpeed, descentSpeed) * Aggressiveness;
        visual.position = Vector3.MoveTowards(oldPosition, bottom.position, speed * Time.deltaTime);
        Vector3 center = playerBody.bounds.center;
        if (SweptContact(oldPosition, visual.position, previousPlayer, center, playerBody.bounds.extents))
        {
            var health = Player.GetComponent<PlayerHealth>();
            float damage = health != null ? health.DamageForCurrentHealthFraction(Value(CrusherStat.CurrentHealthFraction, currentHealthFraction)) : 0f;
            Finish();
            if (health != null) health.TakeDamage(damage, RunLossReason.Crusher);
            return;
        }
        previousPlayer = center;
        if (Vector3.Distance(visual.position, bottom.position) < 0.001f) Finish();
    }
    public override void AttackGranted()
    {
        if (!ValidSetup() || Player == null) { Finish(); return; }
        playerBody = Player.GetComponent<CharacterController>();
        Vector3 delta = Vector3.ProjectOnPlane(safePoint.position - Player.transform.position, Vector3.up);
        float distance = delta.magnitude;
        float initial = distance > 0.001f ? Vector3.Dot(Player.Velocity, delta / distance) : 0f;
        float time = EstimateTravelTime(distance, initial, Player.TargetSpeed * Mathf.Clamp(speedAllowance, 0.1f, 1f), Player.GroundAcceleration);
        if (playerBody == null || float.IsInfinity(time) || time > maxEstimatedSeconds || Value(CrusherStat.DescentSpeed, descentSpeed) <= 0f)
        { Debug.LogWarning("Aplanadora de prueba: recorrido o velocidad no permiten calcular un aviso; encuentro cancelado.", this); Finish(); return; }
        warning = time + Value(CrusherStat.ReactionMargin, reactionMargin);
        previousPlayer = playerBody.bounds.center;
        granted = true;
    }
    // Includes velocity away from safety and acceleration to the limited target speed.
    public static float EstimateTravelTime(float distance, float initialSpeed, float targetSpeed, float acceleration)
    {
        if (distance <= 0f) return 0f;
        if (targetSpeed <= 0.001f || acceleration <= 0.001f) return float.PositiveInfinity;
        float start = Mathf.Min(initialSpeed, targetSpeed);
        float accelerateTime = (targetSpeed - start) / acceleration;
        float acceleratedDistance = start * accelerateTime + 0.5f * acceleration * accelerateTime * accelerateTime;
        if (distance <= acceleratedDistance)
            return (-start + Mathf.Sqrt(start * start + 2f * acceleration * distance)) / acceleration;
        return accelerateTime + (distance - acceleratedDistance) / targetSpeed;
    }
    bool SweptContact(Vector3 before, Vector3 after, Vector3 playerBefore, Vector3 playerAfter, Vector3 worldExtents)
    {
        Quaternion inverse = Quaternion.Inverse(visual.rotation);
        Vector3 x = inverse * new Vector3(worldExtents.x, 0f, 0f);
        Vector3 y = inverse * new Vector3(0f, worldExtents.y, 0f);
        Vector3 z = inverse * new Vector3(0f, 0f, worldExtents.z);
        Vector3 extents = size * 0.5f + new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x), Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
        Vector3 start = inverse * (playerBefore - before);
        Vector3 end = inverse * (playerAfter - after);
        float min = 0f, max = 1f;
        for (int axis = 0; axis < 3; axis++)
        {
            float d = end[axis] - start[axis];
            if (Mathf.Abs(d) < 0.000001f) { if (Mathf.Abs(start[axis]) > extents[axis]) return false; continue; }
            float a = (-extents[axis] - start[axis]) / d, b = (extents[axis] - start[axis]) / d;
            if (a > b) { float swap = a; a = b; b = swap; }
            min = Mathf.Max(min, a); max = Mathf.Min(max, b);
            if (min > max) return false;
        }
        return true;
    }
    public override void AttackCancelled() => Finish();
    void Finish() { if (finished) return; finished = true; granted = false; ReleaseAttack(); if (visual != null) visual.gameObject.SetActive(false); }
    protected override void OnDisable() { roomExit?.Dispose(); roomEnter?.Dispose(); roomExit = roomEnter = null; base.OnDisable(); }
    void OnDrawGizmos()
    {
        if (top == null || bottom == null) return;
        Gizmos.color = Color.red;
        Gizmos.matrix = Matrix4x4.TRS(top.position, top.rotation, Vector3.one); Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.TRS(bottom.position, bottom.rotation, Vector3.one); Gizmos.DrawWireCube(Vector3.zero, size);
        Gizmos.matrix = Matrix4x4.identity; Gizmos.DrawLine(top.position, bottom.position);
        if (safePoint != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(safePoint.position, 0.4f); }
    }
    void OnGUI() { if (Application.isPlaying && !finished) GUI.Label(new Rect(20, 70, 400, 30), "Aplanadora experimental: " + DebugStatus); }
}
