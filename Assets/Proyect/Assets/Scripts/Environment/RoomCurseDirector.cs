using System;
using System.Collections.Generic;
using UnityEngine;

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
    [SerializeField] Shader transitionShader;
    readonly Dictionary<Renderer, Material[]> originals = new Dictionary<Renderer, Material[]>();
    readonly List<Material> materials = new List<Material>();
    readonly List<GroundSurface> surfaces = new List<GroundSurface>();
    IDisposable entered, exited;
    RoomManager manager;
    RoomContext context;
    RoomCurseKind kind;
    float elapsed;
    bool active;
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
        if (message.Context.Kind != RoomKind.Intermediate || UnityEngine.Random.value >= chance) return;
        var geometry = message.Context.Room.GetComponent<RoomGeometry>();
        if (geometry == null || transitionShader == null) return;
        context = message.Context;
        kind = (RoomCurseKind)UnityEngine.Random.Range(1, 4);
        foreach (var floor in geometry.Floors)
        {
            if (floor == null) continue;
            foreach (var renderer in floor.GetComponentsInChildren<Renderer>()) ApplyVisual(renderer);
            foreach (var collider in floor.GetComponentsInChildren<Collider>()) ApplySurface(collider);
        }
        if (surfaces.Count == 0) { Clear(); return; }
        elapsed = 0f; active = true;
        manager.Events.Publish(new RoomCurseStartedEvent(context, kind));
    }
    void ApplyVisual(Renderer renderer)
    {
        if (originals.ContainsKey(renderer)) return;
        var source = renderer.sharedMaterials;
        var changed = new Material[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var original = source[i];
            if (original == null) continue;
            var material = new Material(transitionShader);
            string textureProperty = original.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex";
            if (original.HasProperty(textureProperty))
            {
                material.SetTexture("_BaseMap", original.GetTexture(textureProperty));
                material.SetTextureScale("_BaseMap", original.GetTextureScale(textureProperty));
                material.SetTextureOffset("_BaseMap", original.GetTextureOffset(textureProperty));
            }
            if (original.HasProperty("_BaseColor")) material.SetColor("_BaseColor", original.GetColor("_BaseColor"));
            else if (original.HasProperty("_Color")) material.SetColor("_BaseColor", original.GetColor("_Color"));
            material.SetFloat("_CurseType", (float)kind);
            material.SetFloat("_CurseBlend", 0f);
            changed[i] = material; materials.Add(material);
        }
        originals.Add(renderer, source);
        renderer.sharedMaterials = changed;
    }
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
        surface.Configure(modifiers, kind == RoomCurseKind.Lava ? 1f : 0f);
        surfaces.Add(surface);
    }
    void Update()
    {
        if (!active) return;
        elapsed += Time.deltaTime;
        float blend = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, transitionSeconds));
        foreach (var material in materials) if (material != null) material.SetFloat("_CurseBlend", blend);
    }
    void OnExited(RoomExitedEvent message) { if (active && message.Context.Room == context.Room) Clear(); }
    void Clear()
    {
        bool notify = active; active = false;
        foreach (var pair in originals) if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
        foreach (var surface in surfaces) if (surface != null) { surface.enabled = false; Destroy(surface); }
        foreach (var material in materials) if (material != null) Destroy(material);
        originals.Clear(); surfaces.Clear(); materials.Clear();
        if (notify && manager != null) manager.Events.Publish(new RoomCurseEndedEvent(context, kind));
    }
}
