using System.Collections.Generic;
using MenuUIBase.Audio;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PlayerController))]
public sealed class GroundSurfaceSensor : MonoBehaviour
{
    [SerializeField, Min(0.01f)] float probeRadius = 0.2f;
    [SerializeField, Min(0f)] float probeHeight = 0.1f;
    readonly HashSet<GroundSurface> active = new HashSet<GroundSurface>();
    readonly HashSet<GroundSurface> current = new HashSet<GroundSurface>();
    readonly Collider[] overlapBuffer = new Collider[32];
    bool touchingLava;
    [SerializeField, Min(0f)] float minFootstepSpeed = 0.15f;
    [SerializeField, Range(0f, 1f)] float crouchFootstepVolume = 0.55f;
    Vector3 previousPosition;
    bool positionValid;
    public SurfaceAudioKind FootstepSurface { get; private set; }
    public string FootstepCueId => FootstepSurface == SurfaceAudioKind.Ice ? "footstep.ice" :
        FootstepSurface == SurfaceAudioKind.Tar ? "footstep.tar" : FootstepSurface == SurfaceAudioKind.Lava ? "footstep.lava" : "footstep.normal";
    // Called by a future footstep cadence/animation system, not every frame.
    public void PlayFootstep()
    {
        if (player != null && player.enabled && player.IsGrounded && Time.deltaTime > 0f)
            GameSfx.PlayAt(FootstepCueId, controller.bounds.center - Vector3.up * controller.bounds.extents.y);
    }
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
        bool lavaNow = false;
        FootstepSurface = SurfaceAudioKind.Normal;
        foreach (var surface in current)
        {
            if (surface.AudioKind == SurfaceAudioKind.Lava) lavaNow = true;
            if ((int)surface.AudioKind > (int)FootstepSurface) FootstepSurface = surface.AudioKind;
        }
        if (lavaNow != touchingLava && player.enabled && Time.deltaTime > 0f)
            GameSfx.PlayAt(lavaNow ? "surface.lava.enter" : "surface.lava.exit", controller.bounds.center - Vector3.up * controller.bounds.extents.y);
        touchingLava = lavaNow;
        foreach (var surface in active)
            if (surface != null && !current.Contains(surface)) surface.Exit(player);
        foreach (var surface in current)
        {
            surface.Enter(player); // Idempotent; also restores contact if a surface was re-enabled.
            surface.Stay(player);
        }
        active.Clear();
        foreach (var surface in current) active.Add(surface);
        Vector3 position = transform.position;
        Vector3 delta = position - previousPosition; delta.y = 0f;
        bool walking = positionValid && player.enabled && controller.enabled && player.IsGrounded && !player.IsPounding &&
            !player.IsSliding && !player.IsSprinting && player.HasMovementInput && Time.deltaTime > 0f &&
            delta.magnitude / Time.deltaTime >= minFootstepSpeed;
        previousPosition = position; positionValid = true;
        if (walking)
        {
            var cue = GameSfx.Find(FootstepCueId);
            if (cue == null || cue.Clip == null) cue = GameSfx.Find("footstep.normal");
            AudioManager.Instance.SetFootsteps(cue, player.IsCrouching ? crouchFootstepVolume : 1f);
        }
        else if (AudioManager.HasInstance) AudioManager.Instance.SetFootsteps(null, 0f);
        var names = new List<string>();
        foreach (var surface in active) if (surface != null) names.Add(surface.name);
        SurfaceNames = names.Count == 0 ? "Normal" : string.Join(", ", names);
    }
    void OnDisable()
    {
        foreach (var surface in active) if (surface != null) surface.Exit(player);
        active.Clear(); current.Clear(); SurfaceNames = "Normal";
        touchingLava = false; FootstepSurface = SurfaceAudioKind.Normal; positionValid = false;
        if (AudioManager.HasInstance) AudioManager.Instance.SetFootsteps(null, 0f);
    }
}
