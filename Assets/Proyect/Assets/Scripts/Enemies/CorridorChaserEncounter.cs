using System;
using System.Collections.Generic;
using MenuUIBase.Audio;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A warned, non-destructible hazard travelling through the authored room routes.
[RequireComponent(typeof(RoomManager), typeof(RoomEnemyRoster))]
public sealed class CorridorChaserEncounter : MonoBehaviour
{
    [Tooltip("Prefab editable del corredor; si está vacío se carga Resources/Enemies/Corredor.")]
    [SerializeField] GameObject enemyPrefab;
    [SerializeField] Sprite sprite;
    [SerializeField, Range(0f, 1f)] float spawnChance = 0.3f;
    [SerializeField, Min(0)] int safeRoomsBetween = 2;
    [SerializeField, Min(1f)] float speed = 40f;
    [SerializeField, Min(1f)] float warningWaitSeconds = 6f;
    [Tooltip("Mostrar el aviso provisional de texto del perseguidor.")]
    [SerializeField] bool showWarningText;
    [Header("Daño y zona de impacto")]
    [Tooltip("Radio alternativo si el prefab no tiene SphereCollider. Si existe, se usa el radio del collider del prefab.")]
    [SerializeField, Min(0.05f)] float contactRadius = 0.45f;
    [Tooltip("Fracción de la vida máxima que quita por impacto, con el mínimo configurado.")]
    [SerializeField, Range(0f, 1f)] float damageFraction = 0.5f;
    [SerializeField, Min(0f)] float minimumDamage = 50f;
    [Tooltip("Tiempo sin daño al aparecer o recolocarse cuando se elimina una sala antigua. Sigue avanzando durante este margen.")]
    [SerializeField, Min(0.1f)] float relocationGraceSeconds = 0.75f;
    [SerializeField, Min(0f)] float heightOffset = 1f;
    [Header("Sacudida por cercanía")]
    [SerializeField, Min(0.1f)] float shakeDistance = 70f;
    [SerializeField, Range(0f, 1f)] float maximumShake = 1f;
    [SerializeField, Min(0.1f)] float shakeFrequency = 11f;
    [Header("Presión visual y audio")]
    [SerializeField, Range(0f, 1f)] float vignetteIntensity = 0.504f;
    [SerializeField, Range(0.01f, 1f)] float vignetteSmoothness = 0.9f;
    [SerializeField, Min(0.01f)] float pressureSmoothing = 0.35f;
    [SerializeField, Range(1f, 8f)] float audioDrive = 3f;
    [SerializeField, Min(0.02f)] float attackCrossfadeSeconds = 0.6f;
    RoomManager rooms;
    RoomEnemyRoster roster;
    readonly List<IDisposable> subscriptions = new List<IDisposable>();
    readonly List<Vector3> path = new List<Vector3>();
    GameObject visual;
    SphereCollider hitbox;
    Sprite fallbackSprite;
    readonly AudioSource[] attackSources = new AudioSource[2];
    int attackActive;
    double nextAttackStart;
    float attackFade;
    AudioCue attackCue;
    MMCameraShaker cameraShaker;
    Transform shakePivot, cameraTransform, cameraOriginalParent;
    float nextShakeAt;
    Volume pressureVolume;
    VolumeProfile pressureProfile;
    float pressure;
    RoomDefinition attempted, pathRoom;
    bool warning, running, hit;
    float warningAt;
    int lastEncounter = -1000, pathRoomIndex, pathIndex, lastPointIndex, endRoomIndex;
    int frequencyBuffs, speedBuffs, impatienceBuffs;
    Vector3 previousPlayer;
    bool hasPreviousPlayer;
    float damageAllowedAt;

