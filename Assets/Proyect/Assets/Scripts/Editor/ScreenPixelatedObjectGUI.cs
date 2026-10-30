using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ScreenPixelatedObjectGUI : ShaderGUI
{
    public override void OnGUI(MaterialEditor editor, MaterialProperty[] properties)
    {
        base.OnGUI(editor, properties);
        MaterialProperty overlay = FindProperty("_Overlay", properties);
        EditorGUI.showMixedValue = overlay.hasMixedValue;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.Toggle("Draw On Top", overlay.floatValue > 0.5f);
        if (EditorGUI.EndChangeCheck())
        {
            editor.RegisterPropertyChangeUndo("Draw On Top");
            foreach (Material material in editor.targets)
            {
                material.SetFloat("_Overlay", enabled ? 1 : 0);
                material.SetFloat("_ZTest", (float)(enabled ? CompareFunction.Always : CompareFunction.LessEqual));
                material.renderQueue = enabled ? (int)RenderQueue.Overlay : -1;
            }
        }
        EditorGUI.showMixedValue = false;
    }
}
