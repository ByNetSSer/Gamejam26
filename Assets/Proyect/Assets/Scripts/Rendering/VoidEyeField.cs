using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[DefaultExecutionOrder(150)]
public sealed class VoidEyeField : MonoBehaviour
{
    const string MaterialResource = "VoidEyes/VoidEyeMaterial";
    const float EyeAspect = 77f / 64f;

    [Header("Distribución")]
    [SerializeField, Range(8, 96)] int eyeCount = 36;
    [SerializeField, Range(1, 6)] int depthLayers = 3;
    [SerializeField, Min(10f)] float minimumRadius = 48f;
    [SerializeField, Min(0f)] float geometryClearance = 18f;
    [SerializeField, Min(1f)] float layerSpacing = 34f;
    [SerializeField, Min(0f)] float radiusJitter = 7f;
    [SerializeField] Vector2 eyeHeightRange = new Vector2(5f, 11f);
    [SerializeField] int randomSeed = 7319;

    [Header("Seguimiento fuera de salas")]
    [Tooltip("Mantiene el campo alrededor del jugador cuando abandona el volumen seguro de las salas.")]
    [SerializeField] bool followOutsideRooms = true;
    [Tooltip("Margen adicional alrededor de las salas antes de considerar que el jugador salió.")]
    [SerializeField, Min(0f)] float outsideRoomMargin = 12f;
    [Tooltip("Tiempo de suavizado al mover el centro entre salas o hacia el jugador.")]
    [SerializeField, Min(0f)] float recenterSmoothTime = 0.2f;
    [Tooltip("Objetivo opcional para escenas de prueba sin RoomManager.")]
    [SerializeField] Transform followTargetOverride;
    [Tooltip("Geometría opcional para escenas de prueba sin generación de salas.")]
    [SerializeField] Transform[] staticGeometryRoots;

    [Header("Parpadeo")]
    [SerializeField] Vector2 blinkIntervalRange = new Vector2(4f, 13f);
    [SerializeField, Min(0.1f)] float blinkDuration = 1.8f;
    [Tooltip("Cantidad aproximada de ojos visibles que el sistema mantiene en fases distintas de parpadeo.")]
    [SerializeField, Range(1, 4)] int guaranteedVisibleBlinks = 2;

    [Header("Fondo")]
    [SerializeField] bool configureMainCamera = true;
    [Tooltip("Usa el material de Render Settings como fondo y dibuja los ojos delante. Si no hay material, usa el color sólido.")]
    [SerializeField] bool useSceneSkybox;
    [SerializeField] Color backgroundColor = new Color(0.004f, 0.006f, 0.005f, 1f);
    [SerializeField] Color globalTint = new Color(0.82f, 0.84f, 0.74f, 0.92f);

    RoomManager rooms;
    RoomDefinition cachedPrevious;
    RoomDefinition cachedCurrent;
    RoomDefinition cachedNext;
    GameObject visualRoot;
    Mesh eyeMesh;
    Material eyeMaterial;
    MeshRenderer eyeRenderer;
    Camera configuredCamera;
    Bounds activeBounds;
    bool hasActiveBounds;
    Vector3 anchoredCenter;
    Vector3 centerVelocity;
    bool visualCenterInitialized;
    readonly List<Vector3> localEyeCenters = new List<Vector3>();
    readonly List<int> visibleBlinkCandidates = new List<int>();
    readonly float[] forcedBlinkIndices = { -1f, -1f, -1f, -1f };
    readonly float[] forcedBlinkStarts = { -1000f, -1000f, -1000f, -1000f };
    System.Random blinkRandom;
    int nextForcedBlinkSlot;
    float nextForcedBlinkAt;
    bool forcedBlinksInitialized;
    bool outsideRooms;
    bool wasOutsideRooms;

    public void ConfigureStandalone(Transform followTarget, params Transform[] geometryRoots)
    {
        followTargetOverride = followTarget;
        staticGeometryRoots = geometryRoots;
        rooms = GetComponent<RoomManager>();
        if (Application.isPlaying) RefreshLayout();
    }

    void Awake()
    {
        rooms = GetComponent<RoomManager>();
        EnsureVisuals();
        RefreshLayout();
        ConfigureCamera();
    }

    void OnEnable()
    {
        if (eyeRenderer != null) eyeRenderer.enabled = true;
    }

