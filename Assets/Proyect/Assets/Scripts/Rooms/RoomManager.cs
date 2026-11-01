using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
[RequireComponent(typeof(GameEventBus))]
public sealed class RoomManager : MonoBehaviour
{
    [Header("Salas especiales")]
    [SerializeField] PlayerController player;
    [SerializeField] RoomDefinition startingRoom;
    [SerializeField] RoomDefinition finalRoom;
    [Header("Pools por entrada y salida")]
    [SerializeField] RoomDefinition[] hatchToHatch;
    [SerializeField] RoomDefinition[] hatchToDoor;
    [SerializeField] RoomDefinition[] doorToDoor;
    [SerializeField] RoomDefinition[] doorToHatch;
    [Header("Elementos compartidos")]
    [SerializeField] ImpactTarget hatchPrefab;
    [SerializeField] RoomDoor doorPrefab;
    [Header("Tiempo por torre")][SerializeField, Min(1f)] float towerTimeSeconds = 180f;
    [SerializeField, Min(0f)] float mediumRoomExtraSeconds = 12f;
    [SerializeField, Range(0f, 1f)] float mediumRoomSpawnMultiplier = 0.65f;
    public float TimeRemaining { get; private set; }
    public bool TimerRunning => isActiveAndEnabled && started && !finished && !failed && !lost && !victoryReported
        && towerNumber > 1 && roomsEntered > 1 && roomsEntered < RoomsPerTower;
    [Header("Torre (incluye inicial y final)")]
    [SerializeField, Min(2)] int roomsPerTower = 6;
    [SerializeField] TowerDifficulty difficulty = new TowerDifficulty();
    [SerializeField] bool useFixedSeed;
    [SerializeField] int seed = 12345;
    [Header("Rotaci贸n al bajar por trampilla")]
    [Tooltip("Direcciones permitidas al bajar. El hueco de entrada debe encajar tras girar.")]
    [SerializeField] HatchRotations hatchRotations = HatchRotations.All;
    [Tooltip("Separaci贸n vertical al conectar salas por trampilla, para evitar superficies coincidentes.")]
    [SerializeField, Min(0f)] float hatchVerticalGap = 0.05f;
    // Retain old serialized data for scenes made with the earlier single-pool version.
    [SerializeField, HideInInspector] RoomDefinition[] roomPrefabs;
    readonly List<RoomDefinition> validHatchToHatch = new List<RoomDefinition>();
    readonly List<RoomDefinition> validHatchToDoor = new List<RoomDefinition>();
    readonly List<RoomDefinition> validDoorToDoor = new List<RoomDefinition>();
    readonly List<RoomDefinition> validDoorToHatch = new List<RoomDefinition>();
    readonly List<RoomRoute> routes = new List<RoomRoute>();
    readonly List<Flupin> pausedFlupins = new List<Flupin>();
    RoomDefinition previous, current, next;
    System.Random random;
    int roomsEntered;
    int towerNumber = 1;
    int additionalRooms;
    readonly object towerSpeedSource = new object();
    bool finished, failed, lost, started, pendingDefeat, roomExitSent, victoryReported, linkedNextTower;
    GameEventBus eventBus;
    PlayerHealth health;
    bool healthBound;
    public int RoomsEntered => roomsEntered;
    public RoomDefinition CurrentRoom => current;
    public RoomDefinition PreviousRoom => previous;
    public RoomDefinition NextRoom => next;
    public PlayerController Player => player;
    public int RoomsPerTower => difficulty.Rooms(roomsPerTower, towerNumber) + additionalRooms;
    public int TowerNumber => towerNumber;
    public int TotalTowers => MenuUIBase.Core.RunDifficulty.Towers;
    public bool IsFinalTower => TowerSafetyRules.IsFinalTower(towerNumber, TotalTowers);
    public bool IsFinished => finished;
    public bool HasLinkedNextTower => linkedNextTower;
    public bool IsLost => lost;
    public bool IsWon => victoryReported;
    public void SetStartingExitLocked(bool locked)
    {
        if (roomsEntered == 1 && current != null && current.Exit != null) current.Exit.SetLocked(locked);
    }
    public GameEventBus Events => eventBus != null ? eventBus : GetComponent<GameEventBus>();
    public string Status => failed ? "Configuraci贸n de salas incompleta" : lost ? "Derrota" : victoryReported ? "Partida ganada" : finished ? "Torre completada" : $"Sala {roomsEntered}/{RoomsPerTower}";
    public event Action<int> RoomEntered;
    public event Action TowerFinished;

