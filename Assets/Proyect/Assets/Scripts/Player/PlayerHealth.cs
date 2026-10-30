using System;
using UnityEngine;

[RequireComponent(typeof(PlayerEvents))]
public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1f)] float maxHealth = 10f;
    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public event Action Died;
    PlayerEvents events;
    void Awake() { CurrentHealth = maxHealth; events = GetComponent<PlayerEvents>(); }
    public bool TakeDamage(float amount)
    {
        if (amount <= 0f || CurrentHealth <= 0f) return false;
        float actual = Mathf.Min(CurrentHealth, amount);
        CurrentHealth -= actual;
        events.NotifyDamage(actual);
        if (CurrentHealth <= 0f) Died?.Invoke();
        return true;
    }
}