    void LateUpdate()
    {
        ConfigureCamera();
        if (rooms != null && (cachedPrevious != rooms.PreviousRoom || cachedCurrent != rooms.CurrentRoom || cachedNext != rooms.NextRoom))
            RefreshLayout();
        UpdateAnimationTime();
        UpdateVisualCenter();
        UpdateGuaranteedBlinks();
    }

    void OnDisable()
    {
        if (eyeRenderer != null) eyeRenderer.enabled = false;
    }

    void OnDestroy()
    {
        DestroyRuntimeObject(eyeMaterial);
        DestroyRuntimeObject(eyeMesh);
        if (visualRoot != null) DestroyRuntimeObject(visualRoot);
    }

    void OnValidate()
    {
        eyeCount = Mathf.Clamp(eyeCount, 8, 96);
        depthLayers = Mathf.Clamp(depthLayers, 1, 6);
        minimumRadius = Mathf.Max(10f, minimumRadius);
        geometryClearance = Mathf.Max(0f, geometryClearance);
        layerSpacing = Mathf.Max(1f, layerSpacing);
        radiusJitter = Mathf.Max(0f, radiusJitter);
        outsideRoomMargin = Mathf.Max(0f, outsideRoomMargin);
        recenterSmoothTime = Mathf.Max(0f, recenterSmoothTime);
        eyeHeightRange = OrderedPositive(eyeHeightRange, 0.1f);
        blinkIntervalRange = OrderedPositive(blinkIntervalRange, blinkDuration + 0.1f);
        blinkDuration = Mathf.Max(0.1f, blinkDuration);
        guaranteedVisibleBlinks = Mathf.Clamp(guaranteedVisibleBlinks, 1, 4);

        if (!Application.isPlaying || !isActiveAndEnabled) return;
        EnsureVisuals();
        RefreshLayout();
        ApplyMaterialSettings();
        ConfigureCamera(true);
    }

    void EnsureVisuals()
    {
        if (visualRoot != null) return;

        var template = Resources.Load<Material>(MaterialResource);
        if (template == null)
        {
            Debug.LogError("No se encontró Resources/" + MaterialResource + ".mat para los ojos del vacío.", this);
            enabled = false;
            return;
        }

        visualRoot = new GameObject("Ojos del vacío (runtime)");
        visualRoot.hideFlags = HideFlags.DontSave;

        var filter = visualRoot.AddComponent<MeshFilter>();
        eyeRenderer = visualRoot.AddComponent<MeshRenderer>();
        eyeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        eyeRenderer.receiveShadows = false;
        eyeRenderer.lightProbeUsage = LightProbeUsage.Off;
        eyeRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        eyeRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        eyeRenderer.allowOcclusionWhenDynamic = false;

        eyeMesh = new Mesh { name = "Campo combinado de ojos" };
        eyeMesh.MarkDynamic();
        filter.sharedMesh = eyeMesh;

        eyeMaterial = Instantiate(template);
        eyeMaterial.name = template.name + " (runtime)";
        eyeRenderer.sharedMaterial = eyeMaterial;
        ApplyMaterialSettings();
    }

    void ApplyMaterialSettings()
    {
        if (eyeMaterial == null) return;
        eyeMaterial.SetColor("_Color", globalTint);
        eyeMaterial.SetFloat("_BlinkDuration", blinkDuration);
        ApplyForcedBlinkSettings();
    }

    void UpdateAnimationTime()
    {
        if (eyeMaterial != null) eyeMaterial.SetFloat("_AnimationTime", Time.unscaledTime);
    }

    void ConfigureCamera(bool force = false)
    {
        if (!configureMainCamera) return;
        var main = Camera.main;
        if (main == null) return;
        bool hasSkybox = useSceneSkybox && RenderSettings.skybox != null;
        CameraClearFlags desiredClearFlags = hasSkybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        bool backgroundMatches = hasSkybox || main.backgroundColor == backgroundColor;
        if (!force && configuredCamera == main && main.clearFlags == desiredClearFlags && backgroundMatches)
            return;
        configuredCamera = main;
        configuredCamera.clearFlags = desiredClearFlags;
        if (!hasSkybox) configuredCamera.backgroundColor = backgroundColor;
    }

