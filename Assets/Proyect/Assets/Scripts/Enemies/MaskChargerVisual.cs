using UnityEngine;

// Animated billboard for the MaskCharger in the gameplay scene. The original model
// stays available and is restored when this component is disabled.
[RequireComponent(typeof(MaskCharger), typeof(MeshRenderer))]
public sealed class MaskChargerVisual : MonoBehaviour
{
    [SerializeField] Sprite[] idleFrames;
    [SerializeField] Sprite[] warningFrames;
    [SerializeField] Sprite[] attackFrames;
    [SerializeField] Material spriteMaterial;
    [SerializeField, Min(0.1f)] float framesPerSecond = 12f;
    [SerializeField, Min(0.1f)] float height = 2f;
    MaskCharger enemy;
    MeshRenderer model, image;
    Transform imageTransform;
    Mesh quad;
    MaterialPropertyBlock properties;
    Camera view;
    bool modelWasEnabled;
    MaskChargerState lastState;
    int lastFrame = -1;
    float elapsed;
    static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    void OnEnable()
    {
        enemy = GetComponent<MaskCharger>();
        model = GetComponent<MeshRenderer>();
        if (spriteMaterial == null || idleFrames == null || idleFrames.Length == 0) return;
        modelWasEnabled = model.enabled;
        model.enabled = false;
        var existing = transform.Find("Mask sprites (runtime)");
        if (existing != null) { existing.gameObject.SetActive(false); Destroy(existing.gameObject); }
        var go = new GameObject("Mask sprites (runtime)");
        go.layer = gameObject.layer;
        imageTransform = go.transform;
        imageTransform.SetParent(transform, false);
        quad = new Mesh { name = "Mask sprite quad" };
        quad.vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0), new Vector3(0.5f, 0.5f, 0), new Vector3(-0.5f, 0.5f, 0) };
        quad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        quad.triangles = new[] { 0, 2, 1, 0, 3, 2 };
        quad.RecalculateBounds();
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        image = go.AddComponent<MeshRenderer>();
        image.sharedMaterial = spriteMaterial;
        image.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        image.receiveShadows = false;
        properties = new MaterialPropertyBlock();
        lastState = enemy.State;
        elapsed = 0f;
        lastFrame = -1;
        ApplyFrame();
    }
    void OnDisable()
    {
        if (imageTransform == null) return;
        imageTransform.gameObject.SetActive(false);
        Destroy(imageTransform.gameObject);
        if (quad != null) Destroy(quad);
        if (model != null) model.enabled = modelWasEnabled;
        imageTransform = null;
        image = null;
    }
    void LateUpdate()
    {
        if (image == null) return;
        if (lastState != enemy.State) { lastState = enemy.State; elapsed = 0f; lastFrame = -1; }
        else elapsed += Time.deltaTime;
        ApplyFrame();
        if (view == null && enemy.TargetPlayer != null) view = enemy.TargetPlayer.GetComponentInChildren<Camera>();
        if (view == null) view = Camera.main;
        if (view != null) imageTransform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
    }
    void ApplyFrame()
    {
        Sprite[] frames = lastState == MaskChargerState.Telegraph ? warningFrames :
            (lastState == MaskChargerState.Charging || lastState == MaskChargerState.Braking) ? attackFrames : idleFrames;
        if (frames == null || frames.Length == 0) frames = idleFrames;
        int frame = Mathf.FloorToInt(elapsed * Mathf.Max(0.1f, framesPerSecond)) % frames.Length;
        if (frame == lastFrame || frames[frame] == null) return;
        lastFrame = frame;
        Sprite sprite = frames[frame];
        Texture texture = sprite.texture;
        Rect rect = sprite.rect;
        imageTransform.localScale = new Vector3(height * rect.width / rect.height, height, 1f);
        image.GetPropertyBlock(properties);
        properties.SetTexture(BaseMap, texture);
        properties.SetVector(BaseMapST, new Vector4(rect.width / texture.width, rect.height / texture.height, rect.x / texture.width, rect.y / texture.height));
        image.SetPropertyBlock(properties);
    }
}
