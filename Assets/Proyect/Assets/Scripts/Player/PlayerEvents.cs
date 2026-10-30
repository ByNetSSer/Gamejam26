using System;
using UnityEngine;

// Damage systems call NotifyDamage only after damage is actually accepted.
[RequireComponent(typeof(PlayerController))]
public sealed class PlayerEvents : MonoBehaviour
{
    PlayerController player;
    public event Action<int> RoomCleared;
    public event Action<float> DamageReceived;
    void Awake() => player = GetComponent<PlayerController>();
    void OnEnable() { if (player == null) player = GetComponent<PlayerController>(); player.RoomCleared += OnRoomCleared; }
    void OnDisable() { if (player != null) player.RoomCleared -= OnRoomCleared; }
    void OnRoomCleared(int rooms) => RoomCleared?.Invoke(rooms);
    public void NotifyDamage(float amount) { if (amount > 0f) DamageReceived?.Invoke(amount); }
}
