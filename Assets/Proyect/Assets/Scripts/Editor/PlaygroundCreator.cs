using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class PlaygroundCreator
{
    [MenuItem("Tools/Runner/Crear escena de prueba")]
    public static void Create()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var player = new GameObject("Player", typeof(CharacterController));
        player.transform.position = new Vector3(0f, 1.05f, -7f);
        var controller = player.AddComponent<PlayerController>();
        player.AddComponent<PlayerHealth>();
        var score = player.AddComponent<PlayerScore>();
        player.AddComponent<ScoreDebugView>().SetScore(score);
        var cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(player.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 0.7f, 0f);
        cameraObject.AddComponent<PlayerCamera>().SetPlayer(controller);

        Cube("Suelo principal", new Vector3(0f, -0.5f, -2f), new Vector3(12f, 1f, 16f));
        Cube("Suelo escotilla izquierda", new Vector3(-3.5f, -0.5f, 8f), new Vector3(5f, 1f, 4f));
        Cube("Suelo escotilla derecha", new Vector3(3.5f, -0.5f, 8f), new Vector3(5f, 1f, 4f));
        Cube("Suelo inferior", new Vector3(0f, -6f, 8f), new Vector3(12f, 1f, 10f));
        Cube("Pared izquierda", new Vector3(-6.5f, 2f, 0f), new Vector3(1f, 5f, 20f));
        Cube("Pared derecha", new Vector3(6.5f, 2f, 0f), new Vector3(1f, 5f, 20f));
        Cube("Pared final", new Vector3(0f, 2f, 10.5f), new Vector3(13f, 5f, 1f));
        Cube("Pared inicio", new Vector3(0f, 2f, -10.5f), new Vector3(13f, 5f, 1f));
        Cube("Pared inferior fondo", new Vector3(0f, -3f, 13.5f), new Vector3(13f, 5f, 1f));
        Cube("Pared inferior izquierda", new Vector3(-6.5f, -3f, 8f), new Vector3(1f, 5f, 11f));
        Cube("Pared inferior derecha", new Vector3(6.5f, -3f, 8f), new Vector3(1f, 5f, 11f));
        var hatch = Cube("Escotilla - pisotón", new Vector3(0f, -0.15f, 8f), new Vector3(2f, 0.3f, 4f));
        var target = hatch.AddComponent<ImpactTarget>();
        var serialized = new SerializedObject(target);
        serialized.FindProperty("hatch").boolValue = true;
        serialized.FindProperty("scoreReason").stringValue = "Escotilla rota";
        serialized.ApplyModifiedPropertiesWithoutUndo();
        hatch.AddComponent<RoomAdvanceOnBreak>().SetPlayer(controller);
        Box(new Vector3(0f, 0.5f, -1f));
        Box(new Vector3(2f, 0.5f, 3f));
        Box(new Vector3(-2f, 0.5f, 3f));
        Surface("Hielo - menos frenado y giro", new Vector3(-3f, 0.12f, 0.5f), new Vector3(2.5f, 0.3f, 3f), new Color(0.3f, 0.8f, 1f), 0f,
            new GroundSurface.Modifier { stat = PlayerStat.Braking, type = StatModifierType.Percent, value = -0.45f },
            new GroundSurface.Modifier { stat = PlayerStat.TurnAcceleration, type = StatModifierType.Percent, value = -0.35f });
        Surface("Brea - velocidad y aceleración reducidas", new Vector3(0f, 0.12f, 1.5f), new Vector3(2.5f, 0.3f, 3f), new Color(0.3f, 0.18f, 0.1f), 0f,
            new GroundSurface.Modifier { stat = PlayerStat.MoveSpeed, type = StatModifierType.Percent, value = -0.3f },
            new GroundSurface.Modifier { stat = PlayerStat.Acceleration, type = StatModifierType.Percent, value = -0.25f });
        Surface("Lava - daño por permanecer", new Vector3(3f, 0.12f, 0.5f), new Vector3(2.5f, 0.3f, 3f), new Color(1f, 0.25f, 0.05f), 1f);
        Pickup("Amarillo - velocidad +6", new Vector3(-3f, 0.8f, -5f), PlayerStat.MoveSpeed, 6f, Color.yellow);
        Pickup("Azul - duración combo +4 s", new Vector3(0f, 0.8f, -5f), PlayerStat.ComboDuration, 4f, Color.cyan);
        Pickup("Verde - puntuación x2", new Vector3(3f, 0.8f, -5f), PlayerStat.ScoreAmplification, 1f, Color.green);
        var lightObject = new GameObject("Directional Light", typeof(Light));
        lightObject.GetComponent<Light>().type = LightType.Directional;
        lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log("Prueba en primera persona: WASD mover, ratón mirar, espacio saltar, C agacharse/deslizarse, Ctrl en el aire para pisotón, Escape libera ratón.");
    }
    static GameObject Cube(string name, Vector3 position, Vector3 scale)
    {
        var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
        result.name = name; result.transform.position = position; result.transform.localScale = scale;
        return result;
    }
    static void Box(Vector3 position)
    {
        var box = Cube("Caja rompible", position, Vector3.one);
        var target = box.AddComponent<ImpactTarget>();
        var serialized = new SerializedObject(target);
        serialized.FindProperty("breakSpeed").floatValue = 5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Surface(string name, Vector3 position, Vector3 scale, Color color, float damage, params GroundSurface.Modifier[] modifiers)
    {
        var zone = Cube(name, position, scale);
        zone.GetComponent<Collider>().isTrigger = true;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        zone.GetComponent<Renderer>().sharedMaterial = material;
        var component = zone.AddComponent<GroundSurface>();
        component.SetAudioKind(name.StartsWith("Brea") ? SurfaceAudioKind.Tar : name.StartsWith("Hielo") ? SurfaceAudioKind.Ice : damage > 0f ? SurfaceAudioKind.Lava : SurfaceAudioKind.Normal);
        var data = new SerializedObject(component);
        var entries = data.FindProperty("modifiers");
        entries.arraySize = modifiers.Length;
        for (int i = 0; i < modifiers.Length; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            entry.FindPropertyRelative("stat").enumValueIndex = (int)modifiers[i].stat;
            entry.FindPropertyRelative("type").enumValueIndex = (int)modifiers[i].type;
            entry.FindPropertyRelative("value").floatValue = modifiers[i].value;
        }
        data.FindProperty("damagePerTick").floatValue = damage;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static void Pickup(string name, Vector3 position, PlayerStat stat, float value, Color color)
    {
        var pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pickup.name = name; pickup.transform.position = position; pickup.transform.localScale = Vector3.one * 0.7f;
        pickup.GetComponent<Collider>().isTrigger = true;
        var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.color = color;
        pickup.GetComponent<Renderer>().sharedMaterial = material;
        var component = pickup.AddComponent<StatPickup>();
        var data = new SerializedObject(component);
        data.FindProperty("stat").enumValueIndex = (int)stat;
        data.FindProperty("value").floatValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
