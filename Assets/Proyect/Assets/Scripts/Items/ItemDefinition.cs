using UnityEngine;

public enum ItemTier { Tier1 = 1, Tier2 = 2, Tier3 = 3 }
public enum ItemCategory { Movement, Curse, Health, Events, Flupin, Dontsee, Crusher, Legendary }
public enum ItemDisposition { Beneficial, Mixed, Curse }
public enum ItemEnemyRequirement { None, Flupin, Dontsee, Crusher }

// Design data only: acquiring and applying effects is the responsibility of a future item system.
[CreateAssetMenu(menuName = "Runner/Item de catálogo", fileName = "Item")]
public sealed class ItemDefinition : ScriptableObject
{
    [SerializeField] string displayName;
    [SerializeField, TextArea(2, 4)] string visualDescription;
    [SerializeField, TextArea(2, 5)] string effectDescription;
    [SerializeField] ItemTier tier = ItemTier.Tier1;
    [SerializeField] ItemCategory category;
    [SerializeField] ItemDisposition disposition;
    [SerializeField] ItemEnemyRequirement requiredEnemy;
    [SerializeField, TextArea(2, 4)] string designNotes;
    [SerializeField] Sprite icon;

    public string DisplayName => displayName;
    public string VisualDescription => visualDescription;
    public string EffectDescription => effectDescription;
    public ItemTier Tier => tier;
    public ItemCategory Category => category;
    public ItemDisposition Disposition => disposition;
    public ItemEnemyRequirement RequiredEnemy => requiredEnemy;
    public string DesignNotes => designNotes;
    public Sprite Icon => icon;
}
