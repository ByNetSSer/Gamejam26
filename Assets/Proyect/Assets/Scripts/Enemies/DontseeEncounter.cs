using System;
using System.Collections.Generic;
using UnityEngine;

public enum DontseeEncounterStat { SpawnChance }

// Prepare encounters in the next room, before the player enters.
public sealed class DontseeEncounter : MonoBehaviour
{
    [SerializeField] RoomManager rooms;
    [SerializeField] Dontsee prefab;
    [SerializeField, Range(0f, 1f)] float chance = 0.35f;
    [SerializeField, Min(0)] int safeRoomsBetween = 2;
    IDisposable entered, exited;
    Dontsee current, prepared;
    RoomDefinition attemptedRoom;
    RoomEnemyRoster roster;
    int frequencyCurses;
    public Dontsee CurrentEnemy => current;
    public Dontsee PreparedEnemy => prepared;
    public bool HasPrefab => prefab != null;
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
    public float SpawnChance => Mathf.Clamp01(GetStat(DontseeEncounterStat.SpawnChance).Calculate(chance) + frequencyCurses * 0.15f);
    public int SafeRoomsBetween => Mathf.Max(0, safeRoomsBetween - frequencyCurses);
    public void IncreaseFrequency() => frequencyCurses++;
    int lastEncounter = -100000;
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        if (rooms == null) return;
        entered = rooms.Events.Subscribe<RoomEnteredEvent>(OnEntered);
        exited = rooms.Events.Subscribe<RoomExitedEvent>(OnExited);
    }
    void OnDisable() { entered?.Dispose(); exited?.Dispose(); entered = exited = null; Clear(DontseeEndReason.Disabled); }
    public void Configure(RoomManager manager, Dontsee enemy) { rooms = manager; prefab = enemy; }
    void OnEntered(RoomEnteredEvent e)
    {
        Clear(DontseeEndReason.RoomExited, false);
        if (prepared != null && prepared.transform.parent == e.Context.Room.transform)
        {
            current = prepared;
            prepared = null;
            current.EnterRoom();
        }
    }
    void Update()
    {
        if (rooms == null || !rooms.TimerRunning || rooms.NextRoom == null || rooms.NextRoom == attemptedRoom) return;
        if (roster != null && !roster.DontseeSelected) return;
        attemptedRoom = rooms.NextRoom;
        int index = rooms.RoomsEntered + 1;
        if (index >= rooms.RoomsPerTower || TowerSafetyRules.IsProtectedRoom(index)) return;
        TrySpawn(new RoomContext(attemptedRoom, index, rooms.RoomsPerTower, RoomKind.Intermediate));
    }
    void TrySpawn(RoomContext context)
    {
        if (prefab == null || context.Room == null || context.Room != rooms.NextRoom || prepared != null || context.Index - lastEncounter <= SafeRoomsBetween) return;
        var markers = context.Room.GetComponentsInChildren<DontseeSpawnPoint>();
        if (markers.Length == 0 || UnityEngine.Random.value >= SpawnChance) return;
        var player = FindPlayer();
        if (player == null) return;
        var director = player.GetComponent<EnemyDirector>();
        if (director == null) { Debug.LogWarning("Dontsee necesita un EnemyDirector en el jugador.", this); return; }
        var marker = markers[UnityEngine.Random.Range(0, markers.Length)];
        prepared = Instantiate(prefab, marker.transform.position, marker.transform.rotation, context.Room.transform);
        prepared.SetDirector(director);
        if (roster != null) roster.ApplyDontseeModifiers(prepared);
        prepared.Initialize(rooms.Events, context);
        lastEncounter = context.Index;
        rooms.Events.Publish(new DontseeSpawnedEvent(prepared, player, context, prepared.transform.position));
    }
    PlayerController player;
    PlayerController FindPlayer()
    {
        // TowerStarted is sent before the first RoomEntered; see the subscription below.
        return player;
    }
    void OnExited(RoomExitedEvent e)
    {
        Clear(DontseeEndReason.RoomExited, e.Reason != RoomExitReason.Advanced);
        // Clean up any orphaned encounter instance attached to the room as well.
        if (e.Context.Room != null)
            foreach (var enemy in e.Context.Room.GetComponentsInChildren<Dontsee>(true))
                if (enemy != null) enemy.Finish(DontseeEndReason.RoomExited);
    }
    public void CancelCurrent() { Clear(DontseeEndReason.Cancelled); }
    void Clear(DontseeEndReason reason, bool includePrepared = true)
    {
        if (current != null) { current.Finish(reason); current = null; }
        if (includePrepared && prepared != null) { prepared.Finish(reason); prepared = null; }
    }
    IDisposable started;
    void Awake()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        roster = GetComponent<RoomEnemyRoster>();
        if (rooms != null) started = rooms.Events.Subscribe<TowerStartedEvent>(e => { player = e.Player; lastEncounter = -100000; attemptedRoom = null; });
    }
    void OnDestroy() { started?.Dispose(); }
}
