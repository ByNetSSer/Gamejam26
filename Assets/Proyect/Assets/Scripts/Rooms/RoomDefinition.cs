using UnityEngine;

// A room is geometry plus markers. The first room does not need an entry marker.
public sealed class RoomDefinition : MonoBehaviour
{
    [Header("Entrada (no necesaria en la primera sala)")]
    [SerializeField] RoomConnection entryType;
    [SerializeField] Transform entryAnchor;
    [Header("Salida")]
    [SerializeField] RoomConnection exitType;
    [SerializeField] Transform exitMarker;
    [SerializeField, Min(0.1f)] float openingWidth = 3f;
    [SerializeField, Min(0.1f)] float openingHeightOrDepth = 3f;
    [SerializeField, Min(0f)] float exitClearance = 1.1f;
    [Header("Solo primera sala")]
    [SerializeField] Transform playerStart;
    [Header("Opcional: salida ya configurada en el prefab")]
    [SerializeField] RoomExit exit;

    public RoomConnection EntryType => GetComponent<RoomAutoLayout>() != null ? GetComponent<RoomAutoLayout>().Entry : entryType;
    public Transform EntryAnchor => entryAnchor;
    public Transform PlayerStart => playerStart;
    public RoomExit Exit => exit;
    public RoomConnection ExitType => exit != null ? exit.Connection : GetComponent<RoomAutoLayout>() != null ? GetComponent<RoomAutoLayout>().Exit : exitType;
    public Transform ExitMarker => exit != null ? exit.Anchor : exitMarker;
    public bool IsConfigured => GetComponent<RoomAutoLayout>() != null || MarkerValid(entryAnchor) && MarkerValid(ExitMarker);
    void Awake()
    {
        var auto = GetComponent<RoomAutoLayout>();
        if (auto != null && !auto.Configure(this)) Debug.LogError("No se pudieron configurar las conexiones de " + name, this);
    }
    public bool IsFinalRoomConfigured => EntryType == RoomConnection.Hatch && MarkerValid(entryAnchor) && ExitMarker == null;
    public bool IsStartingRoomConfigured => playerStart != null && MarkerValid(ExitMarker) && ExitType == RoomConnection.Hatch;
    static bool MarkerValid(Transform marker)
    {
        if (marker == null || !marker.gameObject.activeSelf) return false;
        Vector3 scale = marker.lossyScale;
        return Vector3.Dot(marker.up, Vector3.up) > 0.999f &&
            Mathf.Abs(scale.x - 1f) < 0.001f && Mathf.Abs(scale.y - 1f) < 0.001f && Mathf.Abs(scale.z - 1f) < 0.001f;
    }
    public bool CanPrepare(ImpactTarget hatchPrefab, RoomDoor doorPrefab)
    {
        if (ExitMarker == null) return GetComponent<RoomAutoLayout>() != null && hatchPrefab != null && hatchPrefab.IsHatch;
        if (ExitType != RoomConnection.Hatch) return exit != null && exit.Door != null || doorPrefab != null;
        var assigned = exit != null ? exit.Hatch : null;
        return assigned != null ? assigned.IsHatch : hatchPrefab != null && hatchPrefab.IsHatch;
    }
    public void Configure(RoomConnection entry, Transform anchor, Transform start, RoomExit output)
    { entryType = entry; entryAnchor = anchor; playerStart = start; exit = output; }
    public void ConfigureMarkers(RoomConnection entry, Transform entryPoint, Transform start, RoomConnection output, Transform exitPoint)
    { entryType = entry; entryAnchor = entryPoint; playerStart = start; exitType = output; exitMarker = exitPoint; exit = null; }
    public bool Prepare(ImpactTarget hatchPrefab, RoomDoor doorPrefab)
    {
        if (!CanPrepare(hatchPrefab, doorPrefab)) return false;
        if (exit == null)
        {
            exit = exitMarker.GetComponent<RoomExit>();
            if (exit == null) exit = exitMarker.gameObject.AddComponent<RoomExit>();
            exit.Configure(exitType, exitMarker, null, null);
            var gate = exitMarker.GetComponent<RoomGateLink>();
            if (gate != null) exit.SetDoor(gate.Door);
            exit.SetOpening(gate != null ? gate.Width : openingWidth, gate != null ? gate.Height : openingHeightOrDepth, exitClearance);
        }
        return exit.Prepare(hatchPrefab, doorPrefab);
    }
    public void Populate(System.Random random, float spawnChanceMultiplier = 1f)
    {
        foreach (var point in GetComponentsInChildren<RoomSpawnPoint>(true)) point.Spawn(random, spawnChanceMultiplier);
    }
    void OnDrawGizmos()
    {
        if (entryAnchor != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(entryAnchor.position, 0.2f);
            Gizmos.DrawRay(entryAnchor.position, entryAnchor.forward);
        }
        var marker = ExitMarker;
        if (marker != null)
        {
            Gizmos.color = Color.cyan;
            var old = Gizmos.matrix;
            Gizmos.matrix = marker.localToWorldMatrix;
            Gizmos.DrawWireCube(Vector3.zero, ExitType == RoomConnection.Hatch
                ? new Vector3(openingWidth, 0.05f, openingHeightOrDepth)
                : new Vector3(openingWidth, openingHeightOrDepth, 0.05f));
            Gizmos.DrawRay(Vector3.zero, Vector3.forward);
            Gizmos.matrix = old;
        }
    }
    // Optional yaw is for vertical connections; door alignment passes zero.
    public void AlignTo(Transform output, float yawDegrees = 0f)
    {
        Quaternion entryRotation = output.rotation * Quaternion.Euler(0f, yawDegrees, 0f);
        Quaternion rotation = entryRotation * Quaternion.Inverse(Quaternion.Inverse(transform.rotation) * entryAnchor.rotation);
        transform.rotation = rotation;
        transform.position += output.position - entryAnchor.position;
    }
}
