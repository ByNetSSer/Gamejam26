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
    }

    [SerializeField] bool faceCamera = true;
    [Header("Aspectos (el primero es el predeterminado)")]
    [SerializeField] Skin[] skins = new Skin[0];
    Skin selectedSkin;
    bool skinSelected;
    MaterialPropertyBlock properties;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    static readonly int BaseMapTexelSize = Shader.PropertyToID("_BaseMap_TexelSize");
    public string SkinName => selectedSkin != null ? selectedSkin.name : string.Empty;

    Renderer targetRenderer;
    Flupin enemy;
    Camera view;
    int originalLayer;
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
        if (selectedSkin == null || targetRenderer == null) return;
        Sprite sprite = state == FlupinState.Approaching && selectedSkin.attack != null
            ? selectedSkin.attack : selectedSkin.idle;
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
            properties.SetTexture(BaseMap, texture);
            properties.SetVector(BaseMapST, uv);
            properties.SetVector(BaseMapTexelSize, new Vector4(1f / texture.width, 1f / texture.height, texture.width, texture.height));
            targetRenderer.SetPropertyBlock(properties, i);
        }
    }
    void LateUpdate()
    {
        if (!faceCamera) return;
        if (view == null && enemy != null && enemy.TargetPlayer != null) view = enemy.TargetPlayer.GetComponentInChildren<Camera>();
        if (view == null) view = Camera.main;
        if (view != null) transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
    }
}
