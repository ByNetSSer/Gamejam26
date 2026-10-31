using System.Collections.Generic;
using UnityEngine;

// Normally one attack at a time. Occasional mask events reserve the director
// until their participants finish, without interrupting an attack already underway.
[RequireComponent(typeof(PlayerController))]
public sealed class EnemyDirector : MonoBehaviour
{
    [SerializeField, Min(0f)] float graceAfterAttack = 1f;
    [SerializeField, Min(0.1f)] float maxAttackDuration = 6f;
    [Header("Eventos de máscaras")]
    [SerializeField, Range(0f, 1f)] float maskPairChance = 0.2f;
    [SerializeField, Range(0f, 1f)] float maskBurstChance = 0.05f;
    [SerializeField, Min(3)] int maskBurstMaxParticipants = 5;
    [SerializeField, Min(0.1f)] float maskBurstInterval = 0.6f;
    readonly List<EnemyAgent> enemies = new List<EnemyAgent>();
    readonly Queue<EnemyAgent> waiting = new Queue<EnemyAgent>();
    readonly HashSet<EnemyAgent> queued = new HashSet<EnemyAgent>();
    readonly Dictionary<EnemyAgent, float> active = new Dictionary<EnemyAgent, float>();
    readonly List<EnemyAgent> activeSnapshot = new List<EnemyAgent>();
    readonly Queue<EnemyAgent> burstPending = new Queue<EnemyAgent>();
    bool maskEvent;
    float nextAttackTime, nextBurstTime;
    public PlayerController Player { get; private set; }
    public bool IsThreatActive => active.Count > 0 || maskEvent;
    public int ActiveEnemies => enemies.Count;
    public IReadOnlyList<EnemyAgent> Enemies => enemies;
    void Awake() => Player = GetComponent<PlayerController>();
    void OnDisable() => CancelAll();
    void CancelAll()
    {
        maskEvent = false;
        burstPending.Clear();
        activeSnapshot.Clear();
        activeSnapshot.AddRange(active.Keys);
        active.Clear();
        foreach (var enemy in activeSnapshot) if (enemy != null) enemy.AttackCancelled();
        waiting.Clear(); queued.Clear();
    }
    public void Register(EnemyAgent enemy) { if (enemy != null && !enemies.Contains(enemy)) enemies.Add(enemy); }
    public void Unregister(EnemyAgent enemy)
    {
        enemies.Remove(enemy); CancelQueued(enemy);
        if (active.ContainsKey(enemy)) ReleaseAttack(enemy);
    }
    public void RequestAttack(EnemyAgent enemy)
    {
        if (enemy == null || active.ContainsKey(enemy) || burstPending.Contains(enemy) || !enemies.Contains(enemy) || !queued.Add(enemy)) return;
        waiting.Enqueue(enemy);
    }
    public void ReleaseAttack(EnemyAgent enemy)
    {
        CancelQueued(enemy);
        if (enemy == null || !active.Remove(enemy)) return;
        if (!maskEvent && active.Count == 0) nextAttackTime = Time.time + Mathf.Max(0f, graceAfterAttack);
    }
    void CancelQueued(EnemyAgent enemy)
    {
        if (!queued.Remove(enemy)) return;
        int count = waiting.Count;
        for (int i = 0; i < count; i++)
        {
            var item = waiting.Dequeue();
            if (item != enemy) waiting.Enqueue(item);
        }
    }
    bool ConditionValid(EnemyAgent enemy)
    {
        return enemy.Requirement == AttackRequirement.Any ||
            (enemy.Requirement == AttackRequirement.PlayerGrounded && Player.IsGrounded) ||
            (enemy.Requirement == AttackRequirement.PlayerAirborne && !Player.IsGrounded);
    }
    bool Eligible(EnemyAgent enemy)
    {
        return enemy != null && enemy.isActiveAndEnabled && enemies.Contains(enemy) &&
            enemy.CanRequestAttack && ConditionValid(enemy);
    }
    void Grant(EnemyAgent enemy)
    {
        active[enemy] = Time.time + Mathf.Max(0.1f, maxAttackDuration);
        enemy.AttackGranted();
    }
    void Update()
    {
        if (Player == null || !Player.enabled) { CancelAll(); return; }
        activeSnapshot.Clear();
        activeSnapshot.AddRange(active.Keys);
        foreach (var enemy in activeSnapshot)
        {
            if (!active.TryGetValue(enemy, out float until)) continue;
            if (enemy == null || !enemy.isActiveAndEnabled || !ConditionValid(enemy) || (enemy.UsesAttackTimeout && Time.time >= until))
            {
                active.Remove(enemy);
                if (enemy != null) enemy.AttackCancelled();
                if (!maskEvent && active.Count == 0) nextAttackTime = Time.time + Mathf.Max(0f, graceAfterAttack);
            }
        }
        if (maskEvent)
        {
            while (burstPending.Count > 0 && active.Count < 2 && Time.time >= nextBurstTime)
            {
                var enemy = burstPending.Dequeue();
                if (!Eligible(enemy)) continue;
                Grant(enemy);
                nextBurstTime = Time.time + Mathf.Max(0.1f, maskBurstInterval);
            }
            if (burstPending.Count == 0 && active.Count == 0)
            {
                maskEvent = false;
                nextAttackTime = Time.time + Mathf.Max(0f, graceAfterAttack);
            }
            return;
        }
        if (active.Count > 0 || Time.time < nextAttackTime) return;
        while (waiting.Count > 0)
        {
            var enemy = waiting.Dequeue();
            if (enemy == null || !queued.Remove(enemy) || !Eligible(enemy)) continue;
            if (enemy is MaskCharger && TryStartMaskEvent(enemy)) return;
            Grant(enemy);
            break;
        }
    }
    bool TryStartMaskEvent(EnemyAgent first)
    {
        var candidates = new List<EnemyAgent>();
        foreach (var enemy in waiting)
            if (enemy is MaskCharger && queued.Contains(enemy) && Eligible(enemy)) candidates.Add(enemy);
        if (candidates.Count == 0) return false;
        float roll = Random.value;
        float burstChance = candidates.Count >= 2 ? Mathf.Clamp01(maskBurstChance) : 0f;
        bool burst = roll < burstChance;
        bool pair = !burst && roll < burstChance + Mathf.Clamp01(maskPairChance);
        if (!burst && !pair) return false;
        maskEvent = true;
        int count = burst ? Mathf.Min(candidates.Count, Mathf.Max(3, maskBurstMaxParticipants) - 1) : 1;
        for (int i = 0; i < count; i++)
        {
            CancelQueued(candidates[i]);
            burstPending.Enqueue(candidates[i]);
        }
        Grant(first);
        if (pair)
        {
            var partner = burstPending.Dequeue();
            if (Eligible(partner)) Grant(partner);
        }
        nextBurstTime = Time.time + Mathf.Max(0.1f, maskBurstInterval);
        return true;
    }
}
