using System;
using System.Collections.Generic;
using UnityEngine;
using MenuUIBase.Audio;

// Persistent trail hazard, independent of attack turns and room object lifetimes.
[RequireComponent(typeof(RoomManager), typeof(RoomEnemyRoster))]
[DefaultExecutionOrder(200)]
public sealed class GuiltEncounter : MonoBehaviour
{
    [SerializeField, Range(4f, 6f)] float delaySeconds = 4f;
    [SerializeField, Min(0.1f)] float radius = 0.55f;
    [SerializeField, Range(0f, 1f)] float maxHealthDrainPerSecond = 0.08f;
    [Header("Pasos invertidos")]
    [SerializeField, Range(0f, 1f)] float footstepVolume = 0.65f;
    [SerializeField, Min(1f)] float audibleDistance = 30f;
    AudioClip reversedSteps;
    AudioSource footstepSource;
    bool reverseFailed;
    struct Sample { public float Time; public Vector3 Position; public bool Walking; public float Pitch; }
    readonly Queue<Sample> trail = new Queue<Sample>();
    readonly List<IDisposable> subscriptions = new List<IDisposable>();
    RoomManager rooms;
    RoomEnemyRoster roster;
    GameObject visual;
    SpawnScaleFeedback appearance;
    Material bodyMaterial, eyeMaterial;
    Sample previous;
    bool recording;
    float clock, contactSeconds;
    Vector3 lastPlayerPosition;

