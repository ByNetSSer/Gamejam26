using UnityEngine;

// References keep catalog assets available in builds without moving them.
public sealed class RewardCatalog : ScriptableObject
{
    [SerializeField] ItemDefinition[] items;
    public ItemDefinition Find(string displayName)
    {
        if (items == null) return null;
        foreach (var item in items)
            if (item != null && item.DisplayName == displayName) return item;
        return null;
    }
}