    void RefreshLayout()
    {
        EnsureVisuals();
        if (eyeMesh == null || visualRoot == null) return;

        if (rooms != null)
        {
            cachedPrevious = rooms.PreviousRoom;
            cachedCurrent = rooms.CurrentRoom;
            cachedNext = rooms.NextRoom;
        }

        hasActiveBounds = TryGetActiveBounds(out activeBounds);
        Vector3 center;
        float baseRadius;
        float halfHeight = eyeHeightRange.y * 0.5f;
        float maximumHalfExtent = halfHeight * Mathf.Sqrt(1f + EyeAspect * EyeAspect);
        float visualSafety = radiusJitter + maximumHalfExtent;
        if (hasActiveBounds)
        {
            center = activeBounds.center;
            baseRadius = Mathf.Max(minimumRadius + visualSafety,
                activeBounds.extents.magnitude + geometryClearance + visualSafety);
        }
        else
        {
            var target = ResolveFollowTarget();
            center = target != null ? target.position : transform.position;
            baseRadius = minimumRadius + visualSafety;
        }

        anchoredCenter = center;
        if (!visualCenterInitialized)
        {
            visualRoot.transform.SetPositionAndRotation(center, Quaternion.identity);
            visualCenterInitialized = true;
        }
        BuildCombinedMesh(baseRadius);
    }

