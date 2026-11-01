using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FirstPersonHandAnimationEditorSetup
{
    static readonly string[] PlayerScenePaths =
    {
        "Assets/Scenes/Gameplay.unity",
        "Assets/Scenes/e.unity"
    };

    [MenuItem("Tools/Runner/Configurar manos en primera persona en la escena abierta")]
    public static void ConfigureOpenScene()
    {
        int added = AddToPlayersInLoadedScenes(useUndo: true);
        if (added > 0)
            EditorSceneManager.MarkAllScenesDirty();
        Debug.Log($"Manos en primera persona: {added} componente(s) añadido(s) en escenas abiertas.");
    }

    [MenuItem("Tools/Runner/Configurar manos en todas las escenas del jugador")]
    public static void ConfigureProjectPlayerScenes()
    {
        foreach (string path in PlayerScenePaths)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int added = AddToPlayersInLoadedScenes(useUndo: false);
            if (added > 0)
                EditorSceneManager.SaveScene(scene);
            Debug.Log($"Manos en primera persona: {added} componente(s) añadido(s) en {path}.");
        }
    }

    static int AddToPlayersInLoadedScenes(bool useUndo)
    {
        int added = 0;
        PlayerController[] players = Object.FindObjectsByType<PlayerController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PlayerController player in players)
        {
            if (player.GetComponent<FirstPersonHandAnimation>() != null) continue;
            if (useUndo) Undo.AddComponent<FirstPersonHandAnimation>(player.gameObject);
            else player.gameObject.AddComponent<FirstPersonHandAnimation>();
            added++;
        }

        return added;
    }
}
