using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class VoidEyeFieldSandboxBuilder
{
    const string TestScenePath = "Assets/Scenes/SkyboxTesting.unity";
    const string PlayerSourceScenePath = "Assets/Scenes/e.unity";
    const string RoomPrefabPath = "Assets/PrefabsMapas/Bajito/P-P/Acueducto-P-P.prefab";

    [MenuItem("Tools/Runner/Preparar prueba de ojos del vacío")]
    public static void Build()
    {
        var testScene = EditorSceneManager.OpenScene(TestScenePath, OpenSceneMode.Single);
        RemovePreviousTestObjects(testScene);

        var sourceScene = EditorSceneManager.OpenScene(PlayerSourceScenePath, OpenSceneMode.Additive);
        var sourcePlayer = sourceScene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<PlayerController>(true))
            .FirstOrDefault();
        if (sourcePlayer == null)
        {
            EditorSceneManager.CloseScene(sourceScene, true);
            throw new System.InvalidOperationException("No se encontró el jugador en " + PlayerSourceScenePath);
        }

        var playerObject = Object.Instantiate(sourcePlayer.gameObject);
        playerObject.name = "Jugador - Prueba caída";
        SceneManager.MoveGameObjectToScene(playerObject, testScene);
        var player = playerObject.GetComponent<PlayerController>();
        player.enabled = true;
        EditorSceneManager.CloseScene(sourceScene, true);

        var roomPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoomPrefabPath);
        if (roomPrefab == null) throw new System.InvalidOperationException("No se encontró " + RoomPrefabPath);
        var roomObject = (GameObject)PrefabUtility.InstantiatePrefab(roomPrefab, testScene);
        roomObject.name = "Acueducto P-P - Prueba ojos";
        roomObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        var room = roomObject.GetComponent<RoomDefinition>();
        Bounds bounds = CalculateBounds(roomObject.transform);
        Transform entry = room != null ? room.EntryAnchor : null;
        Vector3 spawn = entry != null
            ? entry.position + entry.forward * 3f + Vector3.up * 1.1f
            : new Vector3(bounds.center.x, bounds.min.y + 1.1f, bounds.center.z);
        Quaternion rotation = entry != null
            ? Quaternion.Euler(0f, entry.eulerAngles.y, 0f)
            : Quaternion.identity;
        playerObject.transform.SetPositionAndRotation(spawn, rotation);

        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera.gameObject.scene == testScene && !camera.transform.IsChildOf(playerObject.transform))
                Object.DestroyImmediate(camera.gameObject);
        }

        var fieldObject = new GameObject("Campo de ojos - Prueba");
        SceneManager.MoveGameObjectToScene(fieldObject, testScene);
        var field = fieldObject.AddComponent<VoidEyeField>();
        field.ConfigureStandalone(player.transform, roomObject.transform);

        var controls = fieldObject.AddComponent<VoidEyeFallSandbox>();
        controls.Configure(player, roomObject.transform, spawn, rotation);

        SceneManager.SetActiveScene(testScene);
        EditorSceneManager.MarkSceneDirty(testScene);
        EditorSceneManager.SaveScene(testScene);
        Selection.activeGameObject = fieldObject;
        EditorGUIUtility.PingObject(fieldObject);
        Debug.Log("SkyboxTesting preparado: F inicia caída libre y R devuelve al jugador a la sala.", fieldObject);
    }

    static void RemovePreviousTestObjects(Scene scene)
    {
        string[] names =
        {
            "Jugador - Prueba caída",
            "Acueducto P-P - Prueba ojos",
            "Campo de ojos - Prueba"
        };
        foreach (var root in scene.GetRootGameObjects())
        {
            if (names.Contains(root.name)) Object.DestroyImmediate(root);
        }
    }

    static Bounds CalculateBounds(Transform root)
    {
        Bounds result = new Bounds(root.position, Vector3.one);
        bool found = false;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (!found) { result = collider.bounds; found = true; }
            else result.Encapsulate(collider.bounds);
        }
        return result;
    }
}
