using System.Collections.Generic;
using UnityEngine;

// One active attack (including telegraph) plus a grace period. No singleton.
[RequireComponent(typeof(PlayerController))]
public sealed class EnemyDirector : MonoBehaviour
{
    [SerializeField, Min(0f)] float graceAfterAttack = 1f;
    [SerializeField, Min(0.1f)] float maxAttackDuration = 6f;
    readonly List<EnemyAgent> enemies = new List<EnemyAgent>();
    readonly Queue<EnemyAgent> waiting = new Queue<EnemyAgent>();
    readonly HashSet<EnemyAgent> queued = new HashSet<EnemyAgent>();
    EnemyAgent active;
    float activeUntil, nextAttackTime;
    public PlayerController Player { get; private set; }
    public bool IsThreatActive => active != null;
    public int ActiveEnemies => enemies.Count;
    public IReadOnlyList<EnemyAgent> Enemies => enemies;
    void Awake() => Player = GetComponent<PlayerController>();
    void OnDisable()
    {
        if (active != null) { var interrupted = active; active = null; interrupted.AttackCancelled(); }
        waiting.Clear(); queued.Clear();
    }
    public void Register(EnemyAgent enemy) { if (enemy != null && !enemies.Contains(enemy)) enemies.Add(enemy); }
    public void Unregister(EnemyAgent enemy)
    {
        enemies.Remove(enemy); CancelQueued(enemy);
        if (active == enemy) ReleaseAttack(enemy);
    }
    public void RequestAttack(EnemyAgent enemy)
    {
        if (enemy == null || active == enemy || !enemies.Contains(enemy) || !queued.Add(enemy)) return;
        waiting.Enqueue(enemy);
    }
    public void ReleaseAttack(EnemyAgent enemy)
    {
        CancelQueued(enemy);
        if (active != enemy) return;
        active = null;
        nextAttackTime = Time.time + Mathf.Max(0f, graceAfterAttack);
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
        if (enemy == null || !enemy.isActiveAndEnabled || !enemies.Contains(enemy) || !enemy.CanRequestAttack) return false;
        return ConditionValid(enemy);
    }
    void Update()
    {
        if (Player == null || !Player.enabled)
        {
            if (active != null) { var interrupted = active; active = null; interrupted.AttackCancelled(); }
            waiting.Clear(); queued.Clear(); return;
        }
        if (active != null && (!active.isActiveAndEnabled || !ConditionValid(active) || (active.UsesAttackTimeout && Time.time >= activeUntil)))
        {
            var expired = active;
            ReleaseAttack(expired);
            if (expired != null) expired.AttackCancelled();
        }
        if (active != null || Time.time < nextAttackTime) return;
        while (waiting.Count > 0)
        {
            var enemy = waiting.Dequeue();
            if (enemy == null || !queued.Remove(enemy) || !Eligible(enemy)) continue;
            active = enemy;
            activeUntil = Time.time + Mathf.Max(0.1f, maxAttackDuration);
            enemy.AttackGranted();
            break;
        }
    }
}
