using UnityEngine;

// Only for the sample: a hatch marks one room completion when it breaks.
[RequireComponent(typeof(ImpactTarget))]
public sealed class RoomAdvanceOnBreak : MonoBehaviour
{
    [SerializeField] PlayerController player;
    ImpactTarget target;
    public void SetPlayer(PlayerController value) => player = value;
    void OnEnable() { target = GetComponent<ImpactTarget>(); target.Broken += OnBroken; }
    void OnDisable() { if (target != null) target.Broken -= OnBroken; }
    void OnBroken(ImpactTarget _) { if (player != null) player.RegisterRoomCleared(); }
}
