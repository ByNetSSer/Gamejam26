using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class CrusherTestSceneCreator
{
    [MenuItem("Tools/Runner/Crear prueba aislada de aplanadora")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box("Suelo", new Vector3(0f, -0.25f, 0f), new Vector3(8f, 0.5f, 18f));
        Box("Pared izquierda", new Vector3(-4.25f, 3f, 0f), new Vector3(0.5f, 6f, 18f));
        Box("Pared derecha", new Vector3(4.25f, 3f, 0f), new Vector3(0.5f, 6f, 18f));
        Box("Pared inicio", new Vector3(0f, 3f, -9.25f), new Vector3(8f, 6f, 0.5f));
        Box("Pared final", new Vector3(0f, 3f, 9.25f), new Vector3(8f, 6f, 0.5f));
        var safeZone = Box("Zona segura (verde)", new Vector3(0f, 0.01f, 6.5f), new Vector3(7.5f, 0.02f, 3.5f));
        Object.DestroyImmediate(safeZone.GetComponent<Collider>());
        var safeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.15f, 0.8f, 0.25f) };
        safeZone.GetComponent<Renderer>().sharedMaterial = safeMaterial;
        var player = new GameObject("Jugador", typeof(CharacterController));
        player.transform.position = new Vector3(0f, 1.05f, -6.5f);
        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<EnemyDirector>();
        player.AddComponent<PlayerHealth>();
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        cameraObject.AddComponent<PlayerCamera>().SetPlayer(controller);
        var root = new GameObject("Aplanadora experimental");
        var top = Marker(root.transform, "Inicio", new Vector3(0f, 5.5f, -1.5f));
        var bottom = Marker(root.transform, "Final", new Vector3(0f, 0.5f, -1.5f));
        var safe = Marker(root.transform, "Punto seguro", new Vector3(0f, 1f, 6f));
        var plate = Box("Aplanadora visual", top.position, Vector3.one);
        plate.transform.SetParent(root.transform, true);
        Object.DestroyImmediate(plate.GetComponent<Collider>());
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.8f, 0.15f, 0.08f) };
        plate.GetComponent<Renderer>().sharedMaterial = material;
        var crusher = root.AddComponent<RoomCrusher>();
        crusher.Configure(top, bottom, safe, plate.transform, new Vector3(7.5f, 1f, 10f));
        crusher.SetDirector(player.GetComponent<EnemyDirector>());
        var light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Prueba aislada creada. Entra en Play y alcanza la zona verde; guarda la escena si deseas conservarla.");
    }
    static Transform Marker(Transform parent, string name, Vector3 position)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform; }
    static GameObject Box(string name, Vector3 position, Vector3 scale)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale; return go; }
}
