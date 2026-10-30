using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerController))]
public sealed class GroundSurfaceSensor : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float probeRadius = 0.2f;
    [SerializeField, Min(0f)] float probeHeight = 0.1f;
    readonly HashSet<GroundSurface> active = new HashSet<GroundSurface>();
    readonly HashSet<GroundSurface> current = new HashSet<GroundSurface>();
    readonly Collider[] overlapBuffer = new Collider[32];
    PlayerController player;
    CharacterController controller;
    public string SurfaceNames { get; private set; } = "Normal";
    void Awake() { player = GetComponent<PlayerController>(); controller = GetComponent<CharacterController>(); }
    void LateUpdate()
    {
        current.Clear();
        if (player.IsGrounded && controller.enabled)
        {
            Vector3 center = controller.bounds.center;
            center.y = controller.bounds.min.y + probeHeight;
            float radius = Mathf.Max(0.01f, probeRadius);
            int count = Physics.OverlapSphereNonAlloc(center, radius, overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            // An unusually crowded spot must not silently omit a surface.
            var overlaps = count == overlapBuffer.Length
                ? Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide) : overlapBuffer;
            if (count == overlapBuffer.Length) count = overlaps.Length;
            for (int i = 0; i < count; i++)
            {
                var surface = overlaps[i].GetComponentInParent<GroundSurface>();
                if (surface != null && surface.isActiveAndEnabled) current.Add(surface);
            }
        }
        foreach (var surface in active)
            if (surface != null && !current.Contains(surface)) surface.Exit(player);
        foreach (var surface in current)
        {
            surface.Enter(player); // Idempotent; also restores contact if a surface was re-enabled.
            surface.Stay(player);
        }
        active.Clear();
        foreach (var surface in current) active.Add(surface);
        var names = new List<string>();
        foreach (var surface in active) if (surface != null) names.Add(surface.name);
        SurfaceNames = names.Count == 0 ? "Normal" : string.Join(", ", names);
    }
    void OnDisable()
    {
        foreach (var surface in active) if (surface != null) surface.Exit(player);
        active.Clear(); current.Clear(); SurfaceNames = "Normal";
    }
}
