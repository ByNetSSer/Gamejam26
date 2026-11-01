using System;

using System.Collections.Generic;

using UnityEngine;

using UnityEngine.InputSystem;



public readonly struct EnemyRepelAvailabilityChangedEvent

{

    public readonly PlayerController Player;

    public readonly EnemyAgent Enemy;

    public readonly bool Available;

    public EnemyRepelAvailabilityChangedEvent(PlayerController player, EnemyAgent enemy, bool available)

    { Player = player; Enemy = enemy; Available = available; }

}

public readonly struct EnemyRepelledEvent

{

    public readonly PlayerController Player;

    public readonly EnemyAgent Enemy;

    public readonly Vector3 Position;

    public readonly float Power;

    public EnemyRepelledEvent(PlayerController player, EnemyAgent enemy, Vector3 position, float power)

    { Player = player; Enemy = enemy; Position = position; Power = power; }

}



// Read the repel input before enemies advance into contact during this frame.

[DefaultExecutionOrder(-5)]

[RequireComponent(typeof(EnemyDirector))]

public sealed class PlayerRepel : MonoBehaviour

{

    [SerializeField, Min(0f)] float radius = 4.5f;

    [SerializeField, Min(0f)] float cooldown = 0.65f;

    [SerializeField, Min(0f)] float repelPower = 1f;

    [Header("Parry en fácil")]

    [Tooltip("Protección frente a enemigos después de un parry confirmado; no bloquea superficies.")]

    [SerializeField, Min(0f)] float easyEnemyProtection = 0.55f;

    [Header("Ataques a destructibles")]

    [SerializeField, Min(0f)] float propHitRange = 3f, strikeRange = 3.7f, strikeRadius = 0.55f, strikeCooldown = 0.3f;

    [SerializeField, Range(0f, 1f)] float strikeDamageFraction = 0.65f, parryDamageFraction = 0.3f;

    [SerializeField] LayerMask propHitLayers = ~0;

    [Header("Fatiga por parry repetido")]

    [SerializeField, Min(0f)] float repeatWindow = 1.8f, repeatCooldownStep = 0.12f, maximumExtraCooldown = 0.6f;

    [Header("Corte al golpear")]

    [Tooltip("Efecto opcional. Sin prefab se dibuja una línea de corte breve.")]

    [SerializeField] GameObject slashPrefab;

    [SerializeField, Min(0.01f)] float slashSize = 0.55f, slashLifetime = 0.16f, slashPrefabLifetime = 2f;

    [SerializeField] Color slashColor = new Color(1f, 0.86f, 0.7f, 1f);

    EnemyDirector director;

    float nextUse, nextStrike, lastParry = float.NegativeInfinity;

    int rapidParries;

    static Material slashMaterial;

    readonly HashSet<EnemyAgent> available = new HashSet<EnemyAgent>();

    EventStream events;

    // Listeners subscribe to the player's own stream and dispose subscriptions when disabled.

    public EventStream Events => events ?? (events = new EventStream(Debug.LogException));

    void Awake() { director = GetComponent<EnemyDirector>(); if (GetComponent<ParryFeedback>() == null) gameObject.AddComponent<ParryFeedback>(); if (GetComponent<ParryAccents>() == null) gameObject.AddComponent<ParryAccents>(); }

    void OnDisable() => ClearAvailability();

    void OnDestroy() => events?.Clear();

    void Update()

