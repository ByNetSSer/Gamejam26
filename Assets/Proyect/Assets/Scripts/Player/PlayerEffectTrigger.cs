using System.Collections.Generic;
using UnityEngine;

public enum PlayerEffectTriggerKind { RoomCleared, DamageReceived, SpeedPresence }

// Example composable trigger; add one per item effect, not a global event singleton.
[RequireComponent(typeof(PlayerController), typeof(PlayerEvents), typeof(PlayerStatEffects))]
public sealed class PlayerEffectTrigger : MonoBehaviour
{
    [SerializeField] PlayerEffectTriggerKind trigger;
    [SerializeField] PlayerStat stat = PlayerStat.MoveSpeed;
    [SerializeField] StatModifierType modifierType = StatModifierType.Percent;
    [SerializeField] float value = 0.2f;
    [SerializeField, Min(0.01f)] float duration = 3f;
    [SerializeField, Min(0f)] float speedThreshold = 10f;
    PlayerController player;
    PlayerEvents events;
    PlayerStatEffects effects;
    StatEffect presence;
    readonly List<StatEffect> timedEffects = new List<StatEffect>();
    void Awake() { player = GetComponent<PlayerController>(); events = GetComponent<PlayerEvents>(); effects = GetComponent<PlayerStatEffects>(); }
    void OnEnable()
    {
        events.RoomCleared += OnRoom;
        events.DamageReceived += OnDamage;
    }
    void OnDisable()
    {
        if (events != null) { events.RoomCleared -= OnRoom; events.DamageReceived -= OnDamage; }
        presence?.Cancel(); presence = null;
        foreach (var effect in timedEffects.ToArray()) effect.Cancel();
        timedEffects.Clear();
    }
    void OnRoom(int count) { if (trigger == PlayerEffectTriggerKind.RoomCleared) ApplyTimed(); }
    void OnDamage(float amount) { if (trigger == PlayerEffectTriggerKind.DamageReceived) ApplyTimed(); }
    void ApplyTimed()
    {
        var effect = effects.Apply(stat, modifierType, value, Mathf.Max(0.01f, duration));
        timedEffects.Add(effect);
        effect.Finished += OnEffectFinished;
    }
    void OnEffectFinished(StatEffect effect) { effect.Finished -= OnEffectFinished; timedEffects.Remove(effect); }
    void Update()
    {
        if (trigger != PlayerEffectTriggerKind.SpeedPresence) return;
        if (player.HorizontalSpeed >= speedThreshold)
        {
            if (presence == null || !presence.IsActive) presence = effects.Apply(stat, modifierType, value, -1f);
        }
        else if (presence != null) { presence.Cancel(); presence = null; }
    }
}
