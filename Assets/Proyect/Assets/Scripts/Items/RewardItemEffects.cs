using System;
using UnityEngine;

// Persistent effects purchased from the reward station.
public sealed class RewardItemEffects : MonoBehaviour
{
    PlayerStats stats;
    PlayerHealth health;
    PlayerScore score;
    IDisposable entered;
    readonly object speedSource = new object();
    int dew, moss, fuseShoes;
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
        else if (name == "Zapatillas de mecha")
        {
            fuseShoes++;
            if (entered == null) entered = rooms.Events.Subscribe<RoomEnteredEvent>(OnRoomEntered);
        }
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
        if (score != null) score.Awarded -= OnAwarded;
        if (stats != null) stats.RemoveModifiers(speedSource);
    }
}
