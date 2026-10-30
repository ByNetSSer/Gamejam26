using System;
using UnityEngine;

public sealed class ImpactTarget : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float health = 10f;
    [Tooltip("Velocidad normal mínima para destruir instantáneamente; impactos menores pueden causar daño.")]
    [SerializeField, Min(0f)] float breakSpeed = 12f;
    [SerializeField, Min(0f)] float damagePerSpeed = 0.5f;
    [Tooltip("El daño es vida máxima × velocidad perpendicular / velocidad de rotura.")]
    [SerializeField] bool proportionalSpeedDamage;
    [SerializeField, Min(0f)] float poundBonusDamage = 3f;
    [SerializeField, Min(0.01f)] float impactCooldown = 0.25f;
    [SerializeField] bool hatch;
    [Header("Puntuación")]
    [SerializeField] string scoreReason = "Caja rota";
    [SerializeField, Min(0)] int scorePoints = 1;
    float nextImpactTime;
    float maxHealth;
    bool broken;
    GameObject fallingVisual;
    public void SetFallingVisual(GameObject visual) => fallingVisual = visual;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public event Action<ImpactTarget, float> Damaged;
    void Awake() { maxHealth = Mathf.Max(0.01f, health); }

    float ImpactDamage(PlayerController player, float speed, bool downwardPound)
    {
        float damage = proportionalSpeedDamage
            ? maxHealth * speed / Mathf.Max(0.01f, breakSpeed)
            : speed * damagePerSpeed;
        if (downwardPound) damage += poundBonusDamage;
        return player.Stats.Evaluate(PlayerStat.ImpactDamage, damage);
    }

    // Entry point for a future punch controller. No input or animation is installed here.
    public void ReceivePunch(PlayerController player, float damage)
    {
        if (broken || hatch || player == null || damage <= 0f || float.IsNaN(damage)) return;
        ApplyDamage(player, damage);
    }
    void ApplyDamage(PlayerController player, float damage)
    {
        if (damage <= 0f || float.IsNaN(damage)) return;
        float applied = Mathf.Min(health, damage);
        health = Mathf.Max(0f, health - damage);
        Damaged?.Invoke(this, applied);
        if (health <= 0f) Break(player);
    }
    public bool IsBroken => broken;
    public bool IsHatch => hatch;
    public event Action<ImpactTarget> Broken;

    // Pure prediction: do not apply non-lethal damage before physical contact.
    public bool WillBreak(PlayerController player, Vector3 velocity, Vector3 normal, bool pounding)
    {
        if (broken || player == null) return false;
        float speed = Mathf.Max(0f, -Vector3.Dot(velocity, normal));
        bool downwardPound = pounding && normal.y > 0.5f && speed > 0f;
        if (hatch) return downwardPound;
        if (speed <= 0f) return false;
        if (!proportionalSpeedDamage && speed >= breakSpeed) return true;
        if (Time.time < nextImpactTime) return false;
        return ImpactDamage(player, speed, downwardPound) >= health;
    }

    public void ReceiveImpact(PlayerController player, Vector3 velocity, Vector3 normal, bool pounding)
    {
        if (broken || player == null) return;
        float impactSpeed = Mathf.Max(0f, -Vector3.Dot(velocity, normal));
        bool downwardPound = pounding && normal.y > 0.5f && impactSpeed > 0f;
        if (hatch)
        {
            if (downwardPound) Break(player);
            return;
        }
        if (impactSpeed <= 0f) return;
        if (!proportionalSpeedDamage && impactSpeed >= breakSpeed) { Break(player); return; }
        if (Time.time < nextImpactTime) return;
        nextImpactTime = Time.time + impactCooldown;
        ApplyDamage(player, ImpactDamage(player, impactSpeed, downwardPound));
    }

    void Break(PlayerController player)
    {
        if (broken) return;
        broken = true;
        if (fallingVisual != null)
        {
            fallingVisual.transform.SetParent(null, true);
            var body = fallingVisual.AddComponent<Rigidbody>();
            body.linearVelocity = Vector3.down * 1.5f;
            Destroy(fallingVisual, 4f);
            fallingVisual = null;
        }
        foreach (var collider in GetComponentsInChildren<Collider>()) collider.enabled = false;
        var score = player.GetComponent<PlayerScore>();
        if (score != null && !string.IsNullOrWhiteSpace(scoreReason)) score.Award(scoreReason, scorePoints);
        Broken?.Invoke(this);
        gameObject.SetActive(false);
    }
}
