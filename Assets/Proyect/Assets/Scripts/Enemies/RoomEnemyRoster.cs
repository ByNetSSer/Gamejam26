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
    [SerializeField] bool flupinSelected;
    [SerializeField] bool dontseeSelected;
    [SerializeField] bool maskChargerSelected;
    [SerializeField, Min(0f)] float delayAfterLeavingStart = 5f;
    [SerializeField, Min(1)] int maxCopiesPerEnemy = 4;
    struct FlupinModifier { public object Source; public FlupinStat Stat; public StatModifierType Type; public float Value; }
    struct MaskModifier { public object Source; public MaskChargerStat Stat; public StatModifierType Type; public float Value; }
    struct DontseeModifier { public object Source; public DontseeStat Stat; public StatModifierType Type; public float Value; }
    readonly List<FlupinModifier> flupinModifiers = new List<FlupinModifier>();
    readonly List<MaskModifier> maskModifiers = new List<MaskModifier>();
    readonly List<DontseeModifier> dontseeModifiers = new List<DontseeModifier>();
    readonly List<Flupin> extraFlupins = new List<Flupin>();
    readonly List<MaskCharger> extraMasks = new List<MaskCharger>();
    int flupinCount = 1, maskCount = 1;
    readonly List<IDisposable> subscriptions = new List<IDisposable>();
    RoomManager rooms;
    PlayerController player;
    bool eligible, waiting;
    float readyAt;
    public bool DontseeSelected => dontseeSelected;
    public bool FlupinSelected => flupinSelected;
    public bool MaskChargerSelected => maskChargerSelected;
    public int FlupinCount => flupinSelected ? flupinCount : 0;
    public int MaskChargerCount => maskChargerSelected ? maskCount : 0;
    public bool CanChooseFlupin => flupin != null && (!flupinSelected || flupinCount < Mathf.Max(1, maxCopiesPerEnemy));
    public bool CanChooseMaskCharger => maskCharger != null && (!maskChargerSelected || maskCount < Mathf.Max(1, maxCopiesPerEnemy));
    public bool CanChooseDontsee
    {
        get
        {
            var encounter = GetComponent<DontseeEncounter>();
            return encounter != null && encounter.HasPrefab && (!dontseeSelected || encounter.SpawnChance < 0.999f || encounter.SafeRoomsBetween > 0);
        }
    }
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
        if (e.Context.Kind != RoomKind.Intermediate || TowerSafetyRules.IsProtectedRoom(e.Context.Index) || eligible) return;
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
        while (extraMasks.Count < maskCount - 1)
        {
            var copy = Instantiate(maskCharger.gameObject).GetComponent<MaskCharger>();
            copy.gameObject.SetActive(false);
            foreach (var m in maskModifiers) copy.AddModifier(m.Source, m.Stat, m.Type, m.Value);
            extraMasks.Add(copy);
        }
        PlaceMask(maskCharger, director, 0);
        for (int i = 0; i < extraMasks.Count && i < maskCount - 1; i++) PlaceMask(extraMasks[i], director, i + 1);
    }
    void PlaceMask(MaskCharger enemy, EnemyDirector director, int index)
    {
        if (enemy == null || enemy.gameObject.activeSelf) return;
        enemy.transform.SetParent(null, true);
        enemy.transform.position = player.transform.position + Vector3.up * 0.5f + SpawnDirection(index, maskCount) * 10f;
        enemy.SetDirector(director);
        enemy.gameObject.SetActive(true);
    }
    Vector3 SpawnDirection(int index, int count)
    {
        Vector3 direction = Vector3.ProjectOnPlane(-player.transform.forward, Vector3.up).normalized;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.back;
        return Quaternion.Euler(0f, 360f * index / Mathf.Max(1, count), 0f) * direction;
    }
    void SpawnFlupin()
    {
        if (flupin == null || player == null || !player.enabled) return;
        var director = player.GetComponent<EnemyDirector>();
        if (director == null) { Debug.LogWarning("Flupin necesita un EnemyDirector en el jugador.", this); return; }
        while (extraFlupins.Count < flupinCount - 1)
        {
            var copy = Instantiate(flupin.gameObject).GetComponent<Flupin>();
            copy.gameObject.SetActive(false);
            foreach (var m in flupinModifiers) copy.AddModifier(m.Source, m.Stat, m.Type, m.Value);
            extraFlupins.Add(copy);
        }
        PlaceFlupin(flupin, director, 0);
        for (int i = 0; i < extraFlupins.Count && i < flupinCount - 1; i++) PlaceFlupin(extraFlupins[i], director, i + 1);
    }
    void PlaceFlupin(Flupin enemy, EnemyDirector director, int index)
    {
        if (enemy == null || enemy.gameObject.activeSelf) return;
        enemy.transform.SetParent(null, true);
        enemy.transform.position = player.transform.position + Vector3.up * 0.5f + SpawnDirection(index, flupinCount) * enemy.SpawnDistance;
        enemy.SetDirector(director);
        enemy.gameObject.SetActive(true);
    }
    void Stop()
    {
        eligible = false; waiting = false;
        if (flupin != null) flupin.gameObject.SetActive(false);
        if (maskCharger != null) maskCharger.gameObject.SetActive(false);
        foreach (var enemy in extraFlupins) if (enemy != null) enemy.gameObject.SetActive(false);
        foreach (var enemy in extraMasks) if (enemy != null) enemy.gameObject.SetActive(false);
        var encounter = GetComponent<DontseeEncounter>();
        if (encounter != null) encounter.CancelCurrent();
    }
    public void SetFlupinSelected(bool selected)
    {
        if (flupinSelected == selected) return;
        flupinSelected = selected;
        if (!selected) { if (flupin != null) flupin.gameObject.SetActive(false); foreach (var enemy in extraFlupins) if (enemy != null) enemy.gameObject.SetActive(false); }
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
        if (!selected) { if (maskCharger != null) maskCharger.gameObject.SetActive(false); foreach (var enemy in extraMasks) if (enemy != null) enemy.gameObject.SetActive(false); }
        else if (selected && EnemiesReady) SpawnMaskCharger();
    }
    public bool ChooseFlupinCurse()
    {
        if (!CanChooseFlupin) return false;
        if (!flupinSelected) SetFlupinSelected(true); else { flupinCount++; if (EnemiesReady) SpawnFlupin(); }
        return true;
    }
    public bool ChooseMaskCurse()
    {
        if (!CanChooseMaskCharger) return false;
        if (!maskChargerSelected) SetMaskChargerSelected(true); else { maskCount++; if (EnemiesReady) SpawnMaskCharger(); }
        return true;
    }
    public bool ChooseDontseeCurse()
    {
        if (!CanChooseDontsee) return false;
        if (!dontseeSelected) SetDontseeSelected(true); else GetComponent<DontseeEncounter>().IncreaseFrequency();
        return true;
    }
    public void AddMaskChargerModifier(object source, MaskChargerStat stat, StatModifierType type, float value)
    {
        if (source == null) return;
        maskModifiers.Add(new MaskModifier { Source = source, Stat = stat, Type = type, Value = value });
        if (maskCharger != null) maskCharger.AddModifier(source, stat, type, value);
        foreach (var enemy in extraMasks) if (enemy != null) enemy.AddModifier(source, stat, type, value);
    }
    public void RemoveMaskChargerModifiers(object source)
    {
        maskModifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
        if (maskCharger != null) maskCharger.RemoveModifiers(source);
        foreach (var enemy in extraMasks) if (enemy != null) enemy.RemoveModifiers(source);
    }
    public void AddFlupinModifier(object source, FlupinStat stat, StatModifierType type, float value)
    {
        if (source == null) return;
        flupinModifiers.Add(new FlupinModifier { Source = source, Stat = stat, Type = type, Value = value });
        if (flupin != null) flupin.AddModifier(source, stat, type, value);
        foreach (var enemy in extraFlupins) if (enemy != null) enemy.AddModifier(source, stat, type, value);
    }
    public void RemoveFlupinModifiers(object source)
    {
        flupinModifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
        if (flupin != null) flupin.RemoveModifiers(source);
        foreach (var enemy in extraFlupins) if (enemy != null) enemy.RemoveModifiers(source);
    }
    public void AddDontseeModifier(object source, DontseeStat stat, StatModifierType type, float value)
    {
        if (source == null) return;
        dontseeModifiers.Add(new DontseeModifier { Source = source, Stat = stat, Type = type, Value = value });
        var encounter = GetComponent<DontseeEncounter>();
        if (encounter != null && encounter.CurrentEnemy != null) encounter.CurrentEnemy.AddModifier(source, stat, type, value);
        if (encounter != null && encounter.PreparedEnemy != null) encounter.PreparedEnemy.AddModifier(source, stat, type, value);
    }
    public void RemoveDontseeModifiers(object source)
    {
        dontseeModifiers.RemoveAll(m => ReferenceEquals(m.Source, source));
        var encounter = GetComponent<DontseeEncounter>();
        if (encounter != null && encounter.CurrentEnemy != null) encounter.CurrentEnemy.RemoveModifiers(source);
        if (encounter != null && encounter.PreparedEnemy != null) encounter.PreparedEnemy.RemoveModifiers(source);
    }
    public void ApplyDontseeModifiers(Dontsee enemy)
    { if (enemy != null) foreach (var m in dontseeModifiers) enemy.AddModifier(m.Source, m.Stat, m.Type, m.Value); }
    void OnDestroy()
    {
        foreach (var enemy in extraFlupins) if (enemy != null) Destroy(enemy.gameObject);
        foreach (var enemy in extraMasks) if (enemy != null) Destroy(enemy.gameObject);
    }
}
