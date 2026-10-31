using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Flupin))]
public sealed class FlupinVisual : MonoBehaviour
{
    [System.Serializable]
    public sealed class Skin
    {
        public string name;
        [Min(0f), Tooltip("Peso relativo: 90, 9 y 1 equivalen a 90 %, 9 % y 1 %.")]
        public float weight = 1f;
        public Sprite idle;
        [Tooltip("Opcional. Si está vacío se mantiene Idle durante el ataque.")]
        public Sprite attack;
        [Tooltip("Fotogramas opcionales de reposo, en orden.")] public Sprite[] idleFrames;
        [Tooltip("Fotogramas opcionales de ataque, en orden.")] public Sprite[] attackFrames;
        [Min(0.1f)] public float framesPerSecond = 12f;
        [Min(0.1f), Tooltip("Anchura visual relativa de esta skin.")] public float visualWidth = 1f;
        [Min(0.1f), Tooltip("Tamaño visual relativo de esta skin.")] public float visualScale = 1f;
    }

    [SerializeField] bool faceCamera = true;
    [Header("Aspectos (el primero es el predeterminado)")]
    [SerializeField] Skin[] skins = new Skin[0];
    Skin selectedSkin;
    bool skinSelected;
    bool attacking;
    float animationTime;
    int displayedFrame = -1;
    MaterialPropertyBlock properties;
    static readonly int VisualScale = Shader.PropertyToID("_VisualScale");
    static readonly int VisualWidth = Shader.PropertyToID("_VisualWidth");
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int BaseMapTexelSize = Shader.PropertyToID("_BaseMap_TexelSize");
    public string SkinName => selectedSkin != null ? selectedSkin.name : string.Empty;

    Renderer targetRenderer;
    Flupin enemy;
    Camera view;
    int originalLayer;
    Bounds originalBounds;
    bool boundsChanged;
    bool changedLayer, originalAllowOcclusion;

    void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        enemy = GetComponent<Flupin>();
    }
    void OnEnable()
    {
        if (targetRenderer == null) targetRenderer = GetComponent<Renderer>();
        if (enemy == null) enemy = GetComponent<Flupin>();
        enemy.StateChanged += OnStateChanged;
        if (!skinSelected) SelectSkin();
        if (selectedSkin != null)
        {
            originalBounds = targetRenderer.localBounds;
            Bounds bounds = originalBounds;
            float width = selectedSkin.visualWidth > 0f ? selectedSkin.visualWidth : 1f;
            float scale = selectedSkin.visualScale > 0f ? selectedSkin.visualScale : 1f;
            bounds.extents = new Vector3(bounds.extents.x * Mathf.Max(1f, width * scale), bounds.extents.y * Mathf.Max(1f, scale), bounds.extents.z);
            targetRenderer.localBounds = bounds;
            boundsChanged = true;
        }
        // Flupin resets to Waiting on enable; component callback order is unspecified.
        OnStateChanged(FlupinState.Waiting);
        int overlayLayer = LayerMask.NameToLayer("FlupinOverlay");
        if (overlayLayer < 0)
        {
            Debug.LogWarning("Flupin necesita la capa FlupinOverlay para dibujarse sobre las paredes.", this);
            return;
        }
        originalLayer = targetRenderer.gameObject.layer;
        originalAllowOcclusion = targetRenderer.allowOcclusionWhenDynamic;
        targetRenderer.allowOcclusionWhenDynamic = false;
        targetRenderer.gameObject.layer = overlayLayer;
        changedLayer = true;
    }
    void OnDisable()
    {
        if (enemy != null) enemy.StateChanged -= OnStateChanged;
        if (changedLayer && targetRenderer != null && targetRenderer.gameObject.layer == LayerMask.NameToLayer("FlupinOverlay"))
            targetRenderer.gameObject.layer = originalLayer;
        if (changedLayer && targetRenderer != null) targetRenderer.allowOcclusionWhenDynamic = originalAllowOcclusion;
        changedLayer = false;
        if (boundsChanged && targetRenderer != null) targetRenderer.localBounds = originalBounds;
        boundsChanged = false;
    }
    void SelectSkin()
    {
        skinSelected = true;
        if (skins == null) return;
        float total = 0f;
        foreach (var skin in skins)
        {
            if (skin == null || skin.idle == null) continue;
            if (selectedSkin == null) selectedSkin = skin;
            total += ValidWeight(skin.weight);
        }
        if (total <= 0f) return; // First valid skin is the fallback.
        float roll = Random.value * total;
        foreach (var skin in skins)
        {
            if (skin == null || skin.idle == null || ValidWeight(skin.weight) <= 0f) continue;
            selectedSkin = skin;
            roll -= ValidWeight(skin.weight);
            if (roll < 0f) break;
        }
    }
    static float ValidWeight(float weight) => float.IsNaN(weight) || float.IsInfinity(weight) ? 0f : Mathf.Max(0f, weight);

    void OnStateChanged(FlupinState state)
    {
        attacking = state == FlupinState.Approaching;
        animationTime = 0f;
        displayedFrame = -1;
        UpdateAnimation();
    }
    void UpdateAnimation()
    {
        if (selectedSkin == null || targetRenderer == null) return;
        Sprite[] frames = attacking ? selectedSkin.attackFrames : selectedSkin.idleFrames;
        Sprite fallback = attacking && selectedSkin.attack != null ? selectedSkin.attack : selectedSkin.idle;
        int frame = frames != null && frames.Length > 0
            ? Mathf.FloorToInt(animationTime * Mathf.Max(0.1f, selectedSkin.framesPerSecond)) % frames.Length : 0;
        if (frame == displayedFrame) return;
        displayedFrame = frame;
        Sprite sprite = frames != null && frames.Length > 0 && frames[frame] != null ? frames[frame] : fallback;
        if (sprite == null) return;
        if (targetRenderer is SpriteRenderer spriteRenderer)
        {
            spriteRenderer.sprite = sprite;
            return;
        }
        // The prefab uses a quad and two materials (image + outline). Override both
        // per instance, without editing shared materials or replacing their shaders.
        var texture = sprite.texture;
        var rect = sprite.rect; // These skins use individual, unpacked PNG sprites.
        var uv = new Vector4(rect.width / texture.width, rect.height / texture.height,
            rect.x / texture.width, rect.y / texture.height);
        if (properties == null) properties = new MaterialPropertyBlock();
        var materials = targetRenderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] == null || !materials[i].HasProperty(BaseMap)) continue;
            properties.Clear();
            targetRenderer.GetPropertyBlock(properties, i);
            if (materials[i].HasProperty(VisualScale)) properties.SetFloat(VisualScale, selectedSkin.visualScale > 0f ? selectedSkin.visualScale : 1f);
            if (materials[i].HasProperty(VisualWidth)) properties.SetFloat(VisualWidth, selectedSkin.visualWidth > 0f ? selectedSkin.visualWidth : 1f);
            properties.SetTexture(BaseMap, texture);
            properties.SetVector(BaseMapST, uv);
            properties.SetVector(BaseMapTexelSize, new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height));
            targetRenderer.SetPropertyBlock(properties, i);
        }
    }
    void LateUpdate()
    {
        animationTime += Time.deltaTime;
        UpdateAnimation();
        if (!faceCamera) return;
        if (view == null && enemy != null && enemy.TargetPlayer != null) view = enemy.TargetPlayer.GetComponentInChildren<Camera>();
        if (view == null) view = Camera.main;
        if (view != null) transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
    }
}
