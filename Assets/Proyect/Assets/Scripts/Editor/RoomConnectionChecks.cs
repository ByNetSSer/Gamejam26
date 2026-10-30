using System;
using UnityEditor;
using UnityEngine;

public static class RoomConnectionChecks
{
    [MenuItem("Tools/Runner/Verificar conexiones de salas")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Ejecuta la verificación fuera de Play Mode.");
        var root = new GameObject("Sala de verificación") { hideFlags = HideFlags.HideAndDontSave };
        var target = new GameObject("Salida de verificación") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            var room = root.AddComponent<RoomDefinition>();
            var entry = Child(root.transform, "Entrada", new Vector3(2f, 6f, -3f), 90f);
            var exit = Child(root.transform, "Salida", new Vector3(-1f, 0f, 4f), -90f);
            var spawn = Child(root.transform, "Inicio", Vector3.up, 0f);
            room.ConfigureMarkers(RoomConnection.Hatch, entry, spawn, RoomConnection.Door, exit);
            target.transform.SetPositionAndRotation(new Vector3(25f, 12f, -13f), Quaternion.Euler(0f, 180f, 0f));
            Check(room.IsConfigured, "marcadores válidos");
            foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
            {
                room.AlignTo(target.transform, yaw);
                Check(Vector3.Distance(entry.position, target.transform.position) < 0.001f, "coincidencia de posición con giro " + yaw);
                Check(Quaternion.Angle(entry.rotation, target.transform.rotation * Quaternion.Euler(0f, yaw, 0f)) < 0.01f, "orientación con giro " + yaw);
            }
            room.AlignTo(target.transform); // Una puerta mantiene la orientación directa.
            var door = exit.gameObject.AddComponent<RoomExit>();
            door.Configure(RoomConnection.Door, exit, null, null);
            var doorPrototype = Child(root.transform, "Prototipo de puerta", Vector3.zero, 0f).gameObject.AddComponent<RoomDoor>();
            Check(door.Prepare(null, doorPrototype), "crear puerta y cierre automático");
            Check(!door.HasPassed(exit.TransformPoint(new Vector3(0f, 0f, 1.2f))), "puerta cerrada bloquea transición");
            door.Door.Open();
            Check(!door.HasPassed(exit.TransformPoint(new Vector3(0f, 0f, 0.5f))), "no cerrar antes de pasar");
            Check(door.HasPassed(exit.TransformPoint(new Vector3(0f, 0f, 1.2f))), "detectar paso de puerta abierta");
            Check(!door.HasPassed(exit.TransformPoint(new Vector3(2f, 0f, 1.2f))), "respetar ancho");
            door.CloseBehind();
            var blocker = exit.Find("Cierre de retorno");
            Check(blocker != null && blocker.gameObject.activeSelf && blocker.GetComponent<BoxCollider>() != null, "activar cierre");
            room.ConfigureMarkers(RoomConnection.Door, null, spawn, RoomConnection.Hatch, exit);
            Check(room.IsStartingRoomConfigured, "primera sala sin entrada");
            Check(!room.CanPrepare(null, null), "exigir prefab de trampilla");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(target); }
        Debug.Log("Salas: verificaciones de marcadores, conexión y cierre superadas.");
    }
    static Transform Child(Transform parent, string name, Vector3 position, float yaw)
    {
        var go = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return go.transform;
    }
    static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("Salas: falló " + message); }
}
