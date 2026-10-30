using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class WallLeverTestSceneCreator
{
    [MenuItem("Tools/Runner/Crear prueba aislada de pared y palancas")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box("Suelo", new Vector3(0f, -0.25f, 0f), new Vector3(8f, 0.5f, 16f));
        Box("Pared izquierda", new Vector3(-4.25f, 2.5f, 0f), new Vector3(0.5f, 5f, 16f));
        Box("Pared derecha", new Vector3(4.25f, 2.5f, 0f), new Vector3(0.5f, 5f, 16f));
        Box("Pared inicio", new Vector3(0f, 2.5f, -8.25f), new Vector3(8f, 5f, 0.5f));
        Box("Pared final", new Vector3(0f, 2.5f, 8.25f), new Vector3(8f, 5f, 0.5f));
        Box("Techo", new Vector3(0f, 5.25f, 0f), new Vector3(8f, 0.5f, 16f));
        var player = new GameObject("Jugador", typeof(CharacterController));
        player.transform.position = new Vector3(0f, 1.05f, -6f);
        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<EnemyDirector>();
        player.AddComponent<PlayerHealth>();
        var cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cam.tag = "MainCamera"; cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        cam.AddComponent<PlayerCamera>().SetPlayer(controller);
        var root = new GameObject("Pared y palancas experimental");
        var wall = Box("Pared bloqueante", new Vector3(0f, 2.5f, 2f), new Vector3(8f, 5f, 0.35f));
        wall.transform.SetParent(root.transform, true);
        var wallMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.65f, 0.1f, 0.12f) };
        wall.GetComponent<Renderer>().sharedMaterial = wallMat;
        var template = Box("Plantilla palanca (inactiva)", new Vector3(0f, -20f, 0f), new Vector3(0.45f, 0.65f, 0.45f));
        template.transform.SetParent(root.transform, true);
        var leverMat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.95f, 0.65f, 0.1f) };
        template.GetComponent<Renderer>().sharedMaterial = leverMat;
        var lever = template.AddComponent<WallLever>(); template.SetActive(false);
        var markers = new[] { Marker(root.transform, "Palanca suelo izquierda", new Vector3(-2f, 0.4f, -4f)),
            Marker(root.transform, "Palanca suelo centro", new Vector3(0f, 0.4f, -1.5f)),
            Marker(root.transform, "Palanca suelo derecha", new Vector3(2f, 0.4f, -3f)),
            Marker(root.transform, "Palanca pared izquierda", new Vector3(-3.65f, 0.65f, 0f)) };
        var encounter = root.AddComponent<WallLeverEncounter>();
        encounter.Configure(wall, lever, markers, 3);
        encounter.SetDirector(player.GetComponent<EnemyDirector>());
        var light = new GameObject("Directional Light", typeof(Light));
        light.GetComponent<Light>().type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Prueba aislada creada. Mira las palancas y pulsa E para abrir la pared.");
    }
    static Transform Marker(Transform parent, string name, Vector3 position)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.position = position; return go.transform; }
    static GameObject Box(string name, Vector3 position, Vector3 scale)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.transform.position = position; go.transform.localScale = scale; return go; }
}
