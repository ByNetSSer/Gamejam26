using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public readonly struct WallLeverProgressEvent
{
    public readonly WallLeverEncounter Encounter;
    public readonly int Activated, Total;
    public readonly Vector3 Position;
    public WallLeverProgressEvent(WallLeverEncounter encounter, int activated, int total, Vector3 position)
    { Encounter = encounter; Activated = activated; Total = total; Position = position; }
}
public readonly struct WallLeverOpenedEvent
{
    public readonly WallLeverEncounter Encounter;
    public readonly Vector3 Position;
    public WallLeverOpenedEvent(WallLeverEncounter encounter, Vector3 position) { Encounter = encounter; Position = position; }
}

// Experimental authored layout: no attempt to infer reachability from tile meshes.
public sealed class WallLeverEncounter : EnemyAgent
{
    [SerializeField] GameObject barrier;
    [SerializeField] WallLever leverPrefab;
    [SerializeField] Transform[] leverPositions;
    [SerializeField, Min(1)] int leverCount = 3;
    [SerializeField, Range(0f, 1f)] float chance = 1f;
    [SerializeField, Min(0.1f)] float interactionRange = 3f;
    readonly List<WallLever> levers = new List<WallLever>();
    RoomManager rooms;
    RoomDefinition room;
    IDisposable entered, exited;
    Camera view;
    WallLever focused;
    bool roomActive, selected, active, finished;
    int activated;
    public bool CanActivateLevers => active && !finished;
    public override AttackRequirement Requirement => AttackRequirement.Any;
    public override bool CanRequestAttack => selected && roomActive && !active && !finished;
    public override bool CanBeRepelled => false;
    public override bool UsesAttackTimeout => false;
    public override bool TryRepel(float power) => false;
    public void Configure(GameObject wall, WallLever prefab, Transform[] positions, int count)
    { barrier = wall; leverPrefab = prefab; leverPositions = positions; leverCount = count; }
    void Awake() { if (barrier != null) barrier.SetActive(false); }
    protected override void Start()
    {
        base.Start();
        room = GetComponentInParent<RoomDefinition>();
        rooms = GetComponentInParent<RoomManager>();
        if (room != null && rooms != null)
        {
            if (rooms.Player != null) SetDirector(rooms.Player.GetComponent<EnemyDirector>());
            entered = rooms.Events.Subscribe<RoomEnteredEvent>(e => { if (e.Context.Room == room && !TowerSafetyRules.IsProtectedRoom(e.Context.Index)) Begin(); });
            exited = rooms.Events.Subscribe<RoomExitedEvent>(e => { if (e.Context.Room == room) End(); });
            if (rooms.CurrentRoom == room && !TowerSafetyRules.IsProtectedRoom(rooms.RoomsEntered)) Begin();
        }
        else Begin();
    }
    void Begin()
    {
        if (roomActive || finished) return;
        roomActive = true;
        var choices = new List<Transform>();
        if (leverPositions != null) foreach (var marker in leverPositions)
            if (marker != null && !choices.Contains(marker)) choices.Add(marker);
        if (barrier == null || leverPrefab == null || leverCount < 1 || choices.Count < leverCount || Director == null ||
            !HasSolidCollider(barrier) || !HasSolidCollider(leverPrefab.gameObject))
        { Debug.LogWarning("Pared de palancas: configuración incompleta; se deja libre el paso.", this); finished = true; return; }
        if (UnityEngine.Random.value >= Mathf.Clamp01(chance)) { finished = true; return; }
        selected = true;
        for (int i = 0; i < leverCount; i++)
        {
            int index = UnityEngine.Random.Range(i, choices.Count);
            var swap = choices[i]; choices[i] = choices[index]; choices[index] = swap;
            var lever = Instantiate(leverPrefab, choices[i].position, choices[i].rotation, transform);
            lever.Initialize(this); lever.gameObject.SetActive(true); levers.Add(lever);
        }
        barrier.SetActive(true);
    }
    static bool HasSolidCollider(GameObject target)
    {
        foreach (var collider in target.GetComponentsInChildren<Collider>(true))
            if (collider.enabled && !collider.isTrigger) return true;
        return false;
    }
    void Update()
    {
        focused = null;
        if (finished || !selected || Player == null || !Player.enabled) return;
        if (!active) { RequestAttack(); return; }
        if (view == null) view = Player.GetComponentInChildren<Camera>();
        if (view == null || Cursor.lockState != CursorLockMode.Locked) return;
        if (Physics.Raycast(view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var hit, interactionRange, ~0, QueryTriggerInteraction.Ignore))
        {
            var lever = hit.collider.GetComponentInParent<WallLever>();
            if (lever != null && levers.Contains(lever) && !lever.Activated) focused = lever;
        }
        if (focused != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        { focused.Activate(); focused = null; }
    }
    public override void AttackGranted() { if (!CanRequestAttack) { ReleaseAttack(); return; } active = true; }
    public override void AttackCancelled() => End();
    public void LeverActivated(WallLever lever)
    {
        if (!CanActivateLevers || !levers.Contains(lever)) return;
        activated++;
        rooms?.Events.Publish(new WallLeverProgressEvent(this, activated, levers.Count, lever.transform.position));
        if (activated < levers.Count || finished) return;
        if (barrier != null) barrier.SetActive(false);
        finished = true; active = false; ReleaseAttack();
        rooms?.Events.Publish(new WallLeverOpenedEvent(this, transform.position));
    }
    void End()
    {
        if (finished) return;
        finished = true; active = false; ReleaseAttack();
        if (barrier != null) barrier.SetActive(false);
        foreach (var lever in levers) if (lever != null) Destroy(lever.gameObject);
        levers.Clear();
    }
    protected override void OnDisable()
    {
        entered?.Dispose(); exited?.Dispose(); entered = exited = null;
        if (Application.isPlaying) End();
        base.OnDisable();
    }
    void OnGUI()
    {
        if (!selected || finished) return;
        GUI.Box(new Rect(20, 100, 250, 28), active ? $"Palancas: {activated}/{levers.Count}" : "Pared: esperando turno");
        if (focused != null) GUI.Box(new Rect(Screen.width * 0.5f - 70f, Screen.height * 0.5f + 24f, 140f, 26f), "E - Activar palanca");
    }
    void OnDrawGizmos()
    {
        if (leverPositions == null) return;
        Gizmos.color = Color.yellow;
        foreach (var marker in leverPositions) if (marker != null) Gizmos.DrawWireSphere(marker.position, 0.25f);
    }
}
