using System;
using UnityEngine;

public enum ComboEndReason { Expired, Damage }

[RequireComponent(typeof(PlayerStats))]
public sealed class PlayerScore : MonoBehaviour
{
    [SerializeField, Min(1)] int pointsPerCoin = 10;
    [SerializeField, Min(0.01f)] float baseComboDuration = 4f;
    [SerializeField, Min(0f)] float baseMultiplierGrowth = 0.1f;
    [SerializeField, Min(0f)] float baseScoreAmplification = 1f;
    PlayerStats stats;
    readonly ComboState combo = new ComboState();
    public long TotalPoints { get; private set; }
    public long ComboPoints { get; private set; }
    public long Coins { get; private set; }
    public event Action<long> CoinsChanged;
    public event Action<long, long> ComboPaid;
    public int ComboCount => combo.Count;
    public float ComboDuration => stats.Evaluate(PlayerStat.ComboDuration, baseComboDuration);
    public float MultiplierGrowth => stats.Evaluate(PlayerStat.ComboMultiplierGrowth, baseMultiplierGrowth);
    public float ScoreAmplification => stats.Evaluate(PlayerStat.ScoreAmplification, baseScoreAmplification);
    public float ComboWindowDuration => combo.WindowDuration;
    public float ComboRemaining => combo.Count == 0 ? 0f : Mathf.Max(0f, combo.ExpiresAt - Time.time);
    public event Action<ScoreAward> Awarded;
    public event Action ComboEnded;
    public ComboEndReason LastComboEndReason { get; private set; }
    public void FinishComboOnDamage()
    {
        if (combo.Expire(Time.time)) { FinishCombo(); return; }
        if (combo.Count == 0) return;
        FinishCombo(ComboEndReason.Damage);
    }
    void Awake() { stats = GetComponent<PlayerStats>(); if (GetComponent<ComboHud>() == null) gameObject.AddComponent<ComboHud>(); }
    void Update() { if (combo.Expire(Time.time)) FinishCombo(); }
    void FinishCombo(ComboEndReason reason = ComboEndReason.Expired)
    {
        combo.Reset();
        LastComboEndReason = reason;
        long payout = ComboPoints / Math.Max(1, pointsPerCoin);
        Coins = long.MaxValue - Coins < payout ? long.MaxValue : Coins + payout;
        long points = ComboPoints; ComboPoints = 0;
        CoinsChanged?.Invoke(Coins); ComboPaid?.Invoke(points, payout); ComboEnded?.Invoke();
    }
    public bool TrySpend(long amount)
    {
        if (amount < 0 || Coins < amount) return false;
        Coins -= amount; CoinsChanged?.Invoke(Coins); return true;
    }

    public ScoreAward Award(string reason, int basePoints)
    {
        if (stats == null) stats = GetComponent<PlayerStats>();
        if (combo.Expire(Time.time)) FinishCombo();
        var award = combo.Register(reason, basePoints, Time.time,
            stats.Evaluate(PlayerStat.ComboDuration, baseComboDuration),
            stats.Evaluate(PlayerStat.ComboMultiplierGrowth, baseMultiplierGrowth),
            stats.Evaluate(PlayerStat.ScoreAmplification, baseScoreAmplification));
        TotalPoints = long.MaxValue - TotalPoints < award.Points ? long.MaxValue : TotalPoints + award.Points;
        ComboPoints = long.MaxValue - ComboPoints < award.Points ? long.MaxValue : ComboPoints + award.Points;
        Awarded?.Invoke(award);
        return award;
    }
    public void ResetScore() { TotalPoints = 0; ComboPoints = 0; Coins = 0; combo.Reset(); CoinsChanged?.Invoke(Coins); }
}
