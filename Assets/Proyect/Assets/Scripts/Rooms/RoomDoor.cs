using UnityEngine;

public sealed class RoomDoor : MonoBehaviour
{
    [SerializeField] Transform hinge;
    [SerializeField, Min(0.1f)] float liftSpeed = 8f;
    bool open, sealedBehind, visualInitialized;
    Collider[] solidColliders;
    GameObject externalVisual;
    Transform visual;
    Vector3 closedLocalPosition, raisedLocalPosition;

    public void HideVisualWhenOpened() { }
    public void SetExternalVisual(GameObject value) { externalVisual = value; visualInitialized = false; }
    public bool IsOpen => open && !sealedBehind;
    public bool IsSealed => sealedBehind;
    public void SetHinge(Transform value) { hinge = value; visualInitialized = false; }
    void Awake() => solidColliders = GetComponentsInChildren<Collider>(true);

    void InitializeVisual()
    {
        if (visualInitialized) return;
        visualInitialized = true;
        visual = externalVisual != null ? externalVisual.transform : hinge;
        if (visual == null)
        {
            var renderer = GetComponentInChildren<Renderer>();
            // Do not move the room marker itself; it defines the connection and blocker.
            if (renderer != null && renderer.transform != transform) visual = renderer.transform;
        }
        if (visual == null) return;
        closedLocalPosition = visual.localPosition;
        var renderers = visual.GetComponentsInChildren<Renderer>(true);
        float height = 2f;
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            height = Mathf.Max(0.5f, bounds.size.y);
        }
        Vector3 worldLift = Vector3.up * (height + 0.25f);
        raisedLocalPosition = closedLocalPosition + (visual.parent != null ? visual.parent.InverseTransformVector(worldLift) : worldLift);
    }
    public void Open()
    {
        if (open || sealedBehind) return;
        InitializeVisual();
        open = true;
        if (solidColliders == null) solidColliders = GetComponentsInChildren<Collider>(true);
        foreach (var collider in solidColliders) if (collider != null) collider.enabled = false;
    }
    public void CloseBehind()
    {
        if (sealedBehind) return;
        InitializeVisual();
        sealedBehind = true;
    }
    void Update()
    {
        if (!open || visual == null) return;
        Vector3 target = sealedBehind ? closedLocalPosition : raisedLocalPosition;
        // Local distance is converted to world units even when the authored gate has a scaled parent.
        float worldDistance = visual.parent != null ? visual.parent.TransformVector(target - visual.localPosition).magnitude : (target - visual.localPosition).magnitude;
        if (worldDistance <= 0.0001f) { visual.localPosition = target; return; }
        visual.localPosition = Vector3.Lerp(visual.localPosition, target, Mathf.Min(1f, Mathf.Max(0.1f, liftSpeed) * Time.deltaTime / worldDistance));
    }
}
