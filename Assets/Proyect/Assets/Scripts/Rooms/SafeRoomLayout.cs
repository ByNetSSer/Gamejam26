using System;
using System.Collections.Generic;
using UnityEngine;

// The same authored safe-room prefab serves as a start or a final room.
[DisallowMultipleComponent]
[RequireComponent(typeof(RoomDefinition))]
public sealed class SafeRoomLayout : MonoBehaviour
{
    Transform FindChild(string childName)
    {
        foreach (Transform child in transform)
            if (string.Equals(child.name, childName, StringComparison.OrdinalIgnoreCase)) return child;
        return null;
    }
    public bool HasRequiredParts => FindChild("entrada") != null && FindChild("salida") != null && FindChild("spawninicio") != null;
    public bool Configure(RoomDefinition room, bool starting)
    {
        var entry = FindChild("entrada");
        var exit = FindChild("salida");
        var spawn = FindChild("spawninicio");
        if (entry == null || exit == null || spawn == null) return false;
        SetAltarsActive(!starting);
        Transform marker = RoomAutoLayout.CreateHatchMarker(transform, starting ? exit : entry, !starting);
        if (marker == null) return false;
        room.ConfigureMarkers(RoomConnection.Hatch, starting ? null : marker, starting ? spawn : null,
            RoomConnection.Hatch, starting ? marker : null);
        return true;
    }
    public Transform[] GetAltars()
    {
        var result = new List<Transform>();
        foreach (Transform child in transform)
            if (child.name.StartsWith("Altar", StringComparison.OrdinalIgnoreCase)) result.Add(child);
        result.Sort((a, b) => a.localPosition.z.CompareTo(b.localPosition.z));
        return result.ToArray();
    }
    public void SetAltarsActive(bool active)
    {
        foreach (var altar in GetAltars()) altar.gameObject.SetActive(active);
    }
    public Vector3 FloorCenter
    {
        get
        {
            var floor = FindChild("Piso");
            if (floor == null) return transform.position;
            var bounds = RoomAutoLayout.BoundsOf(floor);
            return new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
        }
    }
}