    {

        if (director == null || director.Player == null || !director.Player.enabled || Time.deltaTime <= 0f)

        { ClearAvailability(); return; }

        RefreshAvailability();

        if (director.Player.TutorialBlocksActions || GameplayPauseMenu.IsPaused) { ClearAvailability(); return; }
        if (Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;

        if (Mouse.current.leftButton.wasPressedThisFrame && Time.time >= nextStrike)

        {

            nextStrike = Time.time + Mathf.Max(0f, strikeCooldown);

            Events.Publish(new PlayerHandActionEvent(director.Player, PlayerHandAction.Punch));

            HitDestructibles(strikeRange, strikeDamageFraction);

        }

        if (Time.time < nextUse || !Mouse.current.rightButton.wasPressedThisFrame) return;

        rapidParries = Time.time - lastParry <= Mathf.Max(0f, repeatWindow) ? rapidParries + 1 : 0;

        lastParry = Time.time;

        float extra = Mathf.Min(Mathf.Max(0f, maximumExtraCooldown), rapidParries * Mathf.Max(0f, repeatCooldownStep));

        nextUse = Time.time + Mathf.Max(0f, cooldown) + extra;

        Events.Publish(new PlayerHandActionEvent(director.Player, PlayerHandAction.Parry));

        HitDestructibles(propHitRange, parryDamageFraction);

        bool easy = MenuUIBase.Core.RunDifficulty.Selected == MenuUIBase.Core.GameDifficulty.Easy;

        var snapshot = new List<EnemyAgent>(director.Enemies);

        foreach (var enemy in snapshot)

        {

            if (enemy == null || !enemy.isActiveAndEnabled) continue;

            if (enemy.CanBeRepelled && InParryRange(enemy.transform.position))

            {

                Vector3 point = enemy.transform.position;

                if (enemy.TryRepel(repelPower)) ConfirmParry(enemy, point, easy);

            }

            else if (easy && enemy.TryGetEasyParryPoint(transform.position, out Vector3 point) && InParryRange(point))

                ConfirmParry(enemy, point, true);

        }

        if (easy)

        {

            var corridor = FindFirstObjectByType<CorridorChaserEncounter>();

            if (corridor != null && corridor.TryGetEasyParryPoint(out Vector3 point) && InParryRange(point))

                ConfirmParry(null, point, true);

            var guilt = FindFirstObjectByType<GuiltEncounter>();

            if (guilt != null && guilt.TryGetEasyParryPoint(out point) && InParryRange(point))

                ConfirmParry(null, point, true);

        }

        RefreshAvailability();

    }

    bool InParryRange(Vector3 point) => (point - transform.position).sqrMagnitude <= radius * radius;

    void ConfirmParry(EnemyAgent enemy, Vector3 point, bool easy)

    {

        if (easy) director.Player.GetComponent<PlayerHealth>()?.GrantEnemyParryProtection(easyEnemyProtection);

        GameSfx.PlayAt("parry.success", point);

        Events.Publish(new EnemyRepelledEvent(director.Player, enemy, point, repelPower));

    }

    void HitDestructibles(float range, float fraction)

    {

        var player = director.Player;

        var view = player.GetComponentInChildren<Camera>();

        if (view == null || range <= 0f || fraction <= 0f) return;

        var ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        var hits = Physics.SphereCastAll(ray, Mathf.Max(0.01f, strikeRadius), range, propHitLayers, QueryTriggerInteraction.Ignore);

        Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        var struck = new HashSet<ImpactTarget>();

        foreach (var hit in hits)

        {

            if (hit.collider == null || hit.collider.transform.IsChildOf(player.transform)) continue;

            var target = hit.collider.GetComponentInParent<ImpactTarget>();

            if (target == null) break; // Solid walls block strikes.

            if (!struck.Add(target)) continue;

            if (target.ReceiveSpeedStrike(player, player.Velocity, hit.normal, fraction))

                ShowSlash(hit.point, hit.normal, view.transform);

        }

    }

    void ShowSlash(Vector3 point, Vector3 normal, Transform view)

    {

        if (slashPrefab != null)

        {

            var effect = Instantiate(slashPrefab, point + normal * 0.02f, Quaternion.LookRotation(normal));

            Destroy(effect, Mathf.Max(0.01f, slashPrefabLifetime));

            return;

        }

        if (slashMaterial == null)

        {

            var shader = Shader.Find("Sprites/Default");

            if (shader == null) return;

            slashMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

        }

        var slash = new GameObject("Corte de impacto");

        var line = slash.AddComponent<LineRenderer>();

        line.sharedMaterial = slashMaterial; line.useWorldSpace = true; line.positionCount = 2;

        line.startWidth = Mathf.Max(0.01f, slashSize * 0.09f); line.endWidth = 0f;

        line.startColor = slashColor; line.endColor = new Color(slashColor.r, slashColor.g, slashColor.b, 0f);

        Vector3 axis = Vector3.ProjectOnPlane(view.right + view.up * 0.5f, normal).normalized;

        if (axis.sqrMagnitude < 0.001f) axis = Vector3.Cross(normal, Vector3.up).normalized;

        Vector3 center = point + normal * 0.025f;

        line.SetPosition(0, center - axis * slashSize * 0.5f);

        line.SetPosition(1, center + axis * slashSize * 0.5f);

        Destroy(slash, Mathf.Max(0.01f, slashLifetime));

    }

    void LateUpdate() => RefreshAvailability();

    void RefreshAvailability()

    {

        var next = new HashSet<EnemyAgent>();

        if (director != null && director.Player != null && director.Player.enabled && !director.Player.TutorialBlocksActions && Time.deltaTime > 0f &&

            Time.time >= nextUse && Mouse.current != null)

        {

            foreach (var enemy in new List<EnemyAgent>(director.Enemies))

                if (enemy != null && enemy.isActiveAndEnabled &&

                    ((enemy.CanBeRepelled && InParryRange(enemy.transform.position)) ||

                     (MenuUIBase.Core.RunDifficulty.Selected == MenuUIBase.Core.GameDifficulty.Easy &&

                      enemy.TryGetEasyParryPoint(transform.position, out Vector3 point) && InParryRange(point))))

                    next.Add(enemy);

        }

        var previous = new List<EnemyAgent>(available);

        available.Clear();

        available.UnionWith(next);

        foreach (var enemy in previous)

            if (enemy != null && !next.Contains(enemy))

                Events.Publish(new EnemyRepelAvailabilityChangedEvent(director != null ? director.Player : null, enemy, false));

        foreach (var enemy in next)

            if (enemy != null && !previous.Contains(enemy))

            {

                if (enemy is Flupin) GameSfx.PlayFollowing("flupin.available", enemy.transform);

                Events.Publish(new EnemyRepelAvailabilityChangedEvent(director.Player, enemy, true));

            }

    }

    void ClearAvailability()

    {

        var previous = new List<EnemyAgent>(available);

        available.Clear();

        foreach (var enemy in previous)

            if (enemy != null) Events.Publish(new EnemyRepelAvailabilityChangedEvent(director != null ? director.Player : null, enemy, false));

    }

}