    bool TryGetActiveBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (rooms != null)
        {
            IncludeTransformBounds(rooms.PreviousRoom != null ? rooms.PreviousRoom.transform : null, ref bounds, ref found);
            IncludeTransformBounds(rooms.CurrentRoom != null ? rooms.CurrentRoom.transform : null, ref bounds, ref found);
            IncludeTransformBounds(rooms.NextRoom != null ? rooms.NextRoom.transform : null, ref bounds, ref found);
        }
        else if (staticGeometryRoots != null)
        {
            foreach (var root in staticGeometryRoots) IncludeTransformBounds(root, ref bounds, ref found);
        }
        return found;
    }

    static void IncludeTransformBounds(Transform root, ref Bounds bounds, ref bool found)
    {
        if (root == null) return;
        bool foundGeometry = false;
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
            foundGeometry = true;
        }
        if (!foundGeometry)
        {
            var fallback = new Bounds(root.position, Vector3.one);
            if (!found) { bounds = fallback; found = true; }
            else bounds.Encapsulate(fallback);
        }
    }

    Transform ResolveFollowTarget()
    {
        if (followTargetOverride != null) return followTargetOverride;
        if (rooms != null && rooms.Player != null) return rooms.Player.transform;
        var main = Camera.main;
        return main != null ? main.transform : null;
    }

    void UpdateVisualCenter()
    {
        if (visualRoot == null) return;
        Vector3 desiredCenter = anchoredCenter;
        if (!followOutsideRooms)
        {
            outsideRooms = false;
            MoveVisualCenter(desiredCenter);
            return;
        }

        var target = ResolveFollowTarget();
        if (target == null) return;
        if (!hasActiveBounds)
        {
            outsideRooms = true;
            MoveVisualCenter(target.position);
            return;
        }

        Vector3 closestPoint = activeBounds.ClosestPoint(target.position);
        float outsideDistance = Vector3.Distance(closestPoint, target.position);
        outsideRooms = outsideDistance > 0.01f;
        if (outsideDistance > 0f)
        {
            float blend = outsideRoomMargin <= 0f
                ? 1f
                : Mathf.SmoothStep(0f, 1f, outsideDistance / outsideRoomMargin);
            desiredCenter = Vector3.Lerp(anchoredCenter, target.position, blend);
        }
        MoveVisualCenter(desiredCenter);
    }

    void MoveVisualCenter(Vector3 desiredCenter)
    {
        if (!visualCenterInitialized || recenterSmoothTime <= 0f)
        {
            visualRoot.transform.position = desiredCenter;
            centerVelocity = Vector3.zero;
            visualCenterInitialized = true;
            return;
        }

        visualRoot.transform.position = Vector3.SmoothDamp(
            visualRoot.transform.position,
            desiredCenter,
            ref centerVelocity,
            recenterSmoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime);
    }

    void UpdateGuaranteedBlinks()
    {
        if (eyeMaterial == null || visualRoot == null || localEyeCenters.Count == 0) return;
        if (blinkRandom == null) blinkRandom = new System.Random(randomSeed ^ 0x5EEDB17);

        float now = Time.unscaledTime;
        bool enteredVoid = outsideRooms && !wasOutsideRooms;
        if (!forcedBlinksInitialized || enteredVoid) InitializeVisibleBlinks(now);

        float cadence = blinkDuration / Mathf.Max(1, guaranteedVisibleBlinks);
        int guard = 0;
        while (now >= nextForcedBlinkAt && guard++ < 4)
        {
            ScheduleVisibleBlink(nextForcedBlinkAt);
            nextForcedBlinkAt += cadence;
        }
        wasOutsideRooms = outsideRooms;
    }

    void InitializeVisibleBlinks(float now)
    {
        nextForcedBlinkSlot = 0;
        for (int i = 0; i < forcedBlinkIndices.Length; i++)
        {
            forcedBlinkIndices[i] = -1f;
            forcedBlinkStarts[i] = -1000f;
        }

        int count = Mathf.Clamp(guaranteedVisibleBlinks, 1, forcedBlinkIndices.Length);
        for (int i = 0; i < count; i++)
        {
            // El desplazamiento evita que dos ojos queden en fotogramas espejo
            // (por ejemplo 25 % y 75 % producen la misma apertura del párpado).
            float progress = (i + 0.3f) / count;
            ScheduleVisibleBlink(now - blinkDuration * progress);
        }

        nextForcedBlinkAt = now + blinkDuration / (count * 2f);
        forcedBlinksInitialized = true;
        ApplyForcedBlinkSettings();
    }

    void ScheduleVisibleBlink(float startTime)
    {
        int eyeIndex = SelectVisibleEye(Time.unscaledTime);
        forcedBlinkIndices[nextForcedBlinkSlot] = eyeIndex;
        forcedBlinkStarts[nextForcedBlinkSlot] = startTime;
        nextForcedBlinkSlot = (nextForcedBlinkSlot + 1) % forcedBlinkIndices.Length;
        ApplyForcedBlinkSettings();
    }

    int SelectVisibleEye(float now)
    {
        var camera = Camera.main;
        visibleBlinkCandidates.Clear();
        if (camera != null)
        {
            Vector3 origin = camera.transform.position;
            for (int i = 0; i < localEyeCenters.Count; i++)
            {
                if (IsForcedBlinkActive(i, now)) continue;
                Vector3 worldCenter = visualRoot.transform.TransformPoint(localEyeCenters[i]);
                Vector3 viewport = camera.WorldToViewportPoint(worldCenter);
                if (viewport.z <= 0f || viewport.x < 0.04f || viewport.x > 0.96f || viewport.y < 0.04f || viewport.y > 0.96f)
                    continue;

                Vector3 toEye = worldCenter - origin;
                float distance = toEye.magnitude;
                if (distance > 0.1f && Physics.Raycast(origin, toEye / distance, distance - 0.1f, ~0, QueryTriggerInteraction.Ignore))
                    continue;
                visibleBlinkCandidates.Add(i);
            }
        }

        if (visibleBlinkCandidates.Count > 0)
            return visibleBlinkCandidates[blinkRandom.Next(visibleBlinkCandidates.Count)];

        int fallback = blinkRandom.Next(localEyeCenters.Count);
        for (int i = 0; i < localEyeCenters.Count; i++)
        {
            int candidate = (fallback + i) % localEyeCenters.Count;
            if (!IsForcedBlinkActive(candidate, now)) return candidate;
        }
        return fallback;
    }

    bool IsForcedBlinkActive(int eyeIndex, float now)
    {
        for (int i = 0; i < forcedBlinkIndices.Length; i++)
        {
            if (Mathf.Abs(forcedBlinkIndices[i] - eyeIndex) > 0.25f) continue;
            float progress = (now - forcedBlinkStarts[i]) / Mathf.Max(0.01f, blinkDuration);
            if (progress >= 0f && progress < 1f) return true;
        }
        return false;
    }

    void ApplyForcedBlinkSettings()
    {
        if (eyeMaterial == null) return;
        eyeMaterial.SetVector("_ForcedEyeIndices", new Vector4(
            forcedBlinkIndices[0], forcedBlinkIndices[1], forcedBlinkIndices[2], forcedBlinkIndices[3]));
        eyeMaterial.SetVector("_ForcedBlinkStarts", new Vector4(
            forcedBlinkStarts[0], forcedBlinkStarts[1], forcedBlinkStarts[2], forcedBlinkStarts[3]));
    }

    void BuildCombinedMesh(float baseRadius)
    {
        var vertices = new List<Vector3>(eyeCount * 4);
        var uv = new List<Vector2>(eyeCount * 4);
        var eyeData = new List<Vector4>(eyeCount * 4);
        var timing = new List<Vector2>(eyeCount * 4);
        var colors = new List<Color32>(eyeCount * 4);
        var indices = new List<int>(eyeCount * 6);
        var random = new System.Random(randomSeed);
        float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
        float maximumExtent = baseRadius;
        localEyeCenters.Clear();

        for (int i = 0; i < eyeCount; i++)
        {
            float vertical = 1f - 2f * ((i + 0.5f) / eyeCount);
            float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - vertical * vertical));
            float angle = goldenAngle * i + Range(random, -0.35f, 0.35f);
            Vector3 direction = new Vector3(Mathf.Cos(angle) * horizontal, vertical, Mathf.Sin(angle) * horizontal);
            int layer = i % depthLayers;
            float radius = baseRadius + layer * layerSpacing + Range(random, -radiusJitter, radiusJitter);
            Vector3 center = direction * radius;
            localEyeCenters.Add(center);

            float distanceScale = Mathf.Pow(radius / Mathf.Max(1f, baseRadius), 0.7f);
            float height = Range(random, eyeHeightRange.x, eyeHeightRange.y) * distanceScale;
            float halfHeight = height * 0.5f;
            float halfWidth = halfHeight * EyeAspect;
            float roll = Range(random, -Mathf.PI, Mathf.PI);
            float period = Range(random, blinkIntervalRange.x, blinkIntervalRange.y);
            float phase = Mathf.Repeat((i + 0.5f) / eyeCount * period
                + Range(random, -blinkDuration * 0.2f, blinkDuration * 0.2f), period);
            byte alpha = (byte)Mathf.RoundToInt(Range(random, 0.52f, 1f) * 255f);
            Color32 color = new Color32(255, 255, 255, alpha);
            int first = vertices.Count;

            AddVertex(center, new Vector2(0f, 0f), halfWidth, halfHeight, roll, i, phase, period, color, vertices, uv, eyeData, timing, colors);
            AddVertex(center, new Vector2(1f, 0f), halfWidth, halfHeight, roll, i, phase, period, color, vertices, uv, eyeData, timing, colors);
            AddVertex(center, new Vector2(1f, 1f), halfWidth, halfHeight, roll, i, phase, period, color, vertices, uv, eyeData, timing, colors);
            AddVertex(center, new Vector2(0f, 1f), halfWidth, halfHeight, roll, i, phase, period, color, vertices, uv, eyeData, timing, colors);

            indices.Add(first); indices.Add(first + 2); indices.Add(first + 1);
            indices.Add(first); indices.Add(first + 3); indices.Add(first + 2);
            maximumExtent = Mathf.Max(maximumExtent, radius + Mathf.Max(halfWidth, halfHeight));
        }

        eyeMesh.Clear();
        eyeMesh.SetVertices(vertices);
        eyeMesh.SetUVs(0, uv);
        eyeMesh.SetUVs(1, eyeData);
        eyeMesh.SetUVs(2, timing);
        eyeMesh.SetColors(colors);
        eyeMesh.SetTriangles(indices, 0, true);
        eyeMesh.bounds = new Bounds(Vector3.zero, Vector3.one * maximumExtent * 2f);
    }

    static void AddVertex(Vector3 center, Vector2 corner, float halfWidth, float halfHeight, float roll, float eyeIndex,
        float phase, float period, Color32 color, List<Vector3> vertices, List<Vector2> uv,
        List<Vector4> eyeData, List<Vector2> timing, List<Color32> colors)
    {
        vertices.Add(center);
        uv.Add(corner);
        eyeData.Add(new Vector4(halfWidth, halfHeight, roll, eyeIndex));
        timing.Add(new Vector2(phase, period));
        colors.Add(color);
    }

    static float Range(System.Random random, float minimum, float maximum)
        => Mathf.Lerp(minimum, maximum, (float)random.NextDouble());

    static Vector2 OrderedPositive(Vector2 value, float minimum)
    {
        float low = Mathf.Max(minimum, Mathf.Min(value.x, value.y));
        float high = Mathf.Max(low, Mathf.Max(value.x, value.y));
        return new Vector2(low, high);
    }

    static void DestroyRuntimeObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }

}
