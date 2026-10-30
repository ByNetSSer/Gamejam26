using System;
using System.Collections.Generic;
using UnityEngine;

public enum RoomSize { Small, Large }

// Runtime adapter for the modular acueducto prefabs; markers are derived from their geometry.
public sealed class RoomAutoLayout : MonoBehaviour
{
    [SerializeField] RoomConnection entry;
    [SerializeField] RoomConnection exit;
    [SerializeField] RoomSize size = RoomSize.Small;
    [Header("Pared y palancas (opcional, experimental)")]
    [Tooltip("Marcador hijo que indica la posición y orientación donde podría aparecer el muro.")]
    [SerializeField] Transform wallSpawnPoint;
    [Tooltip("Marcadores hijos con posibles posiciones de palancas o botones accesibles desde la entrada.")]
    [SerializeField] Transform[] leverSpawnPoints = new Transform[0];
    public RoomConnection Entry => entry;
    public RoomConnection Exit => exit;
    public RoomSize Size => size;
    public Transform WallSpawnPoint => wallSpawnPoint;
    public IReadOnlyList<Transform> LeverSpawnPoints => leverSpawnPoints;

    public bool Configure(RoomDefinition room)
    {
        var floor = FindDirect("Piso");
        var ceiling = FindDirect("Techo");
        if (floor == null || ceiling == null) return false;
        var gates = new List<Transform>();
        foreach (Transform child in transform)
            if (child.gameObject.activeSelf &&
                (child.name == "Entrada" || child.name == "Salida" || child.name.StartsWith("Puerta", StringComparison.OrdinalIgnoreCase)))
                gates.Add(child);
        gates.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        int needed = (entry == RoomConnection.Door ? 1 : 0) + (exit == RoomConnection.Door ? 1 : 0);
        if (gates.Count != needed) { Debug.LogError($"{name}: se esperaban {needed} rejas y se encontraron {gates.Count}.", this); return false; }
        var surfaces = new List<Transform>();
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.name == "Trampilla" && t.childCount == 3) surfaces.Add(t);
        int hatches = (entry == RoomConnection.Hatch ? 1 : 0) + (exit == RoomConnection.Hatch ? 1 : 0);
        if (surfaces.Count != hatches) { Debug.LogError($"{name}: se esperaban {hatches} superficies de trampilla y se encontraron {surfaces.Count}.", this); return false; }
        surfaces.Sort((a, b) => b.position.y.CompareTo(a.position.y));
        var center = BoundsOf(floor).center;
        int gateIndex = 0, hatchIndex = 0;
        Transform entryMarker = entry == RoomConnection.Door ? GateMarker(gates[gateIndex++], floor, center, true) : HatchMarker(surfaces[hatchIndex++], true);
        Transform exitMarker = exit == RoomConnection.Door ? GateMarker(gates[gateIndex], floor, center, false) : HatchMarker(surfaces[hatchIndex], false);
        if (entryMarker == null || exitMarker == null) return false;
        room.ConfigureMarkers(entry, entryMarker, null, exit, exitMarker);
        return true;
    }
    Transform FindDirect(string n) { foreach (Transform t in transform) if (t.name == n) return t; return null; }
    public static Bounds BoundsOf(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return new Bounds(root.position, Vector3.zero);
        var result = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) result.Encapsulate(renderers[i].bounds);
        return result;
    }
    Transform Marker(string name, Vector3 position, Quaternion rotation)
    {
        var marker = new GameObject(name).transform;
        marker.SetParent(transform, false);
        marker.SetPositionAndRotation(position, rotation);
        return marker;
    }
    Transform HatchMarker(Transform surface, bool incoming)
        => CreateHatchMarker(transform, surface, incoming);
    public static Transform CreateHatchMarker(Transform roomRoot, Transform surface, bool incoming)
    {
        var tiles = new List<Transform>();
        foreach (Transform t in surface) tiles.Add(t);
        if (tiles.Count != 3) return null;
        tiles.Sort((a, b) => (BoundsOf(a).center - BoundsOf(surface).center).sqrMagnitude.CompareTo((BoundsOf(b).center - BoundsOf(surface).center).sqrMagnitude));
        var tile = tiles[0];
        var bounds = BoundsOf(tile);
        tile.gameObject.SetActive(false);
        var marker = new GameObject(incoming ? "Entrada trampilla" : "Salida trampilla").transform;
        marker.SetParent(roomRoot, false);
        marker.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y, bounds.center.z), Quaternion.identity);
        if (!incoming) marker.gameObject.AddComponent<RoomHatchVisual>().Tile = tile;
        return marker;
    }
    Transform GateMarker(Transform gate, Transform floor, Vector3 center, bool incoming)
    {
        var bounds = BoundsOf(gate);
        // The direction from a gate to the room's bounding-box center can be
        // diagonal in L-shaped or asymmetric rooms. Use the gate's plane instead.
        Vector3 direction = Vector3.ProjectOnPlane(gate.forward, Vector3.up).normalized;
        // The closest floor tile is more reliable than the overall bounds center
        // when a room bends or the bounding box contains empty space.
        Vector3 nearest = center;
        float nearestDistance = float.PositiveInfinity;
        foreach (var renderer in floor.GetComponentsInChildren<Renderer>())
        {
            Vector3 delta = Vector3.ProjectOnPlane(renderer.bounds.center - bounds.center, Vector3.up);
            if (delta.sqrMagnitude < nearestDistance) { nearestDistance = delta.sqrMagnitude; nearest = renderer.bounds.center; }
        }
        Vector3 outward = Vector3.ProjectOnPlane(bounds.center - nearest, Vector3.up);
        if (Mathf.Abs(Vector3.Dot(direction, outward)) < 0.05f)
            outward = Vector3.ProjectOnPlane(bounds.center - center, Vector3.up);
        if (direction.sqrMagnitude < 0.01f || outward.sqrMagnitude < 0.01f) return null;
        if (Vector3.Dot(direction, outward) < 0f) direction = -direction;
        if (incoming) direction = -direction;
        var marker = Marker(incoming ? "Entrada puerta" : "Salida puerta", bounds.center, Quaternion.LookRotation(direction, Vector3.up));
        if (incoming) gate.gameObject.SetActive(false);
        else
        {
            // Reja models use a negative X scale. Put collision on the unit-scale
            // marker, rather than on that mirrored transform.
            foreach (var original in gate.GetComponentsInChildren<Collider>()) original.enabled = false;
            Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)
            {
                var corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);
                var local = marker.InverseTransformPoint(corner);
                min = Vector3.Min(min, local); max = Vector3.Max(max, local);
            }
            var collider = marker.gameObject.AddComponent<BoxCollider>();
            collider.center = (min + max) * 0.5f;
            collider.size = new Vector3(Mathf.Max(0.05f, max.x - min.x), Mathf.Max(0.05f, max.y - min.y), Mathf.Max(0.05f, max.z - min.z));
            var door = marker.gameObject.AddComponent<RoomDoor>();
            door.SetExternalVisual(gate.gameObject);
            var link = marker.gameObject.AddComponent<RoomGateLink>();
            link.Door = door;
            link.Width = collider.size.x;
            link.Height = collider.size.y;
        }
        return marker;
    }
}

public sealed class RoomGateLink : MonoBehaviour
{
    public RoomDoor Door { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
}

public sealed class RoomHatchVisual : MonoBehaviour
{
    public Transform Tile { get; set; }
}