    public bool TryGetEasyParryPoint(out Vector3 point)
    {
        point = visual != null ? visual.transform.position : default;
        return isActiveAndEnabled && running && !hit && visual != null && Time.time >= damageAllowedAt;
    }
    public void IncreaseFrequency() => frequencyBuffs++;
    public void IncreaseSpeed() => speedBuffs++;
    public void IncreaseImpatience() => impatienceBuffs++;
    void Awake() { rooms = GetComponent<RoomManager>(); roster = GetComponent<RoomEnemyRoster>(); }
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        subscriptions.Add(rooms.Events.Subscribe<TowerStartedEvent>(_ => ResetEncounter()));
        subscriptions.Add(rooms.Events.Subscribe<RoomEnteredEvent>(OnEntered));
        subscriptions.Add(rooms.Events.Subscribe<TowerCompletedEvent>(_ => Stop()));
        subscriptions.Add(rooms.Events.Subscribe<RunLostEvent>(_ => Stop()));
        subscriptions.Add(rooms.Events.Subscribe<RunWonEvent>(_ => Stop()));
        subscriptions.Add(rooms.Events.Subscribe<TowerGenerationFailedEvent>(_ => Stop()));
    }
    void OnDisable() { foreach (var s in subscriptions) s.Dispose(); subscriptions.Clear(); Stop(); DisposePressureVolume(); }
    void OnDestroy() { DisposePressureVolume(); if (fallbackSprite != null) Destroy(fallbackSprite); }
    void ResetEncounter() { Stop(); attempted = null; lastEncounter = -1000; }
    void Stop()
    {
        warning = running = hit = hasPreviousPlayer = false;
        damageAllowedAt = 0f;
        path.Clear(); pathRoom = null;
        for (int i = 0; i < attackSources.Length; i++)
        {
            if (attackSources[i] != null) attackSources[i].Stop();
            attackSources[i] = null;
        }
        attackCue = null;
        hitbox = null;
        if (cameraTransform != null && shakePivot != null) cameraTransform.SetParent(cameraOriginalParent, false);
        if (shakePivot != null) Destroy(shakePivot.gameObject);
        cameraShaker = null; shakePivot = cameraTransform = cameraOriginalParent = null;
        if (visual != null) { Destroy(visual); visual = null; }
    }
    void OnEntered(RoomEnteredEvent e)
    {
        if (e.Context.Kind == RoomKind.Final) { Stop(); return; }
        if (warning && !TowerSafetyRules.IsProtectedRoom(e.Context.Index)) StartRun();
    }
    void Update()
    {
        UpdatePressure();
        if (rooms == null || roster == null || !roster.CorridorChaserSelected || rooms.Player == null || rooms.IsFinished || rooms.IsLost) return;
        if (running) { Advance(); return; }
        if (warning)
        {
            if (!TowerSafetyRules.IsProtectedRoom(rooms.RoomsEntered) && Time.time - warningAt >= Mathf.Max(2f, warningWaitSeconds * Mathf.Pow(0.8f, impatienceBuffs))) StartRun();
            return;
        }
        if (rooms.RoomsEntered < 2 || rooms.NextRoom == null || rooms.NextRoom == attempted || rooms.RoomsEntered + 1 >= rooms.RoomsPerTower) return;
        attempted = rooms.NextRoom;
        if (rooms.RoomsEntered - lastEncounter <= Mathf.Max(0, safeRoomsBetween - frequencyBuffs)) return;
        if (UnityEngine.Random.value >= Mathf.Clamp01(spawnChance + frequencyBuffs * 0.15f)) return;
        warning = true; warningAt = Time.time;
        var exit = rooms.CurrentRoom != null ? rooms.CurrentRoom.Exit : null;
        if (exit != null && exit.Anchor != null) GameSfx.PlayAt("corredor.alert", exit.Anchor.position);
        var context = new RoomContext(rooms.CurrentRoom, rooms.RoomsEntered, rooms.RoomsPerTower, RoomKind.Intermediate);
        rooms.Events.Publish(new CorridorChaserWarningEvent(context));
    }
    void StartRun()
    {
        warning = false; running = true; hit = false;
        lastEncounter = rooms.RoomsEntered;
        endRoomIndex = Mathf.Min(rooms.RoomsEntered + 1, rooms.RoomsPerTower - 1);
        // Start at the entrance of the oldest active room, behind the player.
        // The first intermediate room is used only after the player has left it.
        pathRoomIndex = Mathf.Max(2, rooms.RoomsEntered - 1);
        pathRoom = null; pathIndex = 0;
        var prefab = enemyPrefab != null ? enemyPrefab : Resources.Load<GameObject>("Enemies/Corredor");
        if (prefab != null) visual = Instantiate(prefab, transform);
        else
        {
            visual = new GameObject("Perseguidor de pasillo");
            visual.transform.SetParent(transform, false);
            var renderer = visual.AddComponent<SpriteRenderer>();
            if (sprite == null && fallbackSprite == null) fallbackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            renderer.sprite = sprite != null ? sprite : fallbackSprite;
            renderer.color = sprite != null ? Color.white : new Color(0.9f, 0.15f, 0.15f, 1f);
            visual.transform.localScale = new Vector3(2f, 2f, 1f);
        }
        hitbox = visual.GetComponent<SphereCollider>();
        if (hitbox != null) hitbox.enabled = false; // Radius is used by the swept test, not by physics contacts.
        if (!SetPath(RoomForIndex(pathRoomIndex))) { Stop(); return; }
        Relocate(path[0]);
        SetupCameraShake();
        attackCue = GameSfx.Find("corredor.attack");
        if (attackCue != null && attackCue.Clip != null && attackCue.Clip.length > 0.04f)
        {
            for (int i = 0; i < attackSources.Length; i++)
            {
                var sound = new GameObject("Grito " + (i + 1));
                sound.transform.SetParent(visual.transform, false);
                var source = sound.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.clip = attackCue.Clip;
                source.loop = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = attackCue.MinDistance;
                source.maxDistance = attackCue.MaxDistance;
                source.priority = attackCue.Priority;
                source.volume = 0f;
                sound.AddComponent<CorridorAudioDrive>().Drive = Mathf.Clamp(audioDrive, 1f, 8f);
                attackSources[i] = source;
            }
            attackActive = 0;
            attackFade = Mathf.Min(Mathf.Max(0.02f, attackCrossfadeSeconds), attackCue.Clip.length * 0.5f);
            double firstStart = AudioSettings.dspTime + 0.05;
            nextAttackStart = firstStart + attackCue.Clip.length - attackFade;
            attackSources[0].PlayScheduled(firstStart);
            attackSources[1].PlayScheduled(nextAttackStart);
            UpdateAttackVolume();
        }
        previousPlayer = PlayerCenter(); hasPreviousPlayer = true;
    }
    RoomDefinition RoomForIndex(int index)
    {
        if (index == rooms.RoomsEntered - 1) return rooms.PreviousRoom;
        if (index == rooms.RoomsEntered) return rooms.CurrentRoom;
        if (index == rooms.RoomsEntered + 1) return rooms.NextRoom;
        return null;
    }
    bool SetPath(RoomDefinition room)
    {
        if (room == null || !room.gameObject.activeInHierarchy) return false;
        pathRoom = room; path.Clear(); pathIndex = 0;
        Transform points = null;
        foreach (Transform child in room.transform) if (string.Equals(child.name, "Puntos", StringComparison.OrdinalIgnoreCase)) { points = child; break; }
        var ordered = new List<Transform>();
        if (points != null)
            foreach (Transform point in points)
                if (point.gameObject.activeInHierarchy && point.name.StartsWith("Punto", StringComparison.OrdinalIgnoreCase)) ordered.Add(point);
        ordered.Sort((a, b) => PointOrder(a.name).CompareTo(PointOrder(b.name)));
        if (ordered.Count > 1 && room.EntryAnchor != null &&
            Vector3.Distance(ordered[ordered.Count - 1].position, room.EntryAnchor.position) < Vector3.Distance(ordered[0].position, room.EntryAnchor.position)) ordered.Reverse();
        if (room.EntryAnchor != null) path.Add(room.EntryAnchor.position + Vector3.up * heightOffset);
        foreach (var point in ordered) path.Add(point.position + Vector3.up * heightOffset);
        lastPointIndex = Mathf.Max(0, path.Count - 1);
        if (room.Exit != null && room.Exit.Anchor != null) path.Add(room.Exit.Anchor.position + Vector3.up * heightOffset);
        return path.Count > 0;
    }
    static int PointOrder(string name)
    {
        string suffix = name.Substring(5).Trim();
        if (int.TryParse(suffix, out int number)) return number;
        return suffix.Length == 1 && char.IsLetter(suffix[0]) ? char.ToUpperInvariant(suffix[0]) - 'A' + 1 : int.MaxValue;
    }
    Vector3 PlayerCenter()
    {
        var body = rooms.Player.GetComponent<CharacterController>();
        return body != null ? body.bounds.center : rooms.Player.transform.position + Vector3.up;
    }
    void SetupCameraShake()
    {
        var camera = rooms.Player.GetComponentInChildren<Camera>();
        if (camera == null || camera.transform.parent == null) return;
        cameraTransform = camera.transform;
        cameraOriginalParent = cameraTransform.parent;
        var rig = new GameObject("Corredor FEEL shake");
        shakePivot = rig.transform;
        shakePivot.SetParent(cameraOriginalParent, false);
        cameraTransform.SetParent(shakePivot, false);
        cameraShaker = rig.AddComponent<MMCameraShaker>();
        cameraShaker.Channel = 193;
        var wiggle = rig.GetComponent<MMWiggle>();
        wiggle.PositionActive = true;
        wiggle.PositionWiggleProperties = new WiggleProperties
        {
            WiggleType = WiggleTypes.Noise,
            WigglePermitted = false,
            StartWigglingAutomatically = false
        };
        nextShakeAt = 0f;
    }
    void UpdateCameraShake(Vector3 playerCenter)
    {
        if (cameraShaker == null || visual == null || Time.time < nextShakeAt || Time.deltaTime <= 0f) return;
        float proximity = 1f - Mathf.Clamp01(Vector3.Distance(visual.transform.position, playerCenter) / Mathf.Max(0.1f, shakeDistance));
        float amplitude = Mathf.Clamp01(maximumShake) * proximity;
        if (amplitude < 0.001f) return;
        cameraShaker.ShakeCamera(0.2f, 0f, Mathf.Max(0.1f, shakeFrequency), amplitude, amplitude * 0.65f, 0f, false);
        nextShakeAt = Time.time + 0.12f;
    }
    void UpdatePressure()
    {
        float target = 0f;
        if (running && visual != null && rooms != null && rooms.Player != null)
        {
            target = 1f - Mathf.Clamp01(Vector3.Distance(visual.transform.position, PlayerCenter()) / Mathf.Max(0.1f, shakeDistance));
            if (pressureVolume == null && !CreatePressureVolume()) return;
        }
        pressure = Mathf.Lerp(pressure, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime / Mathf.Max(0.01f, pressureSmoothing)));
        if (pressureVolume != null) pressureVolume.weight = pressure;
    }
    bool CreatePressureVolume()
    {
        var camera = rooms.Player.GetComponentInChildren<Camera>();
        if (camera == null) return false;
        var cameraData = camera.GetUniversalAdditionalCameraData();
        int mask = cameraData.volumeLayerMask.value;
        if (mask == 0) return false;
        cameraData.renderPostProcessing = true;
        var go = new GameObject("Corredor Pressure Volume");
        go.transform.SetParent(transform, false);
        for (int layer = 0; layer < 32; layer++) if ((mask & (1 << layer)) != 0) { go.layer = layer; break; }
        pressureVolume = go.AddComponent<Volume>();
        pressureVolume.isGlobal = true; pressureVolume.priority = 150f; pressureVolume.weight = 0f;
        pressureProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = pressureProfile.Add<Vignette>();
        vignette.color.Override(Color.black);
        vignette.intensity.Override(Mathf.Clamp01(vignetteIntensity));
        vignette.smoothness.Override(Mathf.Clamp(vignetteSmoothness, 0.01f, 1f));
        pressureVolume.sharedProfile = pressureProfile;
        return true;
    }
    void DisposePressureVolume()
    {
        pressure = 0f;
        if (pressureVolume != null) Destroy(pressureVolume.gameObject);
        if (pressureProfile != null)
        {
            foreach (var component in pressureProfile.components) Destroy(component);
            Destroy(pressureProfile);
        }
        pressureVolume = null; pressureProfile = null;
    }
    void Advance()
    {
        if (visual == null || rooms.Player == null) { Stop(); return; }
        UpdateAttackVolume();
        // The manager disables old rooms before broadcasting the next RoomEntered.
        if (pathRoomIndex < rooms.RoomsEntered - 1 || pathRoom == null || !pathRoom.gameObject.activeInHierarchy)
        {
            pathRoomIndex = Mathf.Max(2, rooms.RoomsEntered - 1);
            if (pathRoomIndex > endRoomIndex) { Stop(); return; }
            if (!SetPath(RoomForIndex(pathRoomIndex))) { Stop(); return; }
            pathIndex = lastPointIndex;
            Relocate(path[pathIndex]);
        }
        var camera = rooms.Player.GetComponentInChildren<Camera>();
        if (camera != null)
        {
            Vector3 facing = visual.transform.position - camera.transform.position;
            if (facing.sqrMagnitude > 0.001f) visual.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
        }
        float distance = Mathf.Max(0f, speed * (1f + 0.2f * speedBuffs) * Time.deltaTime);
        float frameDistance = distance;
        // A sweep represents the whole frame. If grace ends part-way through it,
        // skip that sweep rather than retroactively hitting during the safe time.
        bool canDamage = Time.time - Time.deltaTime >= damageAllowedAt;
        Vector3 playerCenter = PlayerCenter();
        UpdateCameraShake(playerCenter);
        while (distance > 0f && running)
        {
            if (pathIndex >= path.Count)
            {
                if (++pathRoomIndex > endRoomIndex) { Stop(); break; }
                var room = RoomForIndex(pathRoomIndex);
                if (room == null) { pathRoomIndex--; break; }
                if (!SetPath(room)) { Stop(); break; }
            }
            Vector3 before = visual.transform.position;
            Vector3 target = path[pathIndex];
            float step = Vector3.Distance(before, target);
            visual.transform.position = Vector3.MoveTowards(before, target, distance);
            float travelled = Mathf.Min(distance, step);
            float startFraction = frameDistance > 0f ? (frameDistance - distance) / frameDistance : 0f;
            float endFraction = frameDistance > 0f ? (frameDistance - distance + travelled) / frameDistance : 1f;
            Vector3 playerBefore = Vector3.Lerp(previousPlayer, playerCenter, startFraction);
            Vector3 playerAfter = Vector3.Lerp(previousPlayer, playerCenter, endFraction);
            Vector3 hitboxOffset = hitbox != null ? hitbox.transform.TransformVector(hitbox.center) : Vector3.zero;
            if (canDamage && !hit && hasPreviousPlayer && Flupin.TouchesPlayer(before + hitboxOffset, visual.transform.position + hitboxOffset, playerBefore, playerAfter, HitRadius()))
            {
                hit = true;
                var health = rooms.Player.GetComponent<PlayerHealth>();
                if (health != null) health.TakeDamage(Mathf.Max(minimumDamage, health.MaxHealth * Mathf.Clamp01(damageFraction)), RunLossReason.CorridorChaser);
                if (!running) break;
            }
            distance -= travelled;
            if (step <= 0.001f || visual.transform.position == target) pathIndex++;
        }
        previousPlayer = playerCenter; hasPreviousPlayer = true;
    }
    void Relocate(Vector3 position)
    {
        visual.transform.position = position;
        previousPlayer = PlayerCenter();
        hasPreviousPlayer = true;
        SpawnScaleFeedback.Play(visual.transform);
        damageAllowedAt = Time.time + Mathf.Max(0.3f, relocationGraceSeconds);
    }
    void UpdateAttackVolume()
    {
        if (attackSources[0] == null || attackSources[1] == null || attackCue == null) return;
        var audio = AudioManager.Instance;
        if (audio == null) return;
        double now = AudioSettings.dspTime;
        int upcoming = 1 - attackActive;
        if (now >= nextAttackStart + attackFade)
        {
            attackSources[attackActive].Stop();
            attackActive = upcoming;
            upcoming = 1 - attackActive;
            nextAttackStart += attackCue.Clip.length - attackFade;
            // Recover gracefully from an unusually long frame or an audio-device reset.
            if (nextAttackStart <= now) nextAttackStart = now + 0.05;
            attackSources[upcoming].PlayScheduled(nextAttackStart);
        }
        float blend = Mathf.Clamp01((float)((now - nextAttackStart) / attackFade));
        float volume = attackCue.Volume * audio.MasterVolume * audio.SoundEffectsVolume;
        attackSources[attackActive].volume = volume * Mathf.Cos(blend * Mathf.PI * 0.5f);
        attackSources[upcoming].volume = volume * Mathf.Sin(blend * Mathf.PI * 0.5f);
    }
    void OnGUI()
    {
        if (showWarningText && warning) GUI.Box(new Rect(Screen.width * 0.5f - 200f, 65f, 400f, 55f), "¡PERSEGUIDOR EN CAMINO!\nApártate hacia una pared.");
    }
    void OnDrawGizmosSelected()
    {
        if (visual == null) return;
        Gizmos.color = Color.red;
        Vector3 center = hitbox != null ? hitbox.transform.TransformPoint(hitbox.center) : visual.transform.position;
        Gizmos.DrawWireSphere(center, HitRadius());
    }
    float HitRadius()
    {
        if (hitbox == null) return Mathf.Max(0f, contactRadius);
        Vector3 scale = hitbox.transform.lossyScale;
        return Mathf.Max(0f, hitbox.radius) * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
    }
}
