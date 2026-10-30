using System;
using System.Collections.Generic;
using UnityEngine;

// Persistent selection for a run. The shop/inventory can call the public selection
// and modifier methods when its enemy contracts are implemented.
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(RoomManager))]
public sealed class RoomEnemyRoster : MonoBehaviour
{
    [SerializeField] Flupin flupin;
    [SerializeField] MaskCharger maskCharger;
    [SerializeField] bool flupinSelected = true;
    [SerializeField] bool dontseeSelected = true;
    [SerializeField] bool maskChargerSelected;
    [SerializeField, Min(0f)] float delayAfterLeavingStart = 5f;
    struct DontseeModifier { public object Source; public DontseeStat Stat; public StatModifierType Type; public float Value; }
    readonly List<DontseeModifier> dontseeModifiers = new List<DontseeModifier>();
    readonly List<IDisposable> subscriptions = new List<IDisposable>();
    RoomManager rooms;
    PlayerController player;
    bool eligible, waiting;
    float readyAt;
    public bool DontseeSelected => dontseeSelected;
    public bool EnemiesReady => eligible && Time.time >= readyAt;
    void Awake()
    {
        rooms = GetComponent<RoomManager>();
        if (flupin != null) flupin.gameObject.SetActive(false);
        if (maskCharger != null) maskCharger.gameObject.SetActive(false);
    }
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        subscriptions.Add(rooms.Events.Subscribe<TowerStartedEvent>(OnTowerStarted));
        subscriptions.Add(rooms.Events.Subscribe<RoomEnteredEvent>(OnRoomEntered));
        subscriptions.Add(rooms.Events.Subscribe<TowerCompletedEvent>(_ => Stop()));
        subscriptions.Add(rooms.Events.Subscribe<RunLostEvent>(_ => Stop()));
        subscriptions.Add(rooms.Events.Subscribe<TowerGenerationFailedEvent>(_ => Stop()));
    }
    void OnDisable()
    {
        foreach (var subscription in subscriptions) subscription.Dispose();
        subscriptions.Clear(); Stop();
    }
    void OnTowerStarted(TowerStartedEvent e) { Stop(); player = e.Player; }
    void OnRoomEntered(RoomEnteredEvent e)
    {
        if (e.Context.Kind == RoomKind.Final) { Stop(); return; }
        if (e.Context.Kind != RoomKind.Intermediate || eligible) return;
        eligible = true;
        readyAt = Time.time + Mathf.Max(0f, delayAfterLeavingStart);
        waiting = true;
    }
    void Update()
    {
        if (!waiting || !EnemiesReady || player == null || !player.enabled) return;
        waiting = false;
        if (flupinSelected) SpawnFlupin();
        if (maskChargerSelected) SpawnMaskCharger();
    }
    void SpawnMaskCharger()
    {
        if (maskCharger == null || player == null || !player.enabled) return;
        var director = player.GetComponent<EnemyDirector>();
        if (director == null) { Debug.LogWarning("La máscara necesita un EnemyDirector en el jugador.", this); return; }
        maskCharger.gameObject.SetActive(false);
        maskCharger.transform.SetParent(null, true);
        Vector3 direction = Vector3.ProjectOnPlane(-player.transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.back;
        maskCharger.transform.position = player.transform.position + Vector3.up * 0.5f + direction * 10f;
        maskCharger.SetDirector(director);
        maskCharger.gameObject.SetActive(true);
    }
    void SpawnFlupin()
    {
        if (flupin == null || player == null || !player.enabled) return;
        var director = player.GetComponent<EnemyDirector>();
        if (director == null) { Debug.LogWarning("Flupin necesita un EnemyDirector en el jugador.", this); return; }
        flupin.gameObject.SetActive(false);
        flupin.transform.SetParent(null, true);
        Vector3 direction = Vector3.ProjectOnPlane(-player.transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.back;
        flupin.transform.position = player.transform.position + Vector3.up * 0.5f + direction * flupin.SpawnDistance;
        flupin.SetDirector(director);
        flupin.gameObject.SetActive(true);
    }
    void Stop()
    {
        eligible = false; waiting = false;
        if (flupin != null) flupin.gameObject.SetActive(false);
        if (maskCharger != null) maskCharger.gameObject.SetActive(false);
        var encounter = GetComponent<DontseeEncounter>();
        if (encounter != null) encounter.CancelCurrent();
    }
    public void SetFlupinSelected(bool selected)
    {
        if (flupinSelected == selected) return;
        flupinSelected = selected;
        if (!selected && flupin != null) flupin.gameObject.SetActive(false);
        else if (selected && EnemiesReady) SpawnFlupin();
    }
    public void SetDontseeSelected(bool selected)
    {
        dontseeSelected = selected;
        if (!selected) { var encounter = GetComponent<DontseeEncounter>(); if (encounter != null) encounter.CancelCurrent(); }
    }
    public void SetMaskChargerSelected(bool selected)
    {
        if (maskChargerSelected == selected) return;
        maskChargerSelected = selected;
        if (!selected && maskCharger != null) maskCharger.gameObject.SetActive(false);
        else if (selected && EnemiesReady) SpawnMaskCharger();
    }
    public void AddMaskChargerModifier(object source, MaskChargerStat stat, StatModifierType type, float value)
    { if (maskCharger != null && source != null) maskCharger.AddModifier(source, stat, type, value); }
    public void RemoveMaskChargerModifiers(object source) { if (maskCharger != null) maskCharger.RemoveModifiers(source); }
    public void AddFlupinModifier(object source, FlupinStat stat, StatModifierType type, float value)
    { if (flupin != null && source != null) flupin.AddModifier(source, stat, type, value); }
    public void RemoveFlupinModifiers(object source) { if (flupin != null) flupin.RemoveModifiers(source); }
    public void AddDontseeModifier(object source, DontseeStat stat, StatModifierType type, float value)
    {
        if (source == null) return;
        dontseeModifiers.Add(new DontseeModifier { Source = source, Stat = stat, Type = type, Value = value });
        var encounter = GetComponent<DontseeEncounter>();
        if (encounter != null && encounter.CurrentEnemy != null) encounter.CurrentEnemy.AddModifier(source, stat, type, value);
    }
    public void RemoveDontseeModifiers(object source)
    {
        dontseeModifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
        var encounter = GetComponent<DontseeEncounter>();
        if (encounter != null && encounter.CurrentEnemy != null) encounter.CurrentEnemy.RemoveModifiers(source);
    }
    public void ApplyDontseeModifiers(Dontsee enemy)
    { if (enemy != null) foreach (var m in dontseeModifiers) enemy.AddModifier(m.Source, m.Stat, m.Type, m.Value); }
}
