using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public enum RoomCurseKind { Ice = 1, Tar = 2, Lava = 3 }
public readonly struct RoomCurseStartedEvent
{
    public readonly RoomContext Room;
    public readonly RoomCurseKind Curse;
    public RoomCurseStartedEvent(RoomContext room, RoomCurseKind curse) { Room = room; Curse = curse; }
}
public readonly struct RoomCurseEndedEvent
{
    public readonly RoomContext Room;
    public readonly RoomCurseKind Curse;
    public RoomCurseEndedEvent(RoomContext room, RoomCurseKind curse) { Room = room; Curse = curse; }
}

// One optional room-wide curse; existing room lifecycle controls its lifetime.
[RequireComponent(typeof(RoomManager))]
public sealed class RoomCurseDirector : MonoBehaviour
{
    [SerializeField, Range(0f, 1f)] float chance = 0.1f;
    [SerializeField, Min(0.01f)] float transitionSeconds = 2f;
    [Header("Visuales de superficie")]
    [SerializeField] Material iceOverlayMaterial;
    [SerializeField] Material tarOverlayMaterial;
    [SerializeField] Material lavaOverlayMaterial;
    [FormerlySerializedAs("lavaPixelation")]
    [SerializeField] LavaPixelationMode surfacePixelation = LavaPixelationMode.None;
    [Header("Reglas de lava")]
    [SerializeField, Min(0f)] float lavaDamagePerTick = 1f;
    [SerializeField, Min(0f)] float lavaGroundedGraceSeconds = 1.25f;
    [SerializeField, Min(0.01f)] float lavaDamageRepeatSeconds = 1f;
    readonly List<GroundSurface> surfaces = new List<GroundSurface>();
    IDisposable entered, exited;
    RoomManager manager;
    RoomContext context;
    RoomCurseKind kind;
    LavaFloorOverlay surfaceOverlay;
    GroundSurfaceDamageGroup lavaDamageGroup;
    float elapsed;
    bool active;
    public bool IsActive => active;
    public RoomCurseKind ActiveCurse => kind;
    void Awake() { manager = GetComponent<RoomManager>(); }
    void OnEnable()
    {
        if (manager == null) manager = GetComponent<RoomManager>();
        entered = manager.Events.Subscribe<RoomEnteredEvent>(OnEntered);
        exited = manager.Events.Subscribe<RoomExitedEvent>(OnExited);
    }
    void OnDisable() { entered?.Dispose(); exited?.Dispose(); entered = exited = null; Clear(); }
    void OnEntered(RoomEnteredEvent message)
    {
        if (message.Context.Kind != RoomKind.Intermediate || TowerSafetyRules.IsProtectedRoom(message.Context.Index) || UnityEngine.Random.value >= chance) return;
        var geometry = message.Context.Room.GetComponent<RoomGeometry>();
        if (geometry == null) return;
        context = message.Context;
        kind = (RoomCurseKind)UnityEngine.Random.Range(1, 4);
        var surfaceMaterial = GetSurfaceMaterial(kind);
        if (surfaceMaterial == null)
        {
            Debug.LogWarning($"La maldición {kind} no tiene material de superficie asignado.", this);
            return;
        }
        if (kind == RoomCurseKind.Lava) lavaDamageGroup = new GroundSurfaceDamageGroup();
        surfaceOverlay = new LavaFloorOverlay(surfaceMaterial);
        foreach (var floor in geometry.Floors)
        {
            if (floor == null) continue;
            foreach (var renderer in floor.GetComponentsInChildren<Renderer>()) ApplyVisual(renderer);
            foreach (var collider in floor.GetComponentsInChildren<Collider>()) ApplySurface(collider);
        }
        if (surfaces.Count == 0) { Clear(); return; }
        surfaceOverlay.SetPixelation(surfacePixelation);
        surfaceOverlay.Show(0f);
        elapsed = 0f; active = true;
        var bounds = new Bounds();
        bool hasBounds = false;
        foreach (var surface in surfaces)
        {
            var collider = surface.GetComponent<Collider>();
            if (collider == null) continue;
            if (!hasBounds) { bounds = collider.bounds; hasBounds = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        Vector3 soundPosition = hasBounds ? bounds.center : context.Room.transform.position;
        if (kind == RoomCurseKind.Tar) GameSfx.StartTarRoom(context.Room.transform, soundPosition);
        if (kind == RoomCurseKind.Ice) GameSfx.PlayAt("surface.ice.freeze", soundPosition);
        manager.Events.Publish(new RoomCurseStartedEvent(context, kind));
    }
    void ApplyVisual(Renderer renderer)
    {
        surfaceOverlay?.Add(renderer);
    }

    Material GetSurfaceMaterial(RoomCurseKind curse) => curse switch
    {
        RoomCurseKind.Ice => iceOverlayMaterial,
        RoomCurseKind.Tar => tarOverlayMaterial,
        RoomCurseKind.Lava => lavaOverlayMaterial,
        _ => null
    };
    void ApplySurface(Collider collider)
    {
        if (!collider.enabled || collider.isTrigger || collider.GetComponent<GroundSurface>() != null) return;
        var surface = collider.gameObject.AddComponent<GroundSurface>();
        var modifiers = kind == RoomCurseKind.Ice ? new[] {
            new GroundSurface.Modifier { stat = PlayerStat.Braking, type = StatModifierType.Percent, value = -0.45f },
            new GroundSurface.Modifier { stat = PlayerStat.TurnAcceleration, type = StatModifierType.Percent, value = -0.35f }
        } : kind == RoomCurseKind.Tar ? new[] {
            new GroundSurface.Modifier { stat = PlayerStat.MoveSpeed, type = StatModifierType.Percent, value = -0.3f },
            new GroundSurface.Modifier { stat = PlayerStat.Acceleration, type = StatModifierType.Percent, value = -0.25f }
        } : Array.Empty<GroundSurface.Modifier>();
        surface.Configure(
            modifiers,
            kind == RoomCurseKind.Lava ? lavaDamagePerTick : 0f,
            lavaDamageRepeatSeconds,
            lavaGroundedGraceSeconds,
            kind == RoomCurseKind.Lava ? lavaDamageGroup : null);
        surface.SetAudioKind(kind == RoomCurseKind.Ice ? SurfaceAudioKind.Ice : kind == RoomCurseKind.Tar ? SurfaceAudioKind.Tar : SurfaceAudioKind.Lava);
        surfaces.Add(surface);
    }
    void Update()
    {
        if (!active) return;
        elapsed += Time.deltaTime;
        float blend = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, transitionSeconds));
        if (surfaceOverlay != null && surfaceOverlay.PixelationMode != surfacePixelation)
            surfaceOverlay.SetPixelation(surfacePixelation);
        surfaceOverlay?.Show(blend);
    }
    void OnExited(RoomExitedEvent message) { if (active && message.Context.Room == context.Room) Clear(); }
    void Clear()
    {
        bool notify = active; active = false;
        surfaceOverlay?.Dispose();
        surfaceOverlay = null;
        lavaDamageGroup?.Clear();
        lavaDamageGroup = null;
        foreach (var surface in surfaces) if (surface != null) { surface.enabled = false; Destroy(surface); }
        surfaces.Clear();
        if (notify && manager != null) manager.Events.Publish(new RoomCurseEndedEvent(context, kind));
    }
}
