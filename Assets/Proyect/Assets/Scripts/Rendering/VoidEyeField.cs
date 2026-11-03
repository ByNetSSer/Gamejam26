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

    [Header("Parpadeo")]
    [SerializeField] Vector2 blinkIntervalRange = new Vector2(4f, 13f);
    [SerializeField, Min(0.1f)] float blinkDuration = 0.75f;

    [Header("Fondo")]
    [SerializeField] bool configureMainCamera = true;
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
        if (rooms == null) return;
        if (cachedPrevious != rooms.PreviousRoom || cachedCurrent != rooms.CurrentRoom || cachedNext != rooms.NextRoom)
            RefreshLayout();
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
        eyeHeightRange = OrderedPositive(eyeHeightRange, 0.1f);
        blinkIntervalRange = OrderedPositive(blinkIntervalRange, blinkDuration + 0.1f);
        blinkDuration = Mathf.Max(0.1f, blinkDuration);

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
    }

    void ConfigureCamera(bool force = false)
    {
        if (!configureMainCamera) return;
        var main = Camera.main;
        if (main == null) return;
        if (!force && configuredCamera == main && main.clearFlags == CameraClearFlags.SolidColor && main.backgroundColor == backgroundColor)
            return;
        configuredCamera = main;
        configuredCamera.clearFlags = CameraClearFlags.SolidColor;
        configuredCamera.backgroundColor = backgroundColor;
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

        bool hasBounds = TryGetActiveBounds(out Bounds bounds);
        Vector3 center;
        float baseRadius;
        if (hasBounds)
        {
            center = bounds.center;
            baseRadius = Mathf.Max(minimumRadius, bounds.extents.magnitude + geometryClearance);
        }
        else
        {
            var main = Camera.main;
            center = main != null ? main.transform.position : transform.position;
            baseRadius = minimumRadius;
        }

        visualRoot.transform.SetPositionAndRotation(center, Quaternion.identity);
        BuildCombinedMesh(baseRadius);
    }

    bool TryGetActiveBounds(out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        if (rooms == null) return false;
        IncludeRoomBounds(rooms.PreviousRoom, ref bounds, ref found);
        IncludeRoomBounds(rooms.CurrentRoom, ref bounds, ref found);
        IncludeRoomBounds(rooms.NextRoom, ref bounds, ref found);
        return found;
    }

    static void IncludeRoomBounds(RoomDefinition room, ref Bounds bounds, ref bool found)
    {
        if (room == null) return;
        bool foundRoomGeometry = false;
        foreach (var collider in room.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
            foundRoomGeometry = true;
        }
        if (!foundRoomGeometry)
        {
            var fallback = new Bounds(room.transform.position, Vector3.one);
            if (!found) { bounds = fallback; found = true; }
            else bounds.Encapsulate(fallback);
        }
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

        for (int i = 0; i < eyeCount; i++)
        {
            float vertical = 1f - 2f * ((i + 0.5f) / eyeCount);
            float horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - vertical * vertical));
            float angle = goldenAngle * i + Range(random, -0.35f, 0.35f);
            Vector3 direction = new Vector3(Mathf.Cos(angle) * horizontal, vertical, Mathf.Sin(angle) * horizontal);
            int layer = i % depthLayers;
            float radius = baseRadius + layer * layerSpacing + Range(random, -radiusJitter, radiusJitter);
            Vector3 center = direction * radius;

            float distanceScale = Mathf.Pow(radius / Mathf.Max(1f, baseRadius), 0.7f);
            float height = Range(random, eyeHeightRange.x, eyeHeightRange.y) * distanceScale;
            float halfHeight = height * 0.5f;
            float halfWidth = halfHeight * EyeAspect;
            float roll = Range(random, -Mathf.PI, Mathf.PI);
            float period = Range(random, blinkIntervalRange.x, blinkIntervalRange.y);
            float phase = Range(random, 0f, period);
            byte alpha = (byte)Mathf.RoundToInt(Range(random, 0.52f, 1f) * 255f);
            Color32 color = new Color32(255, 255, 255, alpha);
            int first = vertices.Count;

            AddVertex(center, new Vector2(0f, 0f), halfWidth, halfHeight, roll, phase, period, color, vertices, uv, eyeData, timing, colors);
            AddVertex(center, new Vector2(1f, 0f), halfWidth, halfHeight, roll, phase, period, color, vertices, uv, eyeData, timing, colors);
            AddVertex(center, new Vector2(1f, 1f), halfWidth, halfHeight, roll, phase, period, color, vertices, uv, eyeData, timing, colors);
            AddVertex(center, new Vector2(0f, 1f), halfWidth, halfHeight, roll, phase, period, color, vertices, uv, eyeData, timing, colors);

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

    static void AddVertex(Vector3 center, Vector2 corner, float halfWidth, float halfHeight, float roll,
        float phase, float period, Color32 color, List<Vector3> vertices, List<Vector2> uv,
        List<Vector4> eyeData, List<Vector2> timing, List<Color32> colors)
    {
        vertices.Add(center);
        uv.Add(corner);
        eyeData.Add(new Vector4(halfWidth, halfHeight, roll, 0f));
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
