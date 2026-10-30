using UnityEngine;

public sealed class RoomDoor : MonoBehaviour
{
    [SerializeField] Transform hinge;
    [SerializeField] float openAngle = 90f;
    [SerializeField, Min(0.01f)] float degreesPerSecond = 300f;
    bool open;
    float angle;
    Quaternion closedRotation;
    Collider[] solidColliders;
    bool hideOnOpen;
    GameObject externalVisual;
    public void HideVisualWhenOpened() => hideOnOpen = true;
    public void SetExternalVisual(GameObject value) { externalVisual = value; hideOnOpen = true; }
    public bool IsOpen => open;
    public void SetHinge(Transform value) => hinge = value;
    void Awake()
    {
        if (hinge != null) closedRotation = hinge.localRotation;
        solidColliders = GetComponentsInChildren<Collider>(true);
    }
    public void Open()
    {
        if (open) return;
        open = true;
        if (solidColliders == null) solidColliders = GetComponentsInChildren<Collider>(true);
        foreach (var collider in solidColliders) if (collider != null) collider.enabled = false;
        if (hideOnOpen)
        {
            var visualRoot = externalVisual != null ? externalVisual : gameObject;
            foreach (var renderer in visualRoot.GetComponentsInChildren<Renderer>()) renderer.enabled = false;
        }
    }
    void Update()
    {
        if (!open || hinge == null) return;
        angle = Mathf.MoveTowards(angle, openAngle, Mathf.Max(0.01f, degreesPerSecond) * Time.deltaTime);
        hinge.localRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);
    }
}
