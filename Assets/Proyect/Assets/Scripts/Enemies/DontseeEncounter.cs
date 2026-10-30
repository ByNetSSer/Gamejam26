using System;
using System.Collections.Generic;
using UnityEngine;

public enum DontseeEncounterStat { SpawnChance }

// Add markers to eligible rooms; one encounter at most is selected on room entry.
public sealed class DontseeEncounter : MonoBehaviour
{
    [SerializeField] RoomManager rooms;
    [SerializeField] Dontsee prefab;
    [SerializeField, Range(0f, 1f)] float chance = 0.35f;
    [SerializeField, Min(0)] int safeRoomsBetween = 2;
    IDisposable entered, exited;
    Dontsee current;
    RoomEnemyRoster roster;
    RoomContext pendingRoom;
    bool pending;
    public Dontsee CurrentEnemy => current;
    readonly Dictionary<DontseeEncounterStat, StatValue> modifiers = new Dictionary<DontseeEncounterStat, StatValue>();
    public StatValue GetStat(DontseeEncounterStat stat)
    {
        if (!modifiers.TryGetValue(stat, out var value)) modifiers.Add(stat, value = new StatValue());
        return value;
    }
    public void AddModifier(object source, DontseeEncounterStat stat, StatModifierType type, float value) => GetStat(stat).Add(source, type, value);
    public void RemoveModifiers(object source)
    {
        if (source == null) return;
        foreach (var value in modifiers.Values) value.RemoveSource(source);
    }
    public float SpawnChance => Mathf.Clamp01(GetStat(DontseeEncounterStat.SpawnChance).Calculate(chance));
    int lastEncounter = -100000;
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        if (rooms == null) return;
        entered = rooms.Events.Subscribe<RoomEnteredEvent>(OnEntered);
        exited = rooms.Events.Subscribe<RoomExitedEvent>(OnExited);
    }
    void OnDisable() { entered?.Dispose(); exited?.Dispose(); entered = exited = null; pending = false; Clear(DontseeEndReason.Disabled); }
    public void Configure(RoomManager manager, Dontsee enemy) { rooms = manager; prefab = enemy; }
    void OnEntered(RoomEnteredEvent e)
    {
        if (current != null && (pendingRoom.Room != e.Context.Room)) Clear(DontseeEndReason.RoomExited);
        if (e.Context.Kind != RoomKind.Intermediate) return;
        pendingRoom = e.Context;
        pending = true;
    }
    void Update()
    {
        if (current != null && pendingRoom.Room != rooms.CurrentRoom) Clear(DontseeEndReason.RoomExited);
        if (pending && pendingRoom.Room != rooms.CurrentRoom) pending = false;
        if (!pending || (roster != null && (!roster.DontseeSelected || !roster.EnemiesReady))) return;
        pending = false;
        TrySpawn(pendingRoom);
    }
    void TrySpawn(RoomContext context)
    {
        if (prefab == null || context.Room == null || context.Room != rooms.CurrentRoom || current != null || context.Index - lastEncounter <= safeRoomsBetween) return;
        var markers = context.Room.GetComponentsInChildren<DontseeSpawnPoint>();
        if (markers.Length == 0 || UnityEngine.Random.value >= SpawnChance) return;
        var player = FindPlayer();
        if (player == null) return;
        var director = player.GetComponent<EnemyDirector>();
        if (director == null) { Debug.LogWarning("Dontsee necesita un EnemyDirector en el jugador.", this); return; }
        var marker = markers[UnityEngine.Random.Range(0, markers.Length)];
        current = Instantiate(prefab, marker.transform.position, marker.transform.rotation, context.Room.transform);
        current.SetDirector(director);
        if (roster != null) roster.ApplyDontseeModifiers(current);
        current.Initialize(rooms.Events, context);
        lastEncounter = context.Index;
        rooms.Events.Publish(new DontseeSpawnedEvent(current, player, context, current.transform.position));
    }
    PlayerController player;
    PlayerController FindPlayer()
    {
        // TowerStarted is sent before the first RoomEntered; see the subscription below.
        return player;
    }
    void OnExited(RoomExitedEvent e)
    {
        if (pendingRoom.Room == e.Context.Room) { pending = false; Clear(DontseeEndReason.RoomExited); }
        // Clean up any orphaned encounter instance attached to the room as well.
        if (e.Context.Room != null)
            foreach (var enemy in e.Context.Room.GetComponentsInChildren<Dontsee>(true))
                if (enemy != null) enemy.Finish(DontseeEndReason.RoomExited);
    }
    public void CancelCurrent() { pending = false; Clear(DontseeEndReason.Cancelled); }
    void Clear(DontseeEndReason reason) { if (current != null) { current.Finish(reason); current = null; } }
    IDisposable started;
    void Awake()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        roster = GetComponent<RoomEnemyRoster>();
        if (rooms != null) started = rooms.Events.Subscribe<TowerStartedEvent>(e => { player = e.Player; lastEncounter = -100000; });
    }
    void OnDestroy() { started?.Dispose(); }
}
