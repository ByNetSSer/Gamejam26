using System;
using System.Collections.Generic;
using UnityEngine;

public enum LavaPixelationMode
{
    None,
    Fine,
    Large
}

// Adds a visual-only material pass to existing floor renderers. The floor mesh,
// collider and authored materials remain untouched and can be restored exactly.
public sealed class LavaFloorOverlay : IDisposable
{
    const string RevealProperty = "_Reveal";

    sealed class Entry
    {
        public Renderer Renderer;
        public Material[] Source;
        public Material[] Baseline;
        public int OverlayIndex = -1;
        public bool Showing;
    }

    readonly Material overlayMaterial;
    readonly HashSet<Material> excludedBaselineMaterials;
    readonly Dictionary<Renderer, Entry> entries = new Dictionary<Renderer, Entry>();
    readonly Dictionary<GameObject, int> sourceLayers = new Dictionary<GameObject, int>();
    readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();

    public bool IsValid => overlayMaterial != null;
    public int Count => entries.Count;
    public int LayerObjectCount => sourceLayers.Count;
    public LavaPixelationMode PixelationMode { get; private set; }

    public int CountObjectsOnPixelationLayer()
    {
        if (PixelationMode == LavaPixelationMode.None) return 0;
        string layerName = PixelationMode == LavaPixelationMode.Fine ? "PixelatedFine" : "PixelatedLarge";
        int targetLayer = LayerMask.NameToLayer(layerName);
        if (targetLayer < 0) return 0;

        int count = 0;
        foreach (var pair in sourceLayers)
            if (pair.Key != null && pair.Key.layer == targetLayer) count++;
        return count;
    }

    public LavaFloorOverlay(Material overlayMaterial, IEnumerable<Material> excludedBaselineMaterials = null)
    {
        this.overlayMaterial = overlayMaterial;
        this.excludedBaselineMaterials = excludedBaselineMaterials != null
            ? new HashSet<Material>(excludedBaselineMaterials)
            : new HashSet<Material>();
        if (overlayMaterial != null) this.excludedBaselineMaterials.Add(overlayMaterial);
    }

    public void Add(Renderer renderer, bool removeExistingOverlayFromBaseline = false)
    {
        if (renderer == null || entries.ContainsKey(renderer)) return;

        var rendererObject = renderer.gameObject;
        if (!sourceLayers.ContainsKey(rendererObject))
            sourceLayers.Add(rendererObject, rendererObject.layer);

        var source = renderer.sharedMaterials;
        var baseline = source;
        if (removeExistingOverlayFromBaseline)
        {
            var filtered = new List<Material>(source.Length);
            foreach (var material in source)
                if (!excludedBaselineMaterials.Contains(material)) filtered.Add(material);
            baseline = filtered.ToArray();
        }

        entries.Add(renderer, new Entry
        {
            Renderer = renderer,
            Source = source,
            Baseline = baseline
        });
    }

    public void SetPixelation(LavaPixelationMode mode)
    {
        PixelationMode = mode;
        if (mode == LavaPixelationMode.None)
        {
            RestoreLayers();
            return;
        }

        string layerName = mode == LavaPixelationMode.Fine ? "PixelatedFine" : "PixelatedLarge";
        int targetLayer = LayerMask.NameToLayer(layerName);
        if (targetLayer < 0)
        {
            Debug.LogWarning($"No existe el layer {layerName}; la lava conservará los layers originales.");
            RestoreLayers();
            PixelationMode = LavaPixelationMode.None;
            return;
        }

        foreach (var pair in sourceLayers)
            if (pair.Key != null) pair.Key.layer = targetLayer;
    }

    public void Show(float reveal)
    {
        if (overlayMaterial == null) return;
        reveal = Mathf.Clamp01(reveal);

        foreach (var pair in entries)
        {
            var entry = pair.Value;
            var renderer = entry.Renderer;
            if (renderer == null || entry.Baseline == null) continue;

            if (!entry.Showing)
            {
                var materials = new Material[entry.Baseline.Length + 1];
                Array.Copy(entry.Baseline, materials, entry.Baseline.Length);
                entry.OverlayIndex = entry.Baseline.Length;
                materials[entry.OverlayIndex] = overlayMaterial;
                renderer.sharedMaterials = materials;
                entry.Showing = true;
            }

            propertyBlock.Clear();
            renderer.GetPropertyBlock(propertyBlock, entry.OverlayIndex);
            propertyBlock.SetFloat(RevealProperty, reveal);
            propertyBlock.SetFloat("_CurseBlend", reveal);
            renderer.SetPropertyBlock(propertyBlock, entry.OverlayIndex);
        }
    }

    public void Hide()
    {
        foreach (var pair in entries)
        {
            var entry = pair.Value;
            if (entry.Renderer == null) continue;
            if (entry.Showing && entry.OverlayIndex >= 0)
            {
                propertyBlock.Clear();
                entry.Renderer.SetPropertyBlock(propertyBlock, entry.OverlayIndex);
            }
            entry.Renderer.sharedMaterials = entry.Baseline;
            entry.OverlayIndex = -1;
            entry.Showing = false;
        }
        RestoreLayers();
        PixelationMode = LavaPixelationMode.None;
    }

    public void RestoreSource()
    {
        foreach (var pair in entries)
        {
            var entry = pair.Value;
            if (entry.Renderer == null) continue;
            if (entry.Showing && entry.OverlayIndex >= 0)
            {
                propertyBlock.Clear();
                entry.Renderer.SetPropertyBlock(propertyBlock, entry.OverlayIndex);
            }
            entry.Renderer.sharedMaterials = entry.Source;
        }
        RestoreLayers();
        PixelationMode = LavaPixelationMode.None;
    }

    void RestoreLayers()
    {
        foreach (var pair in sourceLayers)
            if (pair.Key != null) pair.Key.layer = pair.Value;
    }

    public void Dispose()
    {
        RestoreSource();
        entries.Clear();
        sourceLayers.Clear();
    }
}
