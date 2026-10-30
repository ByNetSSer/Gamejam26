using UnityEngine;

// Temporary pickup for exercising the same modifier API as items, without an inventory.
[RequireComponent(typeof(Collider))]
public sealed class StatPickup : MonoBehaviour
{
    [SerializeField] PlayerStat stat;
    [SerializeField] StatModifierType type = StatModifierType.Flat;
    [SerializeField] float value = 1f;
    [Header("Notificación")]
    [SerializeField] ItemDefinition definition;
    [SerializeField] string displayName;
    [SerializeField, TextArea] string effectDescription;
    [SerializeField] Sprite icon;
    [SerializeField] ItemAcquisitionSource acquisitionSource = ItemAcquisitionSource.Pickup;
    bool collected;
    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        collected = true;
        player.Stats.AddModifier(this, stat, type, value);
        string title = definition != null ? definition.DisplayName : string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        string effect = !string.IsNullOrWhiteSpace(effectDescription) ? effectDescription : ItemAcquisitionHud.Describe(stat, type, value);
        Sprite image = definition != null && definition.Icon != null ? definition.Icon : icon;
        if (image == null) { var renderer = GetComponentInChildren<SpriteRenderer>(); if (renderer != null) image = renderer.sprite; }
        ItemAcquisitionHud.Show(player, title, effect, image, acquisitionSource);
        gameObject.SetActive(false);
    }
}
