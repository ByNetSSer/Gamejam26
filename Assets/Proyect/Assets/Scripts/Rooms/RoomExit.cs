using UnityEngine;

public sealed class RoomExit : MonoBehaviour
{
    [SerializeField] RoomConnection connection;
    [SerializeField] Transform anchor;
    [SerializeField] ImpactTarget hatch;
    [SerializeField] RoomDoor door;
    [SerializeField] GameObject oneWayBlocker;
    [SerializeField, Min(0.1f)] float width = 3f;
    [SerializeField, Min(0.1f)] float heightOrDepth = 3f;
    [Tooltip("Distancia mínima del centro para cerrar después del paso.")]
    [SerializeField, Min(0f)] float clearance = 1.1f;
    public RoomConnection Connection => connection;
    public Transform Anchor => anchor != null ? anchor : transform;
    public ImpactTarget Hatch => hatch;
    public RoomDoor Door => door;
    public void Configure(RoomConnection type, Transform marker, ImpactTarget hatchTarget, GameObject blocker)
    { connection = type; anchor = marker; hatch = hatchTarget; oneWayBlocker = blocker; }
    public void SetDoor(RoomDoor value) => door = value;
    public void SetOpening(float openingWidth, float openingHeightOrDepth, float passClearance)
    { width = Mathf.Max(0.1f, openingWidth); heightOrDepth = Mathf.Max(0.1f, openingHeightOrDepth); clearance = Mathf.Max(0f, passClearance); }
    public bool Prepare(ImpactTarget hatchPrefab, RoomDoor doorPrefab)
    {
        if (connection == RoomConnection.Hatch && hatch == null)
        {
            if (hatchPrefab == null || !hatchPrefab.IsHatch) return false;
            hatch = Instantiate(hatchPrefab, Anchor);
            hatch.transform.localPosition = Vector3.zero;
            hatch.transform.localRotation = Quaternion.identity;
            var visual = Anchor.GetComponent<RoomHatchVisual>();
            if (visual != null && visual.Tile != null)
            {
                var copy = Instantiate(visual.Tile.gameObject, visual.Tile.position, visual.Tile.rotation);
                copy.transform.localScale = visual.Tile.lossyScale;
                copy.transform.SetParent(hatch.transform, true);
                copy.SetActive(true);
                var parts = copy.GetComponentsInChildren<Collider>();
                var box = hatch.GetComponent<BoxCollider>();
                if (box != null && parts.Length > 0)
                {
                    var bounds = parts[0].bounds;
                    for (int i = 1; i < parts.Length; i++) bounds.Encapsulate(parts[i].bounds);
                    var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
                    var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
                    for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
                    {
                        var corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                        var local = hatch.transform.InverseTransformPoint(corner);
                        min = Vector3.Min(min, local); max = Vector3.Max(max, local);
                    }
                    box.center = (min + max) * 0.5f;
                    box.size = max - min;
                    // Match the traversal check and return blocker to the entire tile.
                    width = bounds.size.x;
                    heightOrDepth = bounds.size.z;
                }
                foreach (var collider in parts) collider.enabled = false;
                foreach (var renderer in copy.GetComponentsInChildren<Renderer>())
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var material = materials[i];
                        if (material == null) continue;
                        string property = material.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
                        if (!material.HasProperty(property)) continue;
                        var block = new MaterialPropertyBlock();
                        renderer.GetPropertyBlock(block, i);
                        var color = material.GetColor(property);
                        color.r *= 0.75f; color.g *= 0.75f; color.b *= 0.75f;
                        block.SetColor(property, color);
                        renderer.SetPropertyBlock(block, i);
                    }
                }
                foreach (var renderer in hatch.GetComponents<Renderer>()) renderer.enabled = false;
                hatch.SetFallingVisual(copy);
            }
        }
        if (connection == RoomConnection.Hatch && (hatch == null || !hatch.IsHatch)) return false;
        if (connection == RoomConnection.Door && door == null)
        {
            if (doorPrefab == null) return false;
            door = Instantiate(doorPrefab, Anchor);
            door.transform.localPosition = Vector3.zero;
            door.transform.localRotation = Quaternion.identity;
        }
        if (oneWayBlocker == null)
        {
            oneWayBlocker = new GameObject("Cierre de retorno");
            oneWayBlocker.transform.SetParent(Anchor, false);
            var box = oneWayBlocker.AddComponent<BoxCollider>();
            box.size = connection == RoomConnection.Door
                ? new Vector3(width, heightOrDepth, 0.2f) : new Vector3(width, 0.2f, heightOrDepth);
        }
        oneWayBlocker.SetActive(false);
        return true;
    }
    public bool HasPassed(Vector3 position)
    {
        Vector3 p = Anchor.InverseTransformPoint(position);
        if (connection == RoomConnection.Hatch)
            return hatch != null && hatch.IsBroken && p.y <= -clearance && Mathf.Abs(p.x) <= width * 0.5f && Mathf.Abs(p.z) <= heightOrDepth * 0.5f;
        return door != null && door.IsOpen && p.z >= clearance && Mathf.Abs(p.x) <= width * 0.5f && Mathf.Abs(p.y) <= heightOrDepth * 0.5f;
    }
    public void CloseBehind() { if (oneWayBlocker != null) oneWayBlocker.SetActive(true); }
}
