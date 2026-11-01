using UnityEditor;
using UnityEngine;

public static class LegendaryModelSetup
{
    const string Source = "Assets/Proyect/Assets/Models/";
    const string Output = "Assets/Proyect/Assets/Items/Modelos legendarios/";
    const string Catalog = "Assets/Proyect/Assets/Items/Catalogo/Tier 3/";

    [MenuItem("Tools/Runner/Preparar modelos legendarios")]
    public static void Prepare()
    {
        if (!AssetDatabase.IsValidFolder(Output.TrimEnd('/')))
            AssetDatabase.CreateFolder("Assets/Proyect/Assets/Items", "Modelos legendarios");
        Build("ancla", "ancla.png", null, "ancla_del_explorador");
        Build("botas aladas", "botas aladas.png", "alas aladas.png", "botas_del_vendaval", "alas_del_halcon");
        Build("botas magma", "bota de lava.png", null, "botas_del_meteorito");
        Build("corazon", "coraxzon.png", null, "corazon_de_carrera", "corazon_de_cazador");
        Build("coronamejorada", "corona mejorada.png", null, "corona_del_espectaculo", "corona_del_temerario");
        Build("unicornio", null, null);
        AssignExisting("Spheretransparent", "monoculo_dorado", "maldicion_de_la_torre");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Modelos legendarios preparados. Revisar escala y texturas en Play Mode.");
    }

    static void AssignExisting(string prefabName, params string[] itemNames)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Output + prefabName + ".prefab");
        if (prefab == null) throw new System.Exception("Falta el prefab: " + prefabName);
        foreach (var itemName in itemNames)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Catalog + itemName + ".asset");
            if (item == null) throw new System.Exception("Falta la ficha: " + itemName);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("legendaryModel").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }
    }

    static Material MaterialFor(string name, string texture)
    {
        if (texture == null) return null;
        var image = AssetDatabase.LoadAssetAtPath<Texture2D>(Source + texture);
        if (image == null) throw new System.Exception("Falta la textura: " + texture);
        string path = Output + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new System.Exception("No se encontró el shader URP/Lit.");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.SetTexture("_BaseMap", image);
        EditorUtility.SetDirty(material);
        return material;
    }

    static void Build(string modelName, string texture, string wingTexture, params string[] itemNames)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source + modelName + ".obj");
        if (source == null) throw new System.Exception("Falta el modelo: " + modelName);
        var main = MaterialFor(modelName, texture);
        var wings = MaterialFor(modelName + " alas", wingTexture);
        var root = new GameObject(modelName);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        instance.transform.SetParent(root.transform, false);
        var renderers = instance.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) { Object.DestroyImmediate(root); throw new System.Exception("Modelo sin renderers: " + modelName); }
        foreach (var renderer in renderers)
        {
            if (main == null) continue;
            var material = wings != null && renderer.name.Contains("polySurface45") ? wings : main;
            var materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++) materials[i] = material;
            renderer.sharedMaterials = materials;
        }
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (size > 0.0001f)
        {
            float scale = 0.8f / size;
            instance.transform.localScale = Vector3.one * scale;
            instance.transform.localPosition = -bounds.center * scale;
        }
        string path = Output + modelName + ".prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        foreach (string itemName in itemNames)
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>(Catalog + itemName + ".asset");
            if (item == null) throw new System.Exception("Falta la ficha: " + itemName);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("legendaryModel").objectReferenceValue = prefab;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
        }
    }
}
