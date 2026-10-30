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
    [Header("Feedback de destrucción")]
    [SerializeField] bool destructionFeedback = true;
    [SerializeField] bool paperFragments;
    [Tooltip("Opcional: sustituye el efecto provisional. Se instancia fuera del objeto destruido.")]
    [SerializeField] ParticleSystem destructionParticles;
    [SerializeField, Min(0.1f)] float feedbackLifetime = 3f;
    float nextImpactTime;
    float maxHealth;
    bool broken;
    bool towerScaled;
    bool breakLocked;
    public void SetBreakLocked(bool locked) => breakLocked = locked;
    GameObject fallingVisual;
    public void SetFallingVisual(GameObject visual) => fallingVisual = visual;
    public float Health => health;
    public float MaxHealth => maxHealth;
    public event Action<ImpactTarget, float> Damaged;
    public event Action<Vector3, float> SurvivedHit;
    void Awake() { maxHealth = Mathf.Max(0.01f, health); }

    float ImpactDamage(PlayerController player, float speed, bool downwardPound)
    {
        float damage = proportionalSpeedDamage
            ? maxHealth * speed / Mathf.Max(0.01f, breakSpeed)
            : speed * damagePerSpeed;
        if (downwardPound) damage += poundBonusDamage;
        return player.Stats.Evaluate(PlayerStat.ImpactDamage, damage);
    }

    void ApplyTowerScale(PlayerController player)
    {
        if (towerScaled || hatch || !proportionalSpeedDamage || player == null) return;
        towerScaled = true;
        float scale = player.DestructibleTowerScale;
        maxHealth *= scale;
        health *= scale;
        breakSpeed *= scale;
    }

    // The same damage stat affects axe strikes and collisions.
    public void ReceivePunch(PlayerController player, float damage) => ReceivePunch(player, damage, player != null ? player.transform.forward : Vector3.zero);
    public void ReceivePunch(PlayerController player, float damage, Vector3 direction)
    {
        if (broken || breakLocked || hatch || player == null || damage <= 0f || float.IsNaN(damage)) return;
        ApplyTowerScale(player);
        damage = player.Stats.Evaluate(PlayerStat.ImpactDamage, damage);
        ApplyDamage(player, damage);
        if (!broken) { SurvivedHit?.Invoke(direction, damage); PlayHitFeedback(direction); }
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
        if (broken || breakLocked || player == null) return false;
        ApplyTowerScale(player);
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
        if (broken || breakLocked || player == null) return;
        ApplyTowerScale(player);
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
        float damage = ImpactDamage(player, impactSpeed, downwardPound);
        ApplyDamage(player, damage);
        if (!broken && damage > 0f && !float.IsNaN(damage)) { SurvivedHit?.Invoke(-normal, impactSpeed); PlayHitFeedback(-normal); }
    }

    void Break(PlayerController player)
    {
        if (broken) return;
        broken = true;
        health = 0f;
        // Snapshot before disabling anything. Only descendants are related: never
        // break the supporting parent or unrelated props elsewhere in the room.
        var stacked = GetComponentsInChildren<ImpactTarget>();
        PlayDestructionFeedback();
        GameSfx.PlayAt("impact.break", transform.position);
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
        foreach (var child in stacked)
            if (child != this && child != null && child.gameObject.activeInHierarchy && !child.broken)
                child.Break(player);
        Broken?.Invoke(this);
        gameObject.SetActive(false);
    }
    void PlayHitFeedback(Vector3 direction)
    {
        if (!destructionFeedback) return;
        var go = new GameObject("Impacto de destructible");
        go.transform.position = transform.position;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            if (renderer.GetComponentInParent<ImpactTarget>() == this) { go.transform.position = renderer.bounds.center; break; }
        go.transform.rotation = direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main; main.loop = false; main.startLifetime = 0.3f; main.startSpeed = 1.5f; main.startSize = 0.07f;
        main.startColor = paperFragments ? new Color(1f, 0.92f, 0.7f) : new Color(0.8f, 0.63f, 0.4f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        var emission = particles.emission; emission.enabled = false;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) { var material = new Material(shader); go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material; Destroy(material, 1f); }
        particles.Play(); particles.Emit(5); Destroy(go, 1f);
    }
    void PlayDestructionFeedback()
    {
        if (!destructionFeedback) return;
        Vector3 center = transform.position;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
            if (renderer.GetComponentInParent<ImpactTarget>() == this)
            { center = renderer.bounds.center; break; }
        float lifetime = Mathf.Max(0.1f, feedbackLifetime);
        if (destructionParticles != null)
        {
            var effect = Instantiate(destructionParticles, center, Quaternion.identity);
            effect.Play(true);
            Destroy(effect.gameObject, lifetime);
            return;
        }
        var go = new GameObject(paperFragments ? "Paginas de rotura" : "Fragmentos de rotura");
        go.transform.position = center;
        var particles = go.AddComponent<ParticleSystem>();
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.loop = false;
        main.duration = lifetime;
        main.startLifetime = Mathf.Min(lifetime, 1.3f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
        main.startSize3D = true;
        main.startSizeX = paperFragments ? 0.16f : 0.09f;
        main.startSizeY = paperFragments ? 0.23f : 0.09f;
        main.startSizeZ = 0.09f;
        main.startColor = paperFragments ? new Color(1f, 0.92f, 0.7f) : new Color(0.8f, 0.63f, 0.4f);
        main.gravityModifier = paperFragments ? 0.2f : 0.7f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 12;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.1f;
        var rotation = particles.rotationOverLifetime; rotation.enabled = true; rotation.z = 3f;
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            var material = new Material(shader);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            Destroy(material, lifetime);
        }
        particles.Play();
        particles.Emit(paperFragments ? 10 : 7);
        Destroy(go, lifetime);
    }

}
