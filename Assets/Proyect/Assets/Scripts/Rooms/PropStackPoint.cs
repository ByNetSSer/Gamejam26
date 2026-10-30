using UnityEngine;

// Place on an empty child of a prop. It is populated only by the room's spawn process.
public sealed class PropStackPoint : MonoBehaviour
{
    [SerializeField] PropSpawnPool pool;
    [SerializeField] PropSize allowedSizes = PropSize.Small | PropSize.Medium;
    [SerializeField, Range(0f, 1f)] float startingChance = 0.8f;
    [SerializeField, Range(0f, 1f)] float chanceDecreasePerLevel = 0.08f;
    [SerializeField, Min(0)] int maximumDepth = 4;
    [SerializeField, Min(0)] int maximumObjects = 4;
    [Tooltip("Coloca el punto sobre el límite superior de los colliders del objeto padre.")]
    [SerializeField] bool useParentColliderTop = true;

    public static void Populate(GameObject root, System.Random random)
    {
        if (root == null || random == null) return;
        // A single budget per root keeps branching markers bounded as well as chains.
        var markers = root.GetComponentsInChildren<PropStackPoint>(true);
        int budget = 0;
        foreach (var marker in markers) budget = Mathf.Max(budget, marker.maximumObjects);
        PopulateMarkers(root, random, 1, ref budget);
    }
    static void PopulateMarkers(GameObject owner, System.Random random, int depth, ref int budget)
    {
        // Capture before creating children, and process only points belonging to this prop.
        var markers = owner.GetComponentsInChildren<PropStackPoint>(true);
        Physics.SyncTransforms();
        bool hasBaseBounds = TryBounds(owner, out var baseBounds);
        foreach (var point in markers)
        {
            if (budget <= 0) break;
            if (point == null || !point.isActiveAndEnabled || point.pool == null || depth > point.maximumDepth) continue;
            float chance = Mathf.Clamp01(point.startingChance - depth * point.chanceDecreasePerLevel);
            if (random.NextDouble() >= chance) continue;
            var prefab = point.pool.Choose(random, point.allowedSizes);
            if (prefab == null) continue;
            Vector3 position = point.transform.position;
            if (point.useParentColliderTop && hasBaseBounds)
            {
                Vector3 offset = point.transform.parent.TransformVector(point.transform.localPosition);
                position = new Vector3(baseBounds.center.x, baseBounds.max.y, baseBounds.center.z) + offset;
            }
            var instance = Instantiate(prefab, position, point.transform.rotation, point.transform);
            Physics.SyncTransforms();
            if (TryBounds(instance, out var childBounds)) instance.transform.position += Vector3.up * (position.y - childBounds.min.y);
            budget--;
            PopulateMarkers(instance, random, depth + 1, ref budget);
        }
    }
    static bool TryBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        bool found = false;
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (!found) { bounds = collider.bounds; found = true; }
            else bounds.Encapsulate(collider.bounds);
        }
        return found;
    }
}
