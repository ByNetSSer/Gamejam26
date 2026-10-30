using UnityEngine;

// Temporary pickup for exercising the same modifier API as items, without an inventory.
[RequireComponent(typeof(Collider))]
public sealed class StatPickup : MonoBehaviour
{
    [SerializeField] PlayerStat stat;
    [SerializeField] StatModifierType type = StatModifierType.Flat;
    [SerializeField] float value = 1f;
    bool collected;
    void OnTriggerEnter(Collider other)
    {
        if (collected) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        collected = true;
        player.Stats.AddModifier(this, stat, type, value);
        gameObject.SetActive(false);
    }
}
