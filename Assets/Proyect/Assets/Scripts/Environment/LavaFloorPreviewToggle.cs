using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

// Editor playground helper: H, B and L preview the real room-surface visuals
// and contact behaviour without requiring a RoomManager transition.
public sealed class LavaFloorPreviewToggle : MonoBehaviour
{
    [Header("Materiales de referencia")]
    [FormerlySerializedAs("lavaOverlayMaterial")]
    [SerializeField] Material legacyLavaOverlayMaterial;
    [SerializeField] Material iceOverlayMaterial;
    [SerializeField] Material tarOverlayMaterial;
    [SerializeField] Material lavaOverlayMaterial;
    [SerializeField, Min(0.1f)] float activeSeconds = 30f;
    [SerializeField, Min(0.01f)] float transitionSeconds = 2f;
    [SerializeField] RoomCurseKind previewKind = RoomCurseKind.Lava;
    [Header("Presentación")]
    [SerializeField] LavaPixelationMode pixelationMode = LavaPixelationMode.None;
    [SerializeField, Min(0f)] float damagePerTick = 1f;
    [SerializeField, Min(0f)] float groundedGraceSeconds = 1.25f;
    [SerializeField, Min(0.01f)] float damageRepeatSeconds = 1f;
    [SerializeField] bool includeNamedFloorRenderers = true;
    [SerializeField] bool showDiagnostics = true;

    readonly HashSet<Renderer> renderers = new HashSet<Renderer>();
    readonly HashSet<Collider> colliders = new HashSet<Collider>();
    readonly List<GroundSurface> previewSurfaces = new List<GroundSurface>();
    readonly GroundSurfaceDamageGroup previewDamageGroup = new GroundSurfaceDamageGroup();
    LavaFloorOverlay overlay;
    float elapsed;
    bool active;
    bool initialized;
    InputAction iceAction;
    InputAction tarAction;
    InputAction lavaAction;
    InputAction cyclePixelationAction;
    PlayerController previewPlayer;
    PlayerHealth previewHealth;
    GroundSurfaceSensor previewSensor;

    void OnEnable()
    {
        iceAction = new InputAction("Alternar hielo de prueba", InputActionType.Button, "<Keyboard>/h");
        iceAction.performed += OnIcePerformed;
        iceAction.Enable();
        tarAction = new InputAction("Alternar brea de prueba", InputActionType.Button, "<Keyboard>/b");
        tarAction.performed += OnTarPerformed;
        tarAction.Enable();
        lavaAction = new InputAction("Alternar lava de prueba", InputActionType.Button, "<Keyboard>/l");
        lavaAction.performed += OnLavaPerformed;
        lavaAction.Enable();
        cyclePixelationAction = new InputAction("Cambiar pixelado de lava", InputActionType.Button, "<Keyboard>/o");
        cyclePixelationAction.performed += OnCyclePixelationPerformed;
        cyclePixelationAction.Enable();
    }

    void OnDisable()
    {
        DisposeAction(ref iceAction, OnIcePerformed);
        DisposeAction(ref tarAction, OnTarPerformed);
        DisposeAction(ref lavaAction, OnLavaPerformed);
        if (cyclePixelationAction != null)
        {
            cyclePixelationAction.performed -= OnCyclePixelationPerformed;
            cyclePixelationAction.Disable();
            cyclePixelationAction.Dispose();
            cyclePixelationAction = null;
        }
        if (active) Deactivate();
    }

    static void DisposeAction(ref InputAction action, Action<InputAction.CallbackContext> callback)
    {
        if (action == null) return;
        action.performed -= callback;
        action.Disable();
        action.Dispose();
        action = null;
    }

    void Start()
    {
        Initialize();
    }

    void Update()
    {
        if (!active) return;
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= activeSeconds)
        {
            Deactivate();
            return;
        }

