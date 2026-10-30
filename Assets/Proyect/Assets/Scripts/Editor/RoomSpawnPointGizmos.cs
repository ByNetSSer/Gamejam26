using UnityEditor;
using UnityEngine;

// Scene-view guide; the box indicates suggested clearance, not collision validation.
public static class RoomSpawnPointGizmos
{
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void Draw(RoomSpawnPoint point, GizmoType type)
    {
        if (point == null || !point.gameObject.activeInHierarchy) return;
        var data = new SerializedObject(point);
        var pool = data.FindProperty("propPool");
        int sizes = data.FindProperty("allowedSizes").intValue;
        bool bySize = pool != null && pool.objectReferenceValue != null;
        float width = !bySize ? 1.5f : (sizes & (int)PropSize.Large) != 0 ? 2.5f : (sizes & (int)PropSize.Medium) != 0 ? 1.5f : 0.7f;
        float height = !bySize ? 1.5f : (sizes & (int)PropSize.Large) != 0 ? 2.5f : (sizes & (int)PropSize.Medium) != 0 ? 1.5f : 0.8f;
        Gizmos.color = bySize ? new Color(0.3f, 0.9f, 1f, 0.8f) : new Color(1f, 0.7f, 0.2f, 0.8f);
        var old = Gizmos.matrix;
        Gizmos.matrix = Matrix4x4.TRS(point.transform.position, point.transform.rotation, Vector3.one);
        Gizmos.DrawWireCube(new Vector3(0f, height / 2f, 0f), new Vector3(width, height, width));
        Gizmos.DrawLine(Vector3.zero, Vector3.forward * 0.6f);
        Gizmos.matrix = old;
        if ((type & GizmoType.Selected) != 0) Handles.Label(point.transform.position + Vector3.up * height, point.name + " (espacio orientativo)");
    }
}
