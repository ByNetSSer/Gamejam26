using System.Collections.Generic;
using MoreMountains.Feedbacks;
using UnityEditor;
using UnityEngine;

public static class FlupinDemoBuilder
{
    [MenuItem("Tools/Runner/Añadir Flupin al jugador seleccionado")]
    public static void AddToSelected()
    {
        var selected = Selection.activeGameObject;
        var controller = selected != null ? selected.GetComponentInParent<PlayerController>() : null;
        if (controller == null) { Debug.LogWarning("Selecciona el jugador o uno de sus hijos en la jerarquía."); return; }
        AddTo(controller.gameObject);
    }
    public static void AddTo(GameObject player)
    {
        if (player.GetComponent<EnemyDirector>() == null) player.AddComponent<EnemyDirector>();
        if (player.GetComponent<PlayerRepel>() == null) player.AddComponent<PlayerRepel>();
        if (player.GetComponent<ParryFeedback>() == null) player.AddComponent<ParryFeedback>();
        if (player.GetComponent<ParryAccents>() == null) player.AddComponent<ParryAccents>();
        int existingFlupins = player.GetComponentsInChildren<Flupin>(true).Length;
        var enemy = GameObject.CreatePrimitive(PrimitiveType.Quad);
        enemy.name = "Flupin";
        Object.DestroyImmediate(enemy.GetComponent<Collider>());
        Undo.RegisterCreatedObjectUndo(enemy, "Añadir Flupin");
        enemy.transform.SetParent(player.transform, false);
        enemy.transform.localPosition = Quaternion.Euler(0f, existingFlupins * 40f, 0f) * new Vector3(0f, 0f, 3.2f) + Vector3.up * 0.5f;
        var flupin = enemy.AddComponent<Flupin>();
        flupin.SetDirector(player.GetComponent<EnemyDirector>());
        enemy.AddComponent<FlupinVisual>();
        var warning = Feedback(enemy.transform, "FEEL - aviso", 1.2f);
        var repel = Feedback(enemy.transform, "FEEL - repelido", 1.35f);
        var hit = Feedback(enemy.transform, "FEEL - golpe", 1.5f);
        var data = new SerializedObject(flupin);
        data.FindProperty("warningFeedback").objectReferenceValue = warning;
        data.FindProperty("repelFeedback").objectReferenceValue = repel;
        data.FindProperty("hitFeedback").objectReferenceValue = hit;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    static MMF_Player Feedback(Transform target, string name, float amount)
    {
        var go = new GameObject(name);
        go.transform.SetParent(target, false);
        var player = go.AddComponent<MMF_Player>();
        player.FeedbacksList = new List<MMF_Feedback>
        {
            new MMF_Scale { AnimateScaleTarget = target, AnimateScaleDuration = 0.25f, RemapCurveOne = amount }
        };
        return player;
    }
}