        float fadeIn = elapsed / Mathf.Max(0.01f, transitionSeconds);
        float fadeOut = (activeSeconds - elapsed) / Mathf.Max(0.01f, transitionSeconds);
        if (overlay != null && overlay.PixelationMode != pixelationMode)
            overlay.SetPixelation(pixelationMode);
        overlay?.Show(Mathf.Clamp01(Mathf.Min(fadeIn, fadeOut)));
    }

    void OnIcePerformed(InputAction.CallbackContext context) { if (context.performed) ToggleSurface(RoomCurseKind.Ice); }
    void OnTarPerformed(InputAction.CallbackContext context) { if (context.performed) ToggleSurface(RoomCurseKind.Tar); }
    void OnLavaPerformed(InputAction.CallbackContext context) { if (context.performed) ToggleSurface(RoomCurseKind.Lava); }

    void ToggleSurface(RoomCurseKind kind)
    {
        if (active && previewKind == kind) Deactivate();
        else Activate(kind);
    }

    void OnCyclePixelationPerformed(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        CyclePixelation();
    }

    void CyclePixelation()
    {
        pixelationMode = pixelationMode == LavaPixelationMode.None
            ? LavaPixelationMode.Fine
            : pixelationMode == LavaPixelationMode.Fine
                ? LavaPixelationMode.Large
                : LavaPixelationMode.None;
        if (active) overlay?.SetPixelation(pixelationMode);
    }

    void Initialize()
    {
        if (initialized) return;
        initialized = true;
        if (iceOverlayMaterial == null && tarOverlayMaterial == null && lavaOverlayMaterial == null)
        {
            Debug.LogWarning("Prueba de superficies: no hay materiales de hielo, brea o lava asignados.", this);
            return;
        }

        foreach (var player in FindObjectsByType<PlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != gameObject.scene) continue;
            previewPlayer = player;
            previewHealth = player.GetComponent<PlayerHealth>();
            previewSensor = player.GetComponent<GroundSurfaceSensor>();
            if (previewSensor == null)
            {
                previewSensor = player.gameObject.AddComponent<GroundSurfaceSensor>();
                Debug.Log("Prueba de lava: se añadió el GroundSurfaceSensor que faltaba al jugador de esta escena.", player);
            }
            break;
        }

        foreach (var geometry in FindObjectsByType<RoomGeometry>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (geometry.gameObject.scene != gameObject.scene) continue;
            foreach (var floor in geometry.Floors) AddFloorRoot(floor);
        }

        if (includeNamedFloorRenderers)
        {
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (renderer.gameObject.scene != gameObject.scene || !LooksLikeFloor(renderer.name)) continue;
                AddRenderer(renderer);
                foreach (var collider in renderer.GetComponentsInChildren<Collider>(true)) AddCollider(collider);
            }
            foreach (var collider in FindObjectsByType<Collider>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (collider.gameObject.scene != gameObject.scene || !LooksLikeFloorInHierarchy(collider.transform)) continue;
                AddCollider(collider);
            }
        }

        // Some authored test tiles still contain an older lava pass. Strip any
        // known curse material for the clean NORMAL baseline during Play Mode.
        var cleanupMaterial = lavaOverlayMaterial != null ? lavaOverlayMaterial
            : iceOverlayMaterial != null ? iceOverlayMaterial
            : tarOverlayMaterial;
        overlay = CreateOverlay(cleanupMaterial);
        overlay?.Hide();
        Debug.Log($"Prueba de superficies lista: {renderers.Count} renderers y {colliders.Count} colliders. H activa hielo, B activa brea y L activa lava.", this);
    }

    void AddFloorRoot(GameObject floor)
    {
        if (floor == null) return;
        foreach (var renderer in floor.GetComponentsInChildren<Renderer>(true)) AddRenderer(renderer);
        foreach (var collider in floor.GetComponentsInChildren<Collider>(true)) AddCollider(collider);
    }

    void AddRenderer(Renderer renderer)
    {
        if (renderer != null) renderers.Add(renderer);
    }

    void AddCollider(Collider collider)
    {
        if (collider != null && collider.enabled && !collider.isTrigger) colliders.Add(collider);
    }

    static bool LooksLikeFloor(string objectName)
    {
        return objectName.IndexOf("floor", StringComparison.OrdinalIgnoreCase) >= 0 ||
               objectName.IndexOf("piso", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool LooksLikeFloorInHierarchy(Transform target)
    {
        for (var current = target; current != null; current = current.parent)
            if (LooksLikeFloor(current.name)) return true;
        return false;
    }

    void Activate()
    {
        Initialize();
        var material = GetSurfaceMaterial(previewKind);
        if (material == null)
        {
            Debug.LogWarning($"Prueba de superficies: {previewKind} no tiene material asignado.", this);
            return;
        }

        overlay?.Dispose();
        overlay = CreateOverlay(material);
        overlay.Hide();
        if (!overlay.IsValid || overlay.Count == 0)
        {
            Debug.LogWarning("Prueba de superficies: no se encontraron pisos para transformar.", this);
            return;
        }

        elapsed = 0f;
        active = true;
        EnsurePreviewSurfaces();
        overlay.SetPixelation(pixelationMode);
        overlay.Show(0f);
        Debug.Log($"{previewKind} de prueba activado sobre {previewSurfaces.Count} superficies con pixelado {pixelationMode}. Se retirará en {activeSeconds:0.#} segundos; repetir su tecla también lo cancela.", this);
    }

    LavaFloorOverlay CreateOverlay(Material material)
    {
        if (material == null) return null;
        var result = new LavaFloorOverlay(material, new[]
        {
            legacyLavaOverlayMaterial,
            iceOverlayMaterial,
            tarOverlayMaterial,
            lavaOverlayMaterial
        });
        foreach (var renderer in renderers) result.Add(renderer, true);
        return result;
    }

    void Activate(RoomCurseKind kind)
    {
        if (active) Deactivate();
        previewKind = kind;
        Activate();
    }

    Material GetSurfaceMaterial(RoomCurseKind kind) => kind switch
    {
        RoomCurseKind.Ice => iceOverlayMaterial,
        RoomCurseKind.Tar => tarOverlayMaterial,
        RoomCurseKind.Lava => lavaOverlayMaterial,
        _ => null
    };

    void EnsurePreviewSurfaces()
    {
        if (previewSurfaces.Count == 0)
        {
            foreach (var collider in colliders)
            {
                if (collider == null || collider.GetComponent<GroundSurface>() != null) continue;
                var surface = collider.gameObject.AddComponent<GroundSurface>();
                previewSurfaces.Add(surface);
            }
        }

        foreach (var surface in previewSurfaces)
        {
            if (surface == null) continue;
            var modifiers = previewKind == RoomCurseKind.Ice ? new[] {
                new GroundSurface.Modifier { stat = PlayerStat.Braking, type = StatModifierType.Percent, value = -0.45f },
                new GroundSurface.Modifier { stat = PlayerStat.TurnAcceleration, type = StatModifierType.Percent, value = -0.35f }
            } : previewKind == RoomCurseKind.Tar ? new[] {
                new GroundSurface.Modifier { stat = PlayerStat.MoveSpeed, type = StatModifierType.Percent, value = -0.3f },
                new GroundSurface.Modifier { stat = PlayerStat.Acceleration, type = StatModifierType.Percent, value = -0.25f }
            } : Array.Empty<GroundSurface.Modifier>();
            surface.Configure(
                modifiers,
                previewKind == RoomCurseKind.Lava ? damagePerTick : 0f,
                damageRepeatSeconds,
                groundedGraceSeconds,
                previewKind == RoomCurseKind.Lava ? previewDamageGroup : null);
            surface.SetAudioKind(previewKind == RoomCurseKind.Ice
                ? SurfaceAudioKind.Ice
                : previewKind == RoomCurseKind.Tar ? SurfaceAudioKind.Tar : SurfaceAudioKind.Lava);
            surface.enabled = true;
        }
    }

    void Deactivate()
    {
        if (!active) return;
        active = false;
        elapsed = 0f;
        overlay?.Hide();
        foreach (var surface in previewSurfaces)
            if (surface != null) surface.enabled = false;
        previewDamageGroup.Clear();
        Debug.Log("Superficie de prueba desactivada.", this);
    }

    void OnDestroy()
    {
        overlay?.Dispose();
        foreach (var surface in previewSurfaces)
            if (surface != null) Destroy(surface);
        previewDamageGroup.Clear();
        previewSurfaces.Clear();
    }

    void OnGUI()
    {
        if (!showDiagnostics) return;
        string status;
        if (iceOverlayMaterial == null && tarOverlayMaterial == null && lavaOverlayMaterial == null)
            status = "SUPERFICIES: faltan los materiales";
        else if (!initialized)
            status = "LAVA TEST: inicializando...";
        else if (renderers.Count == 0)
            status = "LAVA TEST: no se detectaron pisos";
        else if (active)
            status = $"{previewKind.ToString().ToUpperInvariant()}  {pixelationMode.ToString().ToUpperInvariant()}  {Mathf.Max(0f, activeSeconds - elapsed):0.0}s  |  H hielo  B brea  L lava";
        else
            status = $"H: HIELO  |  B: BREA  |  L: LAVA  |  O: pixelado ({pixelationMode})  |  pisos: {renderers.Count}";

        var previousColor = GUI.color;
        GUI.color = active ? new Color(1f, 0.35f, 0.12f) : Color.white;
        GUI.Box(new Rect(16f, 16f, 650f, 34f), status);
        if (active)
        {
            string layerStatus = pixelationMode == LavaPixelationMode.None
                ? "Layer original"
                : $"Layer: {pixelationMode} {overlay?.CountObjectsOnPixelationLayer() ?? 0}/{overlay?.LayerObjectCount ?? 0}";
            string playerStatus = previewPlayer == null
                ? "Jugador no encontrado"
                : $"Suelo: {(previewPlayer.IsGrounded ? "SI" : "NO")}  Sensor: {(previewSensor != null ? "SI" : "NO")}  Vida: {(previewHealth != null ? $"{previewHealth.CurrentHealth:0.##}/{previewHealth.MaxHealth:0.##}" : "sin PlayerHealth")}";
            GUI.Box(new Rect(16f, 52f, 650f, 34f), $"{layerStatus}  |  superficies: {previewSurfaces.Count}  |  {playerStatus}");
        }
        if (initialized && renderers.Count > 0)
        {
            float buttonY = active ? 90f : 54f;
            if (GUI.Button(new Rect(16f, buttonY, 110f, 28f), "NORMAL")) Deactivate();
            if (GUI.Button(new Rect(132f, buttonY, 110f, 28f), "HIELO")) Activate(RoomCurseKind.Ice);
            if (GUI.Button(new Rect(248f, buttonY, 110f, 28f), "BREA")) Activate(RoomCurseKind.Tar);
            if (GUI.Button(new Rect(364f, buttonY, 110f, 28f), "LAVA")) Activate(RoomCurseKind.Lava);
            if (GUI.Button(new Rect(480f, buttonY, 150f, 28f), "CAMBIAR PIXELADO"))
                CyclePixelation();
        }
        GUI.color = previousColor;
    }

}
