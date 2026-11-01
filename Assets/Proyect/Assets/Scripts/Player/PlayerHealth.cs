using System;
using UnityEngine;

[RequireComponent(typeof(PlayerEvents))]
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] float maxHealth = 10f;
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public event Action Died;
    public RunLossReason LastDamageReason { get; private set; }
    PlayerEvents events;
    float enemyParryProtectionUntil;
    public bool EnemyParryProtected => Time.time < enemyParryProtectionUntil;
    public void GrantEnemyParryProtection(float seconds)
    {
        if (MenuUIBase.Core.RunDifficulty.Selected != MenuUIBase.Core.GameDifficulty.Easy || seconds <= 0f) return;
        enemyParryProtectionUntil = Mathf.Max(enemyParryProtectionUntil, Time.time + seconds);
    }
    static bool IsEnemyDamage(RunLossReason reason) => reason == RunLossReason.Flupin || reason == RunLossReason.Dontsee ||
        reason == RunLossReason.MaskCharger || reason == RunLossReason.Crusher || reason == RunLossReason.CorridorChaser || reason == RunLossReason.Guilt;

    void Awake()
    {
        CurrentHealth = maxHealth;
        events = GetComponent<PlayerEvents>();
        if (GetComponent<PlayerDamageFeedback>() == null) gameObject.AddComponent<PlayerDamageFeedback>();
    }
    // Fractional enemy attacks retain their percentage at high health but can finish a run.
    public float DamageForCurrentHealthFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        return fraction <= 0f || CurrentHealth <= 0f ? 0f : Mathf.Min(CurrentHealth, Mathf.Max(1f, CurrentHealth * fraction));
    }
    public void Heal(float amount)
    {
        if (amount > 0f && CurrentHealth > 0f) CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
    }
    public bool TakeDamage(float amount, RunLossReason reason = RunLossReason.PlayerHealthDepleted)
    {
        if (amount <= 0f || CurrentHealth <= 0f || (EnemyParryProtected && IsEnemyDamage(reason))) return false;
        float actual = Mathf.Min(CurrentHealth, amount);
        LastDamageReason = reason;
        CurrentHealth -= actual;
        GetComponent<PlayerScore>()?.FinishComboOnDamage();
        GameSfx.Play("player.damage", 0.12f);
        try { events.NotifyDamage(actual); }
        finally { if (CurrentHealth <= 0f) Died?.Invoke(); }
        return true;
    }
}
