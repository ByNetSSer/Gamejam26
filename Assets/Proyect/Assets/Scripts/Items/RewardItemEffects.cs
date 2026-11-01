using System;
using UnityEngine;

// Persistent effects purchased from the reward station.
public sealed class RewardItemEffects : MonoBehaviour
{
    PlayerStats stats;
    PlayerHealth health;
    PlayerScore score;
    IDisposable entered, repelled;
    readonly object speedSource = new object();
    int dew, moss, fuseShoes, racingHearts, hunterHearts;
    float racingTimer, nextHunterHeal;
    float regenTimer, phaseTimer;
    bool boost, earnedDuringBoost;

    void Awake()
    {
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        score = GetComponent<PlayerScore>();
        if (score != null) score.Awarded += OnAwarded;
    }
    public void Acquire(string name, RoomManager rooms)
    {
        if (name == "Gota de rocío") dew++;
        else if (name == "Musgo vital") moss++;
        else if (name == "Botas del meteorito")
        {
            stats.AddModifier(new object(), PlayerStat.MoveSpeed, StatModifierType.Percent, 0.30f);
            stats.AddModifier(new object(), PlayerStat.GroundControl, StatModifierType.Percent, -0.20f);
        }
        else if (name == "Corona del temerario")
        {
            stats.AddModifier(new object(), PlayerStat.ScoreAmplification, StatModifierType.Percent, 0.30f);
            stats.AddModifier(new object(), PlayerStat.ComboDuration, StatModifierType.Percent, -0.20f);
        }
        else if (name == "Corazón de carrera")
        {
            racingHearts++;
            stats.AddModifier(new object(), PlayerStat.MoveSpeed, StatModifierType.Percent, 0.30f);
            stats.AddModifier(new object(), PlayerStat.Acceleration, StatModifierType.Percent, 0.15f);
        }
        else if (name == "Corazón de cazador")
        {
            hunterHearts++;
            var repel = GetComponent<PlayerRepel>();
            if (repelled == null && repel != null) repelled = repel.Events.Subscribe<EnemyRepelledEvent>(OnRepelled);
        }
        else if (name == "Zapatillas de mecha")
        {
            fuseShoes++;
            if (entered == null) entered = rooms.Events.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        }
    }
    void OnRepelled(EnemyRepelledEvent hit)
    {
        if (!(hit.Enemy is Flupin) || health == null || Time.time < nextHunterHeal || health.CurrentHealth <= 0f) return;
        health.Heal(health.MaxHealth * 0.05f * hunterHearts);
        nextHunterHeal = Time.time + 10f;
    }
    void OnAwarded(ScoreAward award) { if (boost && award.Points > 0) earnedDuringBoost = true; }
    void OnRoomEntered(RoomEnteredEvent e)
    {
        stats.RemoveModifiers(speedSource);
        boost = true;
        earnedDuringBoost = false;
        phaseTimer = 3.5f;
        stats.AddModifier(speedSource, PlayerStat.MoveSpeed, StatModifierType.Percent, 0.35f * fuseShoes);
    }
    void Update()
    {
        float dt = Time.deltaTime;
        if (health != null && dew + moss > 0 && health.CurrentHealth > 0f)
        {
            regenTimer += dt;
            while (regenTimer >= 10f)
            {
                regenTimer -= 10f;
                health.Heal(health.MaxHealth * (0.01f * dew + 0.02f * moss));
            }
        }
        if (health != null && racingHearts > 0 && health.CurrentHealth > 0f)
        {
            racingTimer += dt;
            while (racingTimer >= 3f && health.CurrentHealth > 0f)
            {
                racingTimer -= 3f;
                health.TakeDamage(health.MaxHealth * 0.01f * racingHearts);
            }
        }
        if (phaseTimer <= 0f) return;
        phaseTimer -= dt;
        if (phaseTimer > 0f) return;
        stats.RemoveModifiers(speedSource);
        if (boost)
        {
            boost = false;
            if (!earnedDuringBoost && (score == null || score.ComboCount == 0))
            {
                stats.AddModifier(speedSource, PlayerStat.MoveSpeed, StatModifierType.Percent, -0.15f * fuseShoes);
                phaseTimer = 2.5f;
            }
        }
    }
    void OnDestroy()
    {
        entered?.Dispose();
        repelled?.Dispose();
        if (score != null) score.Awarded -= OnAwarded;
        if (stats != null) stats.RemoveModifiers(speedSource);
    }
}