    public void Configure(PlayerController target, RoomDefinition start, RoomDefinition end,
        RoomDefinition[] hh, RoomDefinition[] hd, RoomDefinition[] dd, RoomDefinition[] dh,
        int roomCount, ImpactTarget sharedHatch, RoomDoor sharedDoor)
    {
        player = target; startingRoom = start; finalRoom = end;
        hatchToHatch = hh; hatchToDoor = hd; doorToDoor = dd; doorToHatch = dh;
        roomsPerTower = Mathf.Max(2, roomCount); hatchPrefab = sharedHatch; doorPrefab = sharedDoor;
    }
    void Awake()
    {
        eventBus = GetComponent<GameEventBus>();
        if (eventBus == null) eventBus = gameObject.AddComponent<GameEventBus>();
        if (GetComponent<GameplayMusicDirector>() == null) gameObject.AddComponent<GameplayMusicDirector>();
        if (GetComponent<RoomFallRecovery>() == null) gameObject.AddComponent<RoomFallRecovery>();
    }
    void OnEnable()
    {
        if (!started || failed || lost || victoryReported) return;
        BindHealth();
        if (health != null && health.CurrentHealth <= 0f) OnPlayerDied();
    }
    void OnDisable() => UnbindHealth();
    void BindHealth()
    {
        if (healthBound || player == null) return;
        health = player.GetComponent<PlayerHealth>();
        if (health != null) { health.Died += OnPlayerDied; healthBound = true; }
    }
    void UnbindHealth()
    {
        if (healthBound && health != null) health.Died -= OnPlayerDied;
        healthBound = false;
    }
    void Start()
    {
        if (player == null || startingRoom == null || !startingRoom.IsStartingRoomConfigured || !startingRoom.CanPrepare(hatchPrefab, doorPrefab))
        { Fail("La inicial necesita jugador, posici贸n inicial y salida de trampilla v谩lida."); return; }
        if (finalRoom == null || !finalRoom.IsFinalRoomConfigured)
        { Fail("Asigna una sala final con entrada de trampilla y sin salida."); return; }
        BuildPools();
        if (!RoomRoutePlanner.TryReachAtLeast(RoomConnection.Hatch, RoomsPerTower - 2, RoomConnection.Hatch, routes, out int initialCount))
        { Fail("Los pools no contienen una ruta desde la sala inicial hasta la entrada de la sala final."); return; }
        if (initialCount + 2 > MenuUIBase.Core.RunDifficulty.MaximumRooms) { Fail("Los pools no permiten respetar el m醲imo de pisos de esta dificultad."); return; }
        additionalRooms += initialCount - (RoomsPerTower - 2);
        random = useFixedSeed ? new System.Random(seed) : new System.Random();
        StartTower();
    }
    void StartTower()
    {
        current = Instantiate(startingRoom, transform.position, transform.rotation, transform);
        if (!current.ConfigureSafeRoom(true)) { Fail("No se pudo preparar la zona segura inicial."); return; }
        var safeRoom = current.GetComponent<SafeRoomLayout>();
        if (safeRoom != null) safeRoom.SetFirstRoomTutorialActive(towerNumber == 1);
        if (!current.Prepare(hatchPrefab, doorPrefab)) { Fail("No se pudo preparar la salida de la sala inicial."); return; }
        ActivateTower(true);
    }
    void ActivateTower(bool placeAtSpawn)
    {
        player.SnapshotTowerSpeed();
        current.Populate(random, difficulty.SpawnMultiplier(towerNumber), false);
        roomsEntered = 1;
        if (placeAtSpawn)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(current.PlayerStart.position, current.PlayerStart.rotation);
            player.ResetMotion();
            if (cc != null) cc.enabled = true;
        }
        foreach (var flupin in pausedFlupins) if (flupin != null) flupin.enabled = true;
        pausedFlupins.Clear();
        TimeRemaining = Mathf.Max(1f, towerTimeSeconds);
        PrepareNext();
        if (failed) return;
        BindHealth();
        Events.Publish(new TowerStartedEvent(RoomsPerTower, player));
        PublishEntered();
        started = true;
        if (pendingDefeat || (health != null && health.CurrentHealth <= 0f)) OnPlayerDied();
    }
    // Prepare the connected safe room while the player can still shop.
    void PrepareLinkedNextTower()
    {
        linkedNextTower = false;
        if (IsFinalTower || current == null || startingRoom == null) return;
        var shop = current.GetComponent<SafeRoomLayout>();
        if (shop == null || startingRoom.GetComponent<SafeRoomLayout>() == null) return;
        int baseCount = difficulty.Rooms(roomsPerTower, towerNumber + 1);
        BuildPools();
        if (!RoomRoutePlanner.TryReachAtLeast(RoomConnection.Hatch, baseCount - 2, RoomConnection.Hatch, routes, out int reachable) ||
            reachable + 2 > MenuUIBase.Core.RunDifficulty.MaximumRooms)
        { Debug.LogError("No hay una ruta v醠ida para la siguiente torre dentro del m醲imo de pisos.", this); return; }
        var candidate = Instantiate(startingRoom, transform);
        var safe = candidate.GetComponent<SafeRoomLayout>();
        if (safe == null || !safe.ConfigureLinkedStart(candidate) || !candidate.Prepare(hatchPrefab, doorPrefab))
        {
            candidate.gameObject.SetActive(false); Destroy(candidate);
            Debug.LogError("No se pudo preparar la zona segura conectada; se mantiene el avance provisional.", this);
            return;
        }
        if (!shop.ConfigureShopExit(current) || !current.Prepare(hatchPrefab, doorPrefab))
        {
            candidate.gameObject.SetActive(false); Destroy(candidate);
            Debug.LogError("No se pudo preparar la salida de la tienda; se mantiene el avance provisional.", this);
            return;
        }
        float yaw = RoomRotationSelector.ChooseQuarterTurns(random, hatchRotations) * 90f;
        candidate.AlignTo(current.Exit.Anchor, yaw);
        candidate.transform.position += Vector3.down * Mathf.Max(0f, hatchVerticalGap);
        next = candidate;
        linkedNextTower = true;
    }
    public bool StartNextTower()
    {
        if (!started || !finished || lost || failed || IsFinalTower || player == null || health == null || health.CurrentHealth <= 0f) return false;
        if (linkedNextTower && (current.Exit == null || !current.Exit.HasPassed(player.transform.position))) return false;
        int following = towerNumber + 1;
        BuildPools();
        int baseCount = difficulty.Rooms(roomsPerTower, following);
        if (!RoomRoutePlanner.TryReachAtLeast(RoomConnection.Hatch, baseCount - 2, RoomConnection.Hatch, routes, out int reachableCount) || reachableCount + 2 > MenuUIBase.Core.RunDifficulty.MaximumRooms)
        { Debug.LogError("Los pools no permiten respetar el m醲imo de pisos de esta dificultad.", this); return false; }
        bool connected = linkedNextTower && next != null;
        if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); previous = null; }
        if (connected)
        {
            current.Exit.CloseBehind();
            previous = current;
            current = next;
            next = null;
        }
        else
        {
            if (current != null) { current.gameObject.SetActive(false); Destroy(current.gameObject); current = null; }
            if (next != null) { next.gameObject.SetActive(false); Destroy(next.gameObject); next = null; }
        }
        linkedNextTower = false;
        towerNumber = following; additionalRooms = reachableCount - (baseCount - 2); finished = false; roomExitSent = false;
        player.Stats.RemoveModifiers(towerSpeedSource);
        player.Stats.AddModifier(towerSpeedSource, PlayerStat.MoveSpeed, StatModifierType.Percent, difficulty.SpeedBonus(towerNumber));
        player.enabled = true;
        if (connected) ActivateTower(false);
        else StartTower();
        return !failed;
    }
    void BuildPools()
    {
        routes.Clear();
        validHatchToHatch.Clear(); validHatchToDoor.Clear(); validDoorToDoor.Clear(); validDoorToHatch.Clear();
        AddPool(hatchToHatch, RoomConnection.Hatch, RoomConnection.Hatch, validHatchToHatch);
        AddPool(hatchToDoor, RoomConnection.Hatch, RoomConnection.Door, validHatchToDoor);
        AddPool(doorToDoor, RoomConnection.Door, RoomConnection.Door, validDoorToDoor);
        AddPool(doorToHatch, RoomConnection.Door, RoomConnection.Hatch, validDoorToHatch);
        if (routes.Count == 0 && roomPrefabs != null && roomPrefabs.Length > 0)
        {
            Debug.LogWarning("RoomManager: se est谩 usando la lista antigua; mueve las salas a sus cuatro pools.", this);
            foreach (var prefab in roomPrefabs)
                if (prefab != null) AddPool(new[] { prefab }, prefab.EntryType, prefab.ExitType, GetPool(prefab.EntryType, prefab.ExitType));
        }
    }
    void AddPool(RoomDefinition[] prefabs, RoomConnection entry, RoomConnection exit, List<RoomDefinition> destination)
    {
        if (prefabs == null) return;
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            if (!prefab.IsConfigured || prefab.EntryType != entry || prefab.ExitType != exit || !prefab.CanPrepare(hatchPrefab, doorPrefab))
            {
                Debug.LogWarning($"RoomManager: se omiti贸 '{prefab.name}': revisa su pool, marcadores, escala y prefabs de puertas/trampillas.", prefab);
                continue;
            }
            destination.Add(prefab);
            routes.Add(new RoomRoute(entry, exit));
        }
    }
    List<RoomDefinition> GetPool(RoomConnection entry, RoomConnection exit)
    {
        if (entry == RoomConnection.Hatch) return exit == RoomConnection.Hatch ? validHatchToHatch : validHatchToDoor;
        return exit == RoomConnection.Door ? validDoorToDoor : validDoorToHatch;
    }
    List<RoomDefinition> CandidatesFor(RoomConnection entry, int remaining)
    {
        var valid = new List<RoomDefinition>();
        foreach (var output in new[] { RoomConnection.Hatch, RoomConnection.Door })
            if (RoomRoutePlanner.CanReach(output, remaining - 2, RoomConnection.Hatch, routes))
                valid.AddRange(GetPool(entry, output));
        return valid;
    }
    RoomContext CurrentContext()
    {
        var kind = roomsEntered == 1 ? RoomKind.Initial : roomsEntered == RoomsPerTower ? RoomKind.Final : RoomKind.Intermediate;
        return new RoomContext(current, roomsEntered, RoomsPerTower, kind);
    }
    long CurrentScore()
    {
        var score = player != null ? player.GetComponent<PlayerScore>() : null;
        return score != null ? score.TotalPoints : 0L;
    }
    void PublishEntered()
    {
        roomExitSent = false;
        Events.Publish(new RoomEnteredEvent(CurrentContext()));
        if (finished) return;
        try { RoomEntered?.Invoke(roomsEntered); } catch (Exception e) { Debug.LogException(e); }
    }
    void PublishExited(RoomExitReason reason)
    {
        if (roomExitSent || current == null) return;
        roomExitSent = true;
        Events.Publish(new RoomExitedEvent(CurrentContext(), reason));
    }
    void OnPlayerDied() => LoseRun(health != null ? health.LastDamageReason : RunLossReason.PlayerHealthDepleted);
    void LoseRun(RunLossReason reason)
    {
        if (lost || failed || victoryReported) return;
        if (!started) { pendingDefeat = true; return; }
        finished = true; lost = true;
        UnbindHealth();
        if (player != null) player.enabled = false;
        PublishExited(RoomExitReason.Defeated);
        Events.Publish(new RunLostEvent(CurrentContext(), CurrentScore(), reason));
    }
    public bool DeclareRunVictory()
    {
        if (!TowerSafetyRules.CanDeclareVictory(towerNumber, TotalTowers, started, finished, lost, failed, victoryReported)) return false;
        victoryReported = true;
        UnbindHealth();
        Events.Publish(new RunWonEvent(RoomsPerTower, CurrentScore()));
        return true;
    }
    void Update()
    {
        if (started && !lost && !failed && !victoryReported && health != null && health.CurrentHealth <= 0f) OnPlayerDied();
        if (failed || lost || victoryReported || current == null || player == null) return;
        if (finished)
        {
            if (!lost && !victoryReported && linkedNextTower && current.Exit != null && current.Exit.HasPassed(player.transform.position)) StartNextTower();
            return;
        }
        if (roomsEntered == RoomsPerTower)
        {
            if (!player.IsGrounded) return;
            CompleteRoom();
            if (finished) return;
            finished = true;
            var director = player.GetComponent<EnemyDirector>();
            if (director != null)
                foreach (var enemy in new List<EnemyAgent>(director.Enemies))
                    if (enemy is Flupin flupin && flupin.enabled) { pausedFlupins.Add(flupin); flupin.enabled = false; }
            PublishExited(RoomExitReason.Completed);
            Events.Publish(new TowerCompletedEvent(RoomsPerTower, CurrentScore()));
            if (!lost && !failed && !victoryReported && !IsFinalTower) PrepareLinkedNextTower();
            try { TowerFinished?.Invoke(); } catch (Exception e) { Debug.LogException(e); }
            return;
        }
        if (!current.Exit.HasPassed(player.transform.position)) return;
        if (next == null) { Fail("No se prepar贸 la siguiente sala; se detuvo la progresi贸n."); return; }
        PublishExited(RoomExitReason.Advanced);
        if (finished) return;
        CompleteRoom();
        if (finished) return;
        current.Exit.CloseBehind();
        if (previous != null) { previous.gameObject.SetActive(false); Destroy(previous.gameObject); }
        previous = current;
        current = next;
        next = null;
        roomsEntered++;
        PublishEntered();
        if (!finished) PrepareNext();
    }
    // Resolve room transitions first: entering the final safe room beats expiry this frame,
    // even while the player is still airborne and tower completion awaits landing.
    void LateUpdate()
    {
        if (!TimerRunning) return;
        TimeRemaining = Mathf.Max(0f, TimeRemaining - Time.deltaTime);
        if (TimeRemaining <= 0f) LoseRun(RunLossReason.TimeExpired);
    }
    void CompleteRoom()
    {
        player.RegisterRoomCleared();
        if (finished) return;
        var score = player.GetComponent<PlayerScore>();
        if (score != null) score.Award("Sala completada", 1);
    }
    void PrepareNext()
    {
        int remaining = RoomsPerTower - roomsEntered;
        if (remaining <= 0) return;
        var entry = current.Exit.Connection;
        // Pools are runtime caches; rebuild if a reload or configuration change
        // leaves no route at the expected length, then extend if necessary.
        int minimumIntermediate = remaining - 1;
        if (!RoomRoutePlanner.CanReach(entry, minimumIntermediate, RoomConnection.Hatch, routes))
        {
            BuildPools();
            if (!RoomRoutePlanner.TryReachAtLeast(entry, minimumIntermediate, RoomConnection.Hatch, routes, out int reachable))
            {
                Fail($"No existe una ruta desde {entry} hasta la entrada de la sala final; revisa los prefabs de los pools.");
                return;
            }
            int extra = reachable - minimumIntermediate;
            if (extra > 0)
            {
                additionalRooms += extra;
                remaining += extra;
                Debug.Log($"RoomManager: se a帽adieron {extra} salas para conectar con la sala final.", this);
            }
        }
        RoomDefinition chosen;
        if (remaining == 1)
        {
            if (current.Exit.Connection != RoomConnection.Hatch) { Fail("La sala final solo acepta entrada por trampilla."); return; }
            chosen = finalRoom;
        }
        else
        {
            var valid = CandidatesFor(entry, remaining);
            if (valid.Count == 0)
            {
                BuildPools();
                if (!RoomRoutePlanner.TryReachAtLeast(entry, remaining - 1, RoomConnection.Hatch, routes, out int reachable))
                { Fail($"No existe una ruta desde {entry} hasta la sala final; revisa los prefabs de los pools."); return; }
                int extra = reachable - (remaining - 1);
                additionalRooms += extra;
                remaining += extra;
                if (extra > 0) Debug.Log($"RoomManager: se a帽adieron {extra} salas para conectar con la sala final.", this);
                valid = CandidatesFor(entry, remaining);
            }
            if (valid.Count == 0)
            { Fail($"No hay un prefab v谩lido con entrada {entry} que permita alcanzar la sala final; revisa los avisos de los pools."); return; }
            var small = new List<RoomDefinition>();
            var medium = new List<RoomDefinition>();
            var large = new List<RoomDefinition>();
            foreach (var candidate in valid)
            {
                var layout = candidate.GetComponent<RoomAutoLayout>();
                if (layout != null && layout.Size == RoomSize.Large) large.Add(candidate);
                else if (layout != null && layout.Size == RoomSize.Medium) medium.Add(candidate);
                else small.Add(candidate);
            }
            var pool = small;
            if (large.Count > 0 && random.NextDouble() < difficulty.LargeRoomChance(towerNumber)) pool = large;
            else if (medium.Count > 0 && random.NextDouble() < difficulty.MediumRoomChance(towerNumber)) pool = medium;
            if (pool.Count == 0) pool = medium.Count > 0 ? medium : large;
            chosen = pool[random.Next(pool.Count)];
            if (pool == medium && towerNumber > 1) TimeRemaining += Mathf.Max(0f, mediumRoomExtraSeconds);
        }
        next = Instantiate(chosen, transform);
        if (remaining == 1 && !next.ConfigureSafeRoom(false))
        { next.gameObject.SetActive(false); Destroy(next.gameObject); next = null; Fail("No se pudo preparar la entrada de la zona segura final."); return; }
        if (remaining == 1 && IsFinalTower)
        {
            var safe = next.GetComponent<SafeRoomLayout>();
            if (safe != null) safe.SetAltarsActive(false);
        }
        if (next.EntryAnchor == null || next.ExitMarker == null && remaining > 1)
        {
            next.gameObject.SetActive(false); Destroy(next.gameObject); next = null;
            Fail("No se pudieron identificar las conexiones de la sala seleccionada."); return;
        }
        // Doors align directly; a hatch may turn the next room without moving the shared opening.
        float yaw = current.Exit.Connection == RoomConnection.Hatch
            ? RoomRotationSelector.ChooseQuarterTurns(random, hatchRotations) * 90f : 0f;
        next.AlignTo(current.Exit.Anchor, yaw);
        if (current.Exit.Connection == RoomConnection.Hatch)
            next.transform.position += Vector3.down * Mathf.Max(0f, hatchVerticalGap);
        if (remaining > 1 && !next.Prepare(hatchPrefab, doorPrefab))
        { next.gameObject.SetActive(false); Destroy(next.gameObject); next = null; Fail("No se pudo preparar la salida de la siguiente sala."); return; }
        var nextLayout = next.GetComponent<RoomAutoLayout>();
        float roomSpawnMultiplier = nextLayout != null && nextLayout.Size == RoomSize.Medium ? Mathf.Clamp01(mediumRoomSpawnMultiplier) : 1f;
        next.Populate(random, difficulty.SpawnMultiplier(towerNumber) * roomSpawnMultiplier, !TowerSafetyRules.IsProtectedRoom(roomsEntered + 1));
    }
    void Fail(string message)
    {
        if (failed || finished) return;
        failed = true;
        UnbindHealth();
        Debug.LogError("RoomManager: " + message, this);
        Events.Publish(new TowerGenerationFailedEvent(message, roomsEntered));
    }
}