    public bool TryGetEasyParryPoint(out Vector3 point)
    {
        point = visual != null ? visual.transform.position : default;
        return isActiveAndEnabled && visual != null && visual.activeInHierarchy && (appearance == null || !appearance.IsAppearing);
    }
    void Awake() { rooms = GetComponent<RoomManager>(); roster = GetComponent<RoomEnemyRoster>(); }
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        subscriptions.Add(rooms.Events.Subscribe<TowerStartedEvent>(_ => ResetTrail()));
        subscriptions.Add(rooms.Events.Subscribe<TowerCompletedEvent>(_ => ResetTrail()));
        subscriptions.Add(rooms.Events.Subscribe<RunLostEvent>(_ => ResetTrail()));
        subscriptions.Add(rooms.Events.Subscribe<RunWonEvent>(_ => ResetTrail()));
        subscriptions.Add(rooms.Events.Subscribe<TowerGenerationFailedEvent>(_ => ResetTrail()));
        subscriptions.Add(rooms.Events.Subscribe<RoomEnteredEvent>(e =>
        {
            if (e.Context.Kind != RoomKind.Intermediate || TowerSafetyRules.IsProtectedRoom(e.Context.Index)) ResetTrail();
        }));
    }
    void OnDisable()
    {
        foreach (var subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear(); ResetTrail();
    }
    void ResetTrail()
    {
        trail.Clear(); recording = false; clock = contactSeconds = 0f;
        if (footstepSource != null) footstepSource.Stop();
        if (visual != null) visual.SetActive(false);
    }
    void LateUpdate()
    {
        var player = rooms.Player;
        if (roster == null || !roster.GuiltSelected || !roster.EnemiesReady || !rooms.TimerRunning || player == null || !player.enabled)
        { ResetTrail(); return; }
        float dt = Time.deltaTime;
        if (dt <= 0f) { if (footstepSource != null) footstepSource.Pause(); return; }
        var body = player.GetComponent<CharacterController>();
        Vector3 position = body != null ? body.bounds.center : player.transform.position + Vector3.up * 0.5f;
        // Explicit warps must not create an invisible line across the map.
        if (recording && Vector3.Distance(lastPlayerPosition, position) > Mathf.Max(30f, player.Velocity.magnitude * dt * 2f + 2f)) ResetTrail();
        lastPlayerPosition = position;
        if (!recording)
        {
            recording = true; previous = new Sample { Time = 0f, Position = position };
        }
        clock += dt;
        var surface = player.GetComponent<GroundSurfaceSensor>();
        trail.Enqueue(new Sample { Time = clock, Position = position,
            Walking = surface != null && surface.FootstepsActive,
            Pitch = surface != null ? surface.FootstepPitch : 1f });
        float replayTime = clock - Mathf.Clamp(delaySeconds, 4f, 6f);
        if (replayTime < 0f) return;
        while (trail.Count > 0 && trail.Peek().Time <= replayTime) previous = trail.Dequeue();
        Sample next = trail.Count > 0 ? trail.Peek() : previous;
        float blend = next.Time > previous.Time ? Mathf.InverseLerp(previous.Time, next.Time, replayTime) : 0f;
        if (visual == null) CreateVisual();
        visual.transform.position = Vector3.Lerp(previous.Position, next.Position, blend);
        if (!visual.activeSelf)
        {
            visual.SetActive(true);
            appearance = SpawnScaleFeedback.Play(visual.transform);
        }
        UpdateFootsteps(previous.Walking, Mathf.Lerp(previous.Pitch, next.Pitch, blend));
        Vector3 facing = position - visual.transform.position;
        if (facing.sqrMagnitude > 0.001f) visual.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        // Trigger-stay semantics without solid colliders or dependence on physics layers.
        Vector3 nearest = body != null ? body.ClosestPoint(visual.transform.position) : position;
        bool touching = (appearance == null || !appearance.IsAppearing) && (nearest - visual.transform.position).sqrMagnitude <= radius * radius;
        var contactHealth = player.GetComponent<PlayerHealth>();
        if (touching && (contactHealth == null || !contactHealth.EnemyParryProtected)) contactSeconds += dt;
        if (contactSeconds > 0f && (!touching || contactSeconds >= 0.25f))
        {
            float duration = contactSeconds;
            contactSeconds = 0f;
            var health = player.GetComponent<PlayerHealth>();
            if (health != null) health.TakeDamage(health.MaxHealth * maxHealthDrainPerSecond * duration, RunLossReason.Guilt);
        }
    }
    void CreateVisual()
    {
        visual = new GameObject("Guilt"); visual.transform.SetParent(transform, false);
        var shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        bodyMaterial = new Material(shader); bodyMaterial.color = new Color(0.005f, 0.005f, 0.008f);
        eyeMaterial = new Material(shader); eyeMaterial.color = new Color(3f, 3f, 3f, 1f);
        Sphere("Sombra", Vector3.zero, Vector3.one * radius * 2f, bodyMaterial);
        Sphere("Ojo izquierdo", new Vector3(-0.19f, 0.13f, 0.49f) * (radius / 0.55f), new Vector3(0.12f, 0.18f, 0.1f) * (radius / 0.55f), eyeMaterial);
        Sphere("Ojo derecho", new Vector3(0.19f, 0.13f, 0.49f) * (radius / 0.55f), new Vector3(0.12f, 0.18f, 0.1f) * (radius / 0.55f), eyeMaterial);
        visual.SetActive(false);
    }
    void UpdateFootsteps(bool walking, float pitch)
    {
        if (!walking)
        {
            if (footstepSource != null) footstepSource.Stop();
            return;
        }
        if (reversedSteps == null && !reverseFailed)
        {
            var cue = GameSfx.Find("footstep.normal");
            var clip = cue != null ? cue.Clip : null;
            if (clip == null) return;
            if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
            if (clip.loadState == AudioDataLoadState.Failed) { reverseFailed = true; return; }
            if (clip.loadState != AudioDataLoadState.Loaded) return;
            var data = new float[clip.samples * clip.channels];
            if (!clip.GetData(data, 0)) { reverseFailed = true; Debug.LogWarning("Guilt necesita pasos con Decompress On Load para invertirlos.", this); return; }
            var mono = new float[clip.samples];
            for (int frame = 0; frame < clip.samples; frame++)
            {
                float value = 0f;
                for (int channel = 0; channel < clip.channels; channel++) value += data[frame * clip.channels + channel];
                mono[clip.samples - 1 - frame] = value / clip.channels;
            }
            reversedSteps = AudioClip.Create("Guilt - pasos invertidos", clip.samples, 1, clip.frequency, false);
            reversedSteps.SetData(mono, 0);
        }
        if (reversedSteps == null) return;
        if (footstepSource == null)
        {
            footstepSource = visual.AddComponent<AudioSource>();
            footstepSource.playOnAwake = false; footstepSource.loop = true;
            footstepSource.spatialBlend = 1f; footstepSource.dopplerLevel = 0f;
            footstepSource.rolloffMode = AudioRolloffMode.Linear;
            footstepSource.minDistance = 2f; footstepSource.maxDistance = Mathf.Max(2.1f, audibleDistance);
            footstepSource.clip = reversedSteps;
        }
        var audio = AudioManager.Instance;
        footstepSource.volume = footstepVolume * audio.MasterVolume * audio.SoundEffectsVolume;
        footstepSource.pitch = Mathf.Clamp(pitch, 0.65f, 1.8f);
        if (!footstepSource.isPlaying) footstepSource.Play();
    }
    void Sphere(string name, Vector3 position, Vector3 scale, Material material)
    {
        var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere); sphere.name = name;
        sphere.transform.SetParent(visual.transform, false); sphere.transform.localPosition = position; sphere.transform.localScale = scale;
        var collider = sphere.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
        sphere.GetComponent<Renderer>().sharedMaterial = material;
    }
    void OnDestroy()
    {
        if (visual != null) Destroy(visual);
        if (reversedSteps != null) Destroy(reversedSteps);
        if (bodyMaterial != null) Destroy(bodyMaterial);
        if (eyeMaterial != null) Destroy(eyeMaterial);
    }
}
