using System;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
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
    [Header("Bonus de velocidad")]
    [SerializeField, Range(0f, 0.99f)] float speedBonusStart = 0.5f;
    [SerializeField, Min(1f)] float maximumSpeedDamageMultiplier = 2.5f;
    [Header("Feedback FEEL de daño")]
    [SerializeField] bool damageFeel = true;
    [SerializeField, Min(0.01f)] float damageFeelDuration = 0.2f;
    [SerializeField] Color damageFeelColor = new Color(1f, 0.25f, 0.12f, 1f);
    [Header("Puntuación")]
    [SerializeField] string scoreReason = "Caja rota";
    [SerializeField, Min(0)] int scorePoints = 1;
    [Header("Feedback de destrucción")]
    [SerializeField] bool destructionFeedback = true;
    [SerializeField] bool paperFragments;
    [Tooltip("Opcional: sustituye el efecto provisional. Se instancia fuera del objeto destruido.")]
    [SerializeField] ParticleSystem destructionParticles;
    [SerializeField, Min(0.1f)] float feedbackLifetime = 3f;
    [Header("Fragmentos físicos opcionales")]
    [Tooltip("Prefab con un hijo directo por fragmento; mismo pivote y tamaño que el prop.")]
    [SerializeField] GameObject brokenFragmentsPrefab;
    [SerializeField, Range(1, 24)] int maxPhysicalFragments = 12;
    [SerializeField, Min(0.1f)] float fragmentLifetime = 4f;
    [SerializeField, Min(0f)] float fragmentImpulse = 2f;
    [SerializeField, Min(0f)] float fragmentUpwardImpulse = 1f;
    MMF_Player damageFeedback;
    readonly List<Material> feedbackMaterials = new List<Material>();
    readonly List<(Material material, string property, Color color)> feedbackOriginalColors = new List<(Material, string, Color)>();
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
        // Keep late-game props responsive without making a slow brush lethal.
        // Pound speed is vertical and must not be compared to horizontal sprint.
        float requiredSpeed = breakSpeed;
        if (proportionalSpeedDamage && !downwardPound)
            requiredSpeed = Mathf.Max(requiredSpeed, player.MaximumRunSpeed * 0.45f);
        float damage = proportionalSpeedDamage
            ? maxHealth * speed / Mathf.Max(0.01f, requiredSpeed)
            : speed * damagePerSpeed;
        if (downwardPound) damage += poundBonusDamage;
        return player.Stats.Evaluate(PlayerStat.ImpactDamage, damage) * SpeedDamageMultiplier(player);
    }

    float SpeedDamageMultiplier(PlayerController player)
    {
        float fraction = player.MaximumRunSpeedFraction;
        float bonus = Mathf.InverseLerp(Mathf.Clamp(speedBonusStart, 0f, 0.99f), 1f, fraction);
        return Mathf.Lerp(1f, Mathf.Max(1f, maximumSpeedDamageMultiplier), bonus);
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
        damage = player.Stats.Evaluate(PlayerStat.ImpactDamage, damage) * SpeedDamageMultiplier(player);
        ApplyDamage(player, damage, direction);
        if (!broken) { SurvivedHit?.Invoke(direction, damage); PlayDamageFeel(); PlayHitFeedback(direction); }
    }
    void ApplyDamage(PlayerController player, float damage, Vector3 direction)
    {
        if (damage <= 0f || float.IsNaN(damage)) return;
        float applied = Mathf.Min(health, damage);
        health = Mathf.Max(0f, health - damage);
        Damaged?.Invoke(this, applied);
        if (health <= 0f) Break(player, direction);
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
            if (downwardPound) Break(player, -normal);
            return;
        }
        if (impactSpeed <= 0f) return;
        if (!proportionalSpeedDamage && impactSpeed >= breakSpeed) { Break(player, -normal); return; }
        if (Time.time < nextImpactTime) return;
        nextImpactTime = Time.time + impactCooldown;
        float damage = ImpactDamage(player, impactSpeed, downwardPound);
        ApplyDamage(player, damage, -normal);
        if (!broken && damage > 0f && !float.IsNaN(damage)) { SurvivedHit?.Invoke(-normal, impactSpeed); PlayDamageFeel(); PlayHitFeedback(-normal); }
    }

    void Break(PlayerController player, Vector3 direction)
    {
        if (broken) return;
        broken = true;
        StopDamageFeel();
        health = 0f;
        // Snapshot before disabling anything. Only descendants are related: never
        // break the supporting parent or unrelated props elsewhere in the room.
        var stacked = GetComponentsInChildren<ImpactTarget>();
        PlayDestructionFeedback();
        SpawnFragments(player, direction);
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
                child.Break(player, direction);
        Broken?.Invoke(this);
        gameObject.SetActive(false);
    }
    void SpawnFragments(PlayerController player, Vector3 direction)
    {
        if (brokenFragmentsPrefab == null || hatch) return;
        var fragments = Instantiate(brokenFragmentsPrefab, transform.position, transform.rotation);
        fragments.transform.localScale = transform.lossyScale;
        var playerColliders = player.GetComponentsInChildren<Collider>();
        int count = 0;
        foreach (Transform piece in fragments.transform)
        {
            if (count++ >= Mathf.Clamp(maxPhysicalFragments, 1, 24)) { piece.gameObject.SetActive(false); continue; }
            var renderers = piece.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { piece.gameObject.SetActive(false); continue; }
            var colliders = piece.GetComponentsInChildren<Collider>();
            bool usable = false;
            foreach (var collider in colliders)
                if (collider.enabled && !collider.isTrigger) usable = true;
            foreach (var mesh in piece.GetComponentsInChildren<MeshCollider>())
                if (!mesh.convex) { usable = false; break; }
            if (!usable)
            {
                foreach (var collider in colliders) collider.enabled = false;
                // Simple local bounds collider: avoids non-convex dynamic mesh colliders.
                var box = piece.gameObject.AddComponent<BoxCollider>();
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                Vector3 min = bounds.min, max = bounds.max;
                var localBounds = new Bounds(piece.InverseTransformPoint(min), Vector3.zero);
                for (int i = 0; i < 8; i++)
                    localBounds.Encapsulate(piece.InverseTransformPoint(new Vector3((i & 1) == 0 ? min.x : max.x, (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z)));
                box.center = localBounds.center; box.size = localBounds.size;
                colliders = new Collider[] { box };
            }
            foreach (var collider in colliders)
                foreach (var playerCollider in playerColliders)
                    if (collider != null && playerCollider != null) Physics.IgnoreCollision(collider, playerCollider);
            var body = piece.GetComponent<Rigidbody>();
            if (body == null) body = piece.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = false;
            Vector3 push = direction.sqrMagnitude > 0.0001f ? direction.normalized : player.transform.forward;
            body.AddForce(push * fragmentImpulse + Vector3.up * fragmentUpwardImpulse, ForceMode.Impulse);
        }
        Destroy(fragments, Mathf.Max(0.1f, fragmentLifetime));
    }
    void PlayDamageFeel()
    {
        if (!damageFeel || broken || !isActiveAndEnabled) return;
        if (damageFeedback == null)
        {
            var feedbacks = new List<MMF_Feedback>();
            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                if (renderer.GetComponentInParent<ImpactTarget>() != this) continue;
                var shared = renderer.sharedMaterials;
                Material[] instances = null;
                for (int i = 0; i < shared.Length; i++)
                {
                    var material = shared[i];
                    if (material == null) continue;
                    string property = material.HasProperty("_BaseColor") ? "_BaseColor" : material.HasProperty("_Color") ? "_Color" : null;
                    if (property == null) continue;
                    if (instances == null)
                    {
                        instances = renderer.materials;
                        foreach (var instance in instances)
                            if (instance != null && !feedbackMaterials.Contains(instance)) feedbackMaterials.Add(instance);
                    }
                    feedbackOriginalColors.Add((instances[i], property, instances[i].GetColor(property)));
                    feedbacks.Add(new MMF_MaterialSetProperty
                    {
                        TargetRenderer = renderer, MaterialID = i, PropertyName = property,
                        ExtraTargetRenderers = new List<MMF_MaterialSetProperty.ExtraRendererData>(),
                        PropertyType = MMF_MaterialSetProperty.PropertyTypes.Color,
                        NewColor = damageFeelColor, InterpolateValue = true, Duration = Mathf.Max(0.01f, damageFeelDuration)
                    });
                }
            }
            if (feedbacks.Count == 0) return;
            var go = new GameObject("FEEL daño de prop");
            go.transform.SetParent(transform, false);
            damageFeedback = go.AddComponent<MMF_Player>();
            damageFeedback.FeedbacksList = feedbacks;
            damageFeedback.Initialization();
        }
        // FEEL 5.5 does not retain this feedback's interpolation coroutine handle.
        // Stop all coroutines on the dedicated player before restarting or removing it.
        StopDamageFeel();
        damageFeedback.PlayFeedbacks();
    }
    void StopDamageFeel()
    {
        if (damageFeedback != null)
        {
            damageFeedback.StopFeedbacks();
            damageFeedback.StopAllCoroutines();
        }
        foreach (var entry in feedbackOriginalColors)
            if (entry.material != null && entry.material.HasProperty(entry.property))
                entry.material.SetColor(entry.property, entry.color);
    }
    void OnDisable() => StopDamageFeel();
    void OnDestroy()
    {
        StopDamageFeel();
        foreach (var material in feedbackMaterials) if (material != null) Destroy(material);
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
