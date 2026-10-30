using UnityEngine;

[RequireComponent(typeof(Renderer), typeof(Flupin))]
public sealed class FlupinVisual : MonoBehaviour
{
    [SerializeField] bool faceCamera = true;
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
        if (changedLayer && targetRenderer != null && targetRenderer.gameObject.layer == LayerMask.NameToLayer("FlupinOverlay"))
            targetRenderer.gameObject.layer = originalLayer;
        if (changedLayer && targetRenderer != null) targetRenderer.allowOcclusionWhenDynamic = originalAllowOcclusion;
        changedLayer = false;
    }
    void LateUpdate()
    {
        if (!faceCamera) return;
        if (view == null && enemy != null && enemy.TargetPlayer != null) view = enemy.TargetPlayer.GetComponentInChildren<Camera>();
        if (view == null) view = Camera.main;
        if (view != null) transform.rotation = Quaternion.LookRotation(view.transform.forward, view.transform.up);
    }
}
