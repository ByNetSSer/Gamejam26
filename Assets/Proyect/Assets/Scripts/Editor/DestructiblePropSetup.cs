using UnityEditor;
using UnityEngine;

public static class DestructiblePropSetup
{
    [MenuItem("Tools/Runner/Preparar props seleccionados como destructibles")]
    static void Prepare()
    {
        foreach (var go in Selection.gameObjects)
        {
            if (go.GetComponentInChildren<Collider>() == null)
            {
                Debug.LogWarning("El prop necesita un Collider para recibir impactos: " + go.name, go);
                continue;
            }
            var target = go.GetComponent<ImpactTarget>();
            if (target == null) target = Undo.AddComponent<ImpactTarget>(go);
            var serialized = new SerializedObject(target);
            serialized.FindProperty("scoreReason").stringValue = go.name + " roto";
            serialized.FindProperty("scorePoints").intValue = 2;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(go);
        }
    }
}
