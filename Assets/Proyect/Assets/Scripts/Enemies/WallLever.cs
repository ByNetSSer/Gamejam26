using UnityEngine;

// Test lever instantiated only at authored positions.
public sealed class WallLever : MonoBehaviour
{
    WallLeverEncounter owner;
    bool activated;
    public bool Activated => activated;
    public void Initialize(WallLeverEncounter encounter) { owner = encounter; }
    public void Activate()
    {
        if (activated || owner == null || !owner.CanActivateLevers) return;
        activated = true;
        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            block.SetColor("_BaseColor", Color.green);
            block.SetColor("_Color", Color.green);
            renderer.SetPropertyBlock(block);
        }
        owner.LeverActivated(this);
    }
}
