using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Editor-only guides. These are suggested clearances, not collision checks.
public static class RoomSpawnPointGizmos
{
    public static int ValidOptions(SerializedObject data)
    {
        var pool = data.FindProperty("propPool").objectReferenceValue;
        var entries = pool != null ? new SerializedObject(pool).FindProperty("entries") : data.FindProperty("options");
        int count = 0, sizes = data.FindProperty("allowedSizes").intValue;
        if (entries == null) return 0;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            float weight = entry.FindPropertyRelative("weight").floatValue;
            if (entry.FindPropertyRelative("prefab").objectReferenceValue == null || weight <= 0f || float.IsNaN(weight) || float.IsInfinity(weight)) continue;
            if (pool != null && (entry.FindPropertyRelative("size").intValue & sizes) == 0) continue;
            count++;
        }
        return count;
    }
    static string Sizes(int flags)
    {
        var names = new List<string>();
        if ((flags & 1) != 0) names.Add("P");
        if ((flags & 2) != 0) names.Add("M");
        if ((flags & 4) != 0) names.Add("G");
        return names.Count == 0 ? "ningún tamaño" : string.Join("/", names);
    }
    [DrawGizmo(GizmoType.Selected | GizmoType.NonSelected)]
    static void Draw(RoomSpawnPoint point, GizmoType type)
    {
        if (point == null) return;
        bool selected = (type & GizmoType.Selected) != 0;
        if (!point.gameObject.activeInHierarchy && !selected) return;
        var data = new SerializedObject(point);
        var pool = data.FindProperty("propPool").objectReferenceValue;
        int sizes = data.FindProperty("allowedSizes").intValue;
        int valid = ValidOptions(data);
        float chance = Mathf.Clamp01(data.FindProperty("chance").floatValue);
        bool active = point.isActiveAndEnabled;
        Color color = !active ? Color.gray : valid == 0 ? Color.red : new Color(0.25f, 0.9f, 1f);
        var offsets = new List<Vector3>();
        if (pool != null) offsets.Add(data.FindProperty("poolLocalOffset").vector3Value);
        else
        {
            var options = data.FindProperty("options");
            for (int i = 0; i < options.arraySize; i++)
            {
                var entry = options.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("prefab").objectReferenceValue == null || entry.FindPropertyRelative("weight").floatValue <= 0f) continue;
                var offset = entry.FindPropertyRelative("localOffset").vector3Value;
                if (!offsets.Contains(offset)) offsets.Add(offset);
            }
            if (offsets.Count == 0) offsets.Add(Vector3.zero);
        }
        float width = pool == null ? 1.5f : (sizes & 4) != 0 ? 2.5f : (sizes & 2) != 0 ? 1.5f : 0.7f;
        float height = Mathf.Max(0.8f, width);
        var oldMatrix = Gizmos.matrix;
        var oldColor = Gizmos.color;
        var oldHandles = Handles.color;
        foreach (var offset in offsets)
        {
            var position = point.transform.TransformPoint(offset);
            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(point.transform.position, position);
            Gizmos.DrawWireSphere(position, 0.09f);
            Gizmos.matrix = Matrix4x4.TRS(position, point.transform.rotation, Vector3.one);
            Gizmos.DrawWireCube(new Vector3(0, height / 2f, 0), new Vector3(width, height, width));
            Arrow(position, point.transform.forward, Color.blue, 0.9f);
            if (selected)
            {
                Arrow(position, point.transform.right, Color.red, 0.65f);
                Arrow(position, point.transform.up, Color.green, 0.65f);
                Handles.Label(position + point.transform.forward, "+Z: frente del prefab");
            }
            string status = !active ? "INACTIVO" : valid == 0 ? "SIN OPCIONES VÁLIDAS" : $"{chance:P0} base · 0–1 objeto";
            string label = (pool != null ? pool.name + " [" + Sizes(sizes) + "]" : "Lista de opciones") + "\n" + status;
            if (selected) label = point.name + "\n" + label + $"\n{valid} opciones · espacio orientativo {width:F1} × {height:F1} × {width:F1} m\nNo incluye la pila; no comprueba colisiones";
            Handles.Label(position + point.transform.up * (height + 0.15f), label);
        }
        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
        Handles.color = oldHandles;
    }
    static void Arrow(Vector3 origin, Vector3 direction, Color color, float length)
    {
        Handles.color = color;
        Handles.ArrowHandleCap(0, origin, Quaternion.LookRotation(direction), length, EventType.Repaint);
    }
}

[CustomEditor(typeof(RoomSpawnPoint)), CanEditMultipleObjects]
public sealed class RoomSpawnPointInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Cada punto elige como máximo UN objeto. Duplicar puntos aumenta la cantidad. El círculo marca la posición efectiva (incluye offset); azul +Z es el frente, verde +Y arriba y rojo +X derecha. P/M/G: pequeño/mediano/grande. La caja es orientativa: revisar el mayor prefab y dejar altura para sus pilas.", MessageType.Info);
        if (targets.Length != 1) return;
        var point = (RoomSpawnPoint)target;
        var data = new SerializedObject(point);
        if (RoomSpawnPointGizmos.ValidOptions(data) == 0)
            EditorGUILayout.HelpBox("No hay prefabs válidos para este punto. Revisa el catálogo, tamaños permitidos y pesos positivos.", MessageType.Warning);
        var room = point.GetComponentInParent<RoomDefinition>();
        if (room == null) return;
        int active = 0, inactive = 0;
        float expected = 0f;
        foreach (var marker in room.GetComponentsInChildren<RoomSpawnPoint>(true))
        {
            if (!marker.isActiveAndEnabled) { inactive++; continue; }
            var markerData = new SerializedObject(marker);
            if (RoomSpawnPointGizmos.ValidOptions(markerData) == 0) continue;
            active++;
            expected += Mathf.Clamp01(markerData.FindProperty("chance").floatValue);
        }
        EditorGUILayout.HelpBox($"Sala: {active} puntos activos con opciones, {inactive} inactivos. Promedio base: {expected:F1} objetos directos; máximo: {active}. No incluye apilados ni el aumento de probabilidad por torre. Es una expectativa, no una cantidad garantizada.", MessageType.None);
    }
}
