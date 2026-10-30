using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class TowerDemoCreator
{
    [MenuItem("Tools/Runner/Crear torre de prueba")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        const string parent = "Assets/Proyect";
        string folderName = "TorrePrueba";
        string unique = AssetDatabase.GenerateUniqueAssetPath(parent + "/" + folderName);
        string name = System.IO.Path.GetFileName(unique);
        AssetDatabase.CreateFolder(parent, name);
        var brown = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        brown.color = new Color(0.35f, 0.17f, 0.07f);
        AssetDatabase.CreateAsset(brown, unique + "/Madera marron.mat");
        var hatchPrefab = MakeHatch(unique, brown);
        var doorPrefab = MakeDoor(unique, brown);
        var dontseePrefab = MakeDontsee(unique);
        var initial = MakeRoom(unique, "Inicial - solo salida trampilla", RoomConnection.Door, RoomConnection.Hatch, true);
        var final = MakeRoom(unique, "Final - solo entrada trampilla", RoomConnection.Hatch, RoomConnection.Hatch, false, true);
        var rooms = new RoomDefinition[4];
        rooms[0] = MakeRoom(unique, "Puerta a puerta", RoomConnection.Door, RoomConnection.Door);
        rooms[1] = MakeRoom(unique, "Puerta a trampilla", RoomConnection.Door, RoomConnection.Hatch);
        rooms[2] = MakeRoom(unique, "Trampilla a puerta", RoomConnection.Hatch, RoomConnection.Door);
        rooms[3] = MakeRoom(unique, "Trampilla a trampilla", RoomConnection.Hatch, RoomConnection.Hatch);
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var player = new GameObject("Jugador", typeof(CharacterController));
        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<PlayerHealth>();
        var score = player.AddComponent<PlayerScore>();
        player.AddComponent<ScoreDebugView>().SetScore(score);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        cameraObject.AddComponent<PlayerCamera>().SetPlayer(controller);
        FlupinDemoBuilder.AddTo(player);
        var tower = new GameObject("Gestor de salas");
        var manager = tower.AddComponent<RoomManager>();
        manager.Configure(controller, initial, final, new[] { rooms[3] }, new[] { rooms[2] }, new[] { rooms[0] }, new[] { rooms[1] }, 8, hatchPrefab, doorPrefab);
        tower.AddComponent<DontseeEncounter>().Configure(manager, dontseePrefab);
        tower.AddComponent<RoomDebugView>().SetManager(manager);
        var lightObject = new GameObject("Directional Light", typeof(Light));
        lightObject.GetComponent<Light>().type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Torre de prueba creada con prefabs en " + unique + ". Guarda la escena si deseas conservarla.");
    }
    static Dontsee MakeDontsee(string folder)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Dontsee";
        Object.DestroyImmediate(go.GetComponent<Collider>());
        var open = AssetDatabase.LoadAssetAtPath<Material>("Assets/Proyect/Assets/Sprites/Materials/Dontsee ojo abierto.mat");
        var closed = AssetDatabase.LoadAssetAtPath<Material>("Assets/Proyect/Assets/Sprites/Materials/Dontsee ojo cerrado.mat");
        go.transform.localScale = new Vector3(1.21f, 2f, 1f);
        go.GetComponent<Renderer>().sharedMaterial = open;
        var enemy = go.AddComponent<Dontsee>();
        var data = new SerializedObject(enemy);
        data.FindProperty("stalkingMaterial").objectReferenceValue = open;
        data.FindProperty("watchedMaterial").objectReferenceValue = closed;
        data.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + "/Dontsee.prefab").GetComponent<Dontsee>();
        Object.DestroyImmediate(go);
        return prefab;
    }
    static ImpactTarget MakeHatch(string folder, Material brown)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Trampilla";
        go.transform.localScale = new Vector3(3f, 0.2f, 3f);
        go.GetComponent<Renderer>().sharedMaterial = brown;
        var target = go.AddComponent<ImpactTarget>();
        var data = new SerializedObject(target);
        data.FindProperty("hatch").boolValue = true;
        data.FindProperty("scoreReason").stringValue = "Trampilla rota";
        data.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, folder + "/Trampilla.prefab").GetComponent<ImpactTarget>();
        Object.DestroyImmediate(go);
        return prefab;
    }
    static RoomDoor MakeDoor(string folder, Material brown)
    {
        var root = new GameObject("Puerta");
        var hinge = Marker(root.transform, "Bisagra", new Vector3(-1.5f, 0f, 0f));
        var panel = Cube(hinge, "Panel", new Vector3(1.5f, 0f, 0f), new Vector3(3f, 3f, 0.2f));
        panel.GetComponent<Renderer>().sharedMaterial = brown;
        var door = root.AddComponent<RoomDoor>();
        door.SetHinge(hinge);
        var data = new SerializedObject(door);
        data.FindProperty("openAngle").floatValue = -90f;
        data.ApplyModifiedPropertiesWithoutUndo();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/Puerta.prefab").GetComponent<RoomDoor>();
        Object.DestroyImmediate(root);
        return prefab;
    }
    static RoomDefinition MakeRoom(string folder, string name, RoomConnection entry, RoomConnection output, bool isInitial = false, bool isFinal = false)
    {
        var root = new GameObject(name);
        var room = root.AddComponent<RoomDefinition>();
        if (!isInitial && !isFinal)
            Marker(root.transform, "Aparición Dontsee", new Vector3(3f, 1.5f, 1f)).gameObject.AddComponent<DontseeSpawnPoint>();
        // Room dimensions: x/z -5..5, floor y=0, height 6.
        if (output == RoomConnection.Hatch && !isFinal)
        {
            Cube(root.transform, "Suelo anterior", new Vector3(0f, -0.25f, -2.25f), new Vector3(10f, 0.5f, 5.5f));
            Cube(root.transform, "Suelo posterior", new Vector3(0f, -0.25f, 4.25f), new Vector3(10f, 0.5f, 1.5f));
            Cube(root.transform, "Suelo izquierdo", new Vector3(-3.25f, -0.25f, 2f), new Vector3(3.5f, 0.5f, 3f));
            Cube(root.transform, "Suelo derecho", new Vector3(3.25f, -0.25f, 2f), new Vector3(3.5f, 0.5f, 3f));
        }
        else Cube(root.transform, "Suelo", new Vector3(0f, -0.25f, 0f), new Vector3(10f, 0.5f, 10f));
        Cube(root.transform, "Pared izquierda", new Vector3(-5.25f, 3f, 0f), new Vector3(0.5f, 6f, 10.5f));
        Cube(root.transform, "Pared derecha", new Vector3(5.25f, 3f, 0f), new Vector3(0.5f, 6f, 10.5f));
        EndWall(root.transform, -5f, !isInitial && entry == RoomConnection.Door);
        EndWall(root.transform, 5f, !isFinal && output == RoomConnection.Door);
        var entryAnchor = isInitial ? null : Marker(root.transform, "Marcador entrada", entry == RoomConnection.Door ? new Vector3(0f, 1.1f, -5f) : new Vector3(0f, 6f, 0f));
        var start = Marker(root.transform, "Inicio del jugador", new Vector3(0f, 1.05f, -3f));
        Transform exitAnchor = isFinal ? null : Marker(root.transform, "Marcador salida", output == RoomConnection.Door ? new Vector3(0f, 1.1f, 5f) : new Vector3(0f, 0f, 2f));
        // El prefab de sala solo contiene marcadores; la trampilla y el cierre se crean en ejecución.
        room.ConfigureMarkers(entry, entryAnchor, start, output, exitAnchor);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, folder + "/" + name + ".prefab").GetComponent<RoomDefinition>();
        Object.DestroyImmediate(root);
        return prefab;
    }
    static void EndWall(Transform root, float z, bool door)
    {
        if (!door) { Cube(root, "Pared", new Vector3(0f, 3f, z), new Vector3(10f, 6f, 0.5f)); return; }
        Cube(root, "Pared puerta izquierda", new Vector3(-3.25f, 3f, z), new Vector3(3.5f, 6f, 0.5f));
        Cube(root, "Pared puerta derecha", new Vector3(3.25f, 3f, z), new Vector3(3.5f, 6f, 0.5f));
        Cube(root, "Dintel", new Vector3(0f, 4.5f, z), new Vector3(3f, 3f, 0.5f));
    }
    static Transform Marker(Transform root, string name, Vector3 position)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); go.transform.localPosition = position; return go.transform;
    }
    static GameObject Cube(Transform root, string name, Vector3 position, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.SetParent(root, false);
        go.transform.localPosition = position; go.transform.localScale = scale; return go;
    }
}
