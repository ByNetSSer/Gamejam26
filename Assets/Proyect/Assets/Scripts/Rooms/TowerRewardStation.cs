using System;

using System.Collections.Generic;

using AssetKits.ParticleImage;

using UnityEngine;

using UnityEngine.InputSystem;



public enum EnemyCurse { None, Flupin, Dontsee, MaskCharger, FlupinHaste, FlupinFury, MaskHaste, MaskFury, MaskStalking, DontseeFury, CorridorChaser, CorridorChaserSpeed, CorridorChaserHaste, Guilt }



// Temporary reward room; effects are explicit until catalog items have runtime implementations.

[RequireComponent(typeof(RoomManager))]

public sealed class TowerRewardStation : MonoBehaviour

{

    [Serializable] struct Reward

    {

        public string name, description;

        public PlayerStat stat;

        public float amount;

        public StatModifierType type;

        public EnemyCurse enemy;

        public Reward(string name, string description, PlayerStat stat, float amount, StatModifierType type)

        { this.name = name; this.description = description; this.stat = stat; this.amount = amount; this.type = type; enemy = EnemyCurse.None; }

        public Reward(string name, string description, EnemyCurse enemy)

        { this.name = name; this.description = description; stat = default; amount = 0f; type = StatModifierType.Flat; this.enemy = enemy; }

    }

    [SerializeField, Min(1)] int commonPrice = 3;

    [SerializeField, Min(1)] int rarePrice = 6;

    [SerializeField, Min(1)] int legendaryPrice = 12;

    [SerializeField, Min(0f)] float priceGrowthPerPurchase = 0.15f;

    [SerializeField] Sprite commonSprite;

    [SerializeField] GameObject rareModel;

    [SerializeField] GameObject rerollPrefab;

    [Tooltip("Particle Image EnergySpread para la tienda; sin asignar se usa la luz de respaldo.")]

    [SerializeField] GameObject offerEnergyPrefab;

    [Tooltip("Sprite monocromo para teñir los anillos del efecto de energía.")]

    [SerializeField] Sprite offerEnergyRingSprite;

    [Header("Presentación de ofertas")]

    [Tooltip("Escala del canvas mundial del aura; reduce o aumenta todo el efecto.")]

    [SerializeField, Min(0.0001f)] float offerEnergyWorldScale = 0.004f;

    [Tooltip("Tamaño inicial de los anillos EnergySpread (el prefab original usa 1500).")]

    [SerializeField, Min(1f)] float offerEnergyRingSize = 180f;

    [SerializeField, Min(0f)] float offerExtraHeight = 0.4f;

    [SerializeField, Min(0f)] float offerFloatHeight = 0.12f;

    [SerializeField, Min(0f)] float offerFloatSpeed = 1.8f;

    [Tooltip("Giro de los modelos 3D de la tienda, en grados por segundo.")]

    [SerializeField, Min(0f)] float offerModelRotationSpeed = 20f;

    [Tooltip("Separación adicional del aura detrás de los modelos 3D.")]

    [SerializeField, Min(0f)] float offerModelAuraPadding = 0.15f;

    [SerializeField, Min(0f)] float offerLightIntensity = 10f;

    [SerializeField, Min(0f)] float offerLightRange = 15f;

    [SerializeField, Min(1f)] float interactionRange = 4f;

    [Header("Depuración de tienda")]

    [Tooltip("Todas las ofertas de tienda serán legendarias, también al renovar. Desactivar para restaurar las probabilidades normales.")]

    [SerializeField] bool debugAllLegendary = true;

    [SerializeField, Range(0f, 1f)] float legendaryChance = 0.02f;

    [SerializeField, Range(0f, 1f)] float rareChance = 0.23f;

    RoomManager rooms;

    IDisposable completed, started;

    GameObject stage;

    GameObject offersRoot;

    Camera view;

    RewardCatalog catalog;

    int purchases, rerolls;

    [SerializeField, Min(1)] int initialRerollPrice = 3;

    [SerializeField, Min(1)] int rerollPriceIncrease = 1;

    [SerializeField, Min(0f)] float priceGrowthPerReroll = 0.1f;

    int RerollPrice => initialRerollPrice + rerolls * rerollPriceIncrease;

    bool curseSelection;

    PlayerScore score;

    RewardPedestal focused;

    Sprite auraSprite;

    Material auraMaterial;

    Transform roomReroll;

    RewardPedestal roomRerollInteraction;

    readonly List<Collider> roomRerollHitboxes = new List<Collider>();

    readonly HashSet<string> previousOffers = new HashSet<string>();

    readonly List<RewardPedestal> pedestals = new List<RewardPedestal>();

    readonly List<Material> ownedMaterials = new List<Material>();

    // Only catalog effects already supported by PlayerStats are offered.

    static readonly Reward[] common = {

        new Reward("Gota de rocío", "Recupera 1 % de vida máxima cada 10 segundos.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Zapatillas de mecha", "+35 % de velocidad al entrar durante 3,5 s; sin combo ni puntos, fatiga de 2,5 s.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Cordones del paje", "+3 % de velocidad de movimiento.", PlayerStat.MoveSpeed, 0.03f, StatModifierType.Percent),

        new Reward("Suelas gastadas", "+5 % de control lateral en suelo.", PlayerStat.GroundControl, 0.05f, StatModifierType.Percent),

        new Reward("Reloj atrasado", "+5 % de duración del combo.", PlayerStat.ComboDuration, 0.05f, StatModifierType.Percent),

        new Reward("Moneda de la suerte", "+3 % de puntos.", PlayerStat.ScoreAmplification, 0.03f, StatModifierType.Percent),

        new Reward("Cupón del mercader", "+5 % de descuento (máximo 20 %).", PlayerStat.ShopDiscount, 0.05f, StatModifierType.Flat),

        new Reward("Impulso", "+8 % de aceleración.", PlayerStat.Acceleration, 0.08f, StatModifierType.Percent),

        new Reward("Resorte", "+6 % de salto.", PlayerStat.JumpSpeed, 0.06f, StatModifierType.Percent),

        new Reward("Guantes de piedra", "+10 % de daño por impacto.", PlayerStat.ImpactDamage, 0.1f, StatModifierType.Percent)

    };

    static readonly Reward[] rare = {

        new Reward("Musgo vital", "Recupera 2 % de vida máxima cada 10 segundos.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Trofeo del corredor", "+10 % de puntos.", PlayerStat.ScoreAmplification, 0.1f, StatModifierType.Percent),

        new Reward("Botas del viajero", "+10 % de velocidad de movimiento.", PlayerStat.MoveSpeed, 0.1f, StatModifierType.Percent),

        new Reward("Anillo de maniobra", "+15 % de aceleración de giro.", PlayerStat.TurnAcceleration, 0.15f, StatModifierType.Percent),

        new Reward("Pluma del acróbata", "+15 % de control aéreo.", PlayerStat.AirControl, 0.15f, StatModifierType.Percent),

        new Reward("Reloj del bufón", "+20 % de duración del combo.", PlayerStat.ComboDuration, 0.2f, StatModifierType.Percent),

        new Reward("Suelas del guardia", "+15 % de control lateral en suelo.", PlayerStat.GroundControl, 0.15f, StatModifierType.Percent),

        new Reward("Resorte encantado", "+15 % de velocidad de salto.", PlayerStat.JumpSpeed, 0.15f, StatModifierType.Percent),

        new Reward("Monóculo de la fortuna", "+15 % de puntos.", PlayerStat.ScoreAmplification, 0.15f, StatModifierType.Percent)

    };

    static readonly Reward[] legendary = {

        new Reward("Botas del meteorito", "+30 % de velocidad y -20 % de control lateral en suelo.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Corazón de carrera", "+30 % de velocidad y +15 % de aceleración; pierdes 1 % de vida máxima cada 3 segundos.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Corazón de cazador", "Al repeler a Flupin recuperas 5 % de vida máxima, como máximo una vez cada 10 segundos.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Corona del temerario", "+30 % de puntos y -20 % de duración del combo.", PlayerStat.MoveSpeed, 0f, StatModifierType.Flat),

        new Reward("Botas del vendaval", "+25 % de velocidad de movimiento.", PlayerStat.MoveSpeed, 0.25f, StatModifierType.Percent),

        new Reward("Alas del halcón", "+25 % de control aéreo.", PlayerStat.AirControl, 0.25f, StatModifierType.Percent),

        new Reward("Ancla del explorador", "+30 % de control lateral en suelo.", PlayerStat.GroundControl, 0.3f, StatModifierType.Percent),

        new Reward("Corona del espectáculo", "+25 % de crecimiento del multiplicador de combo.", PlayerStat.ComboMultiplierGrowth, 0.25f, StatModifierType.Percent),

        new Reward("Monóculo dorado", "+25 % de puntos.", PlayerStat.ScoreAmplification, 0.25f, StatModifierType.Percent),

        new Reward("Maldición de la torre", "+30 % al aumento de velocidad por sala.", PlayerStat.SpeedPerRoom, 0.3f, StatModifierType.Percent)

    };

    // Provisional drawbacks until the curse catalog has executable effects.

    static readonly Reward[] curses = {

        new Reward("Paso inestable", "-15 % de control lateral en suelo.", PlayerStat.GroundControl, -0.15f, StatModifierType.Percent),

        new Reward("Carga pesada", "-15 % de aceleración.", PlayerStat.Acceleration, -0.15f, StatModifierType.Percent),

        new Reward("Mirada inquieta", "-15 % de control de cámara.", PlayerStat.CameraControl, -0.15f, StatModifierType.Percent),

        new Reward("Salto encadenado", "-10 % de fuerza de salto.", PlayerStat.JumpSpeed, -0.1f, StatModifierType.Percent)

    };

    void Awake()

    {

        rooms = GetComponent<RoomManager>();

        catalog = Resources.Load<RewardCatalog>("RewardCatalog");

        auraSprite = Resources.Load<Sprite>("Sprite/ShopAura");

        var shader = Resources.Load<Shader>("ShopAura");

        if (shader != null) auraMaterial = new Material(shader);

    }

    void OnDestroy() { if (auraMaterial != null) Destroy(auraMaterial); }

    void OnEnable()

    {

        if (rooms == null) rooms = GetComponent<RoomManager>();

        completed = rooms.Events.Subscribe<TowerCompletedEvent>(OnCompleted);

        started = rooms.Events.Subscribe<TowerStartedEvent>(OnStarted);

    }

    void OnDisable() { completed?.Dispose(); started?.Dispose(); completed = started = null; Clear(); }

    bool CopiesOnly => GetComponent<EnemyCopiesMode>() is EnemyCopiesMode mode && mode.isActiveAndEnabled && mode.enemyCopiesOnly;

    void OnStarted(TowerStartedEvent e)

    {

        Clear();

        if (!TowerSafetyRules.RequiresStartingCurse(rooms.TowerNumber) || rooms.CurrentRoom == null || rooms.Player == null) return;

        var enemyChoices = EnemyChoices();

        if (CopiesOnly && enemyChoices.Count == 0)

        {

            Debug.LogWarning("No quedan enemigos disponibles para las maldiciones; la salida permanece abierta.", this);

            return;

        }

        CreateStage("Maldición obligatoria");

        curseSelection = true;

        rooms.SetStartingExitLocked(true);

        offersRoot = new GameObject("Maldiciones"); offersRoot.transform.SetParent(stage.transform, false);

        var choices = CopiesOnly ? new List<Reward>() : new List<Reward>(curses);

        var offered = new List<Reward>();

        if (CopiesOnly)

        {

            var remaining = new List<Reward>(enemyChoices);

            while (offered.Count < 3)

            {

                if (remaining.Count == 0) remaining.AddRange(enemyChoices);

                int pick = UnityEngine.Random.Range(0, remaining.Count);

                offered.Add(remaining[pick]); remaining.RemoveAt(pick);

            }

        }

        else if (enemyChoices.Count > 0)

        {

            // Keep the new enemy discoverable: offer its unlock until chosen.

            // After that, all enemy upgrades return to the normal random pool.

            int pick = enemyChoices.FindIndex(reward => reward.enemy == EnemyCurse.CorridorChaser &&

                !GetComponent<RoomEnemyRoster>().CorridorChaserSelected);

            if (pick < 0) pick = UnityEngine.Random.Range(0, enemyChoices.Count);

            offered.Add(enemyChoices[pick]);

            enemyChoices.RemoveAt(pick);

        }

        if (!CopiesOnly) choices.AddRange(enemyChoices);

        while (offered.Count < 3)

        {

            int pick = UnityEngine.Random.Range(0, choices.Count);

            offered.Add(choices[pick]); choices.RemoveAt(pick);

        }

        for (int i = offered.Count - 1; i > 0; i--)

        { int pick = UnityEngine.Random.Range(0, i + 1); var swap = offered[i]; offered[i] = offered[pick]; offered[pick] = swap; }

        for (int i = 0; i < offered.Count; i++) AddPedestal(i, offered[i], 1, true);

    }

    List<Reward> EnemyChoices()

    {

        var enemyChoices = new List<Reward>();

        var roster = GetComponent<RoomEnemyRoster>();

        if (roster != null)

        {

            if (roster.CanChooseGuilt)

                enemyChoices.Add(new Reward("Invocar a Guilt", "Tu sombra repite tus pasos con 4 s de retraso. El contacto drena vida poco a poco.", EnemyCurse.Guilt));

            if (CopiesOnly)

            {

                if (roster.CanChooseFlupin)

                    enemyChoices.Add(new Reward(roster.FlupinSelected ? "+1 Flupin" : "Invocar a Flupin", "Añade un Flupin a las siguientes salas.", EnemyCurse.Flupin));

                if (roster.CanChooseMaskCharger)

                    enemyChoices.Add(new Reward(roster.MaskChargerSelected ? "+1 máscara" : "Invocar a la máscara", "Añade una máscara a las siguientes salas.", EnemyCurse.MaskCharger));

                if (!roster.DontseeSelected && roster.CanChooseDontsee)

                    enemyChoices.Add(new Reward("Invocar a Dontsee", "Dontsee podrá aparecer en las siguientes salas.", EnemyCurse.Dontsee));

                if (!roster.CorridorChaserSelected && roster.CanChooseCorridorChaser)

                    enemyChoices.Add(new Reward("Invocar al perseguidor", "El perseguidor podrá aparecer en las siguientes salas.", EnemyCurse.CorridorChaser));

                return enemyChoices;

            }

            if (roster.CanChooseCorridorChaser)

                enemyChoices.Add(new Reward(roster.CorridorChaserSelected ? "Rastro persistente" : "Invocar al perseguidor", roster.CorridorChaserSelected ? "El perseguidor aparece con más frecuencia." : "Un perseguidor veloz recorrerá los puntos de las salas; apártate hacia las paredes.", EnemyCurse.CorridorChaser));

            if (roster.CorridorChaserSelected)

            {

                enemyChoices.Add(new Reward("Perseguidor furioso", "El perseguidor recorre su ruta un 20 % más rápido.", EnemyCurse.CorridorChaserSpeed));

                enemyChoices.Add(new Reward("Perseguidor impaciente", "El aviso por esperar en la sala dura un 20 % menos.", EnemyCurse.CorridorChaserHaste));

            }

            if (roster.CanChooseFlupin)

                enemyChoices.Add(new Reward(roster.FlupinSelected ? "Enjambre de Flupin" : "Invocar a Flupin", roster.FlupinSelected ? "Aparece un Flupin adicional en cada torre." : "Flupin aparecerá en las siguientes salas.", EnemyCurse.Flupin));

            if (roster.CanChooseMaskCharger)

                enemyChoices.Add(new Reward(roster.MaskChargerSelected ? "Doble máscara" : "Invocar a la máscara", roster.MaskChargerSelected ? "Aparece una máscara adicional en cada torre." : "La máscara aparecerá en las siguientes salas.", EnemyCurse.MaskCharger));

            if (roster.FlupinSelected)

            {

                enemyChoices.Add(new Reward("Flupin impaciente", "Flupin espera un 20 % menos antes de atacar y recuperarse.", EnemyCurse.FlupinHaste));

                enemyChoices.Add(new Reward("Flupin furioso", "+20 % de agresividad de Flupin: se acerca más rápido y reduce sus esperas.", EnemyCurse.FlupinFury));

            }

            if (roster.MaskChargerSelected)

            {

                enemyChoices.Add(new Reward("Máscaras sin respiro", "Las máscaras reducen un 20 % su aviso, intervalo y recuperación (aviso mínimo: 0,3 s).", EnemyCurse.MaskHaste));

                enemyChoices.Add(new Reward("Máscaras furiosas", "+20 % de agresividad: embestidas más rápidas, mejor acecho y menos espera.", EnemyCurse.MaskFury));

                enemyChoices.Add(new Reward("Acecho implacable", "+30 % de velocidad de seguimiento, alcance de ataque, giro y capacidad de alcanzar al jugador.", EnemyCurse.MaskStalking));

            }

            if (roster.DontseeSelected)

                enemyChoices.Add(new Reward("Dontsee furioso", "+20 % de agresividad de Dontsee: acelera su acercamiento y agota antes el aviso.", EnemyCurse.DontseeFury));

            if (roster.CanChooseDontsee)

                enemyChoices.Add(new Reward(roster.DontseeSelected ? "Mirada frecuente" : "Invocar a Dontsee", roster.DontseeSelected ? "+15 puntos de probabilidad y una sala menos de espera." : "Dontsee podrá aparecer en las siguientes salas.", EnemyCurse.Dontsee));

        }

        return enemyChoices;

    }

    int lastAutomaticCurseTower;

    void ApplyTowerCurse()

    {

        int tower = rooms.TowerNumber;

        if (!TowerSafetyRules.ShouldApplyTowerCurse(tower, rooms.TotalTowers, lastAutomaticCurseTower) || rooms.Player == null) return;

        var choices = EnemyChoices();

        if (CopiesOnly && choices.Count == 0)

        {

            lastAutomaticCurseTower = tower;

            Debug.LogWarning("No quedan enemigos disponibles para la maldición automática de esta torre.", this);

            return;

        }

        Reward reward = choices.Count > 0 ? choices[UnityEngine.Random.Range(0, choices.Count)] : curses[UnityEngine.Random.Range(0, curses.Length)];

        if (reward.enemy != EnemyCurse.None)

        {

            if (!ApplyEnemyCurse(reward.enemy)) return;

        }

        else rooms.Player.Stats.AddModifier(new object(), reward.stat, reward.type, reward.amount);

        lastAutomaticCurseTower = tower;

        NotifyReward(reward.name, reward.description, ItemAcquisitionSource.Tower);

    }

    bool ApplyEnemyCurse(EnemyCurse curse)

    {

        var roster = GetComponent<RoomEnemyRoster>();

        if (roster == null) return false;

        if (CopiesOnly && curse != EnemyCurse.Flupin && curse != EnemyCurse.MaskCharger &&

            !(curse == EnemyCurse.Dontsee && !roster.DontseeSelected) &&

            !(curse == EnemyCurse.CorridorChaser && !roster.CorridorChaserSelected) &&

            !(curse == EnemyCurse.Guilt && roster.CanChooseGuilt)) return false;

        object source = new object();

        switch (curse)

        {

            case EnemyCurse.Guilt: return roster.ChooseGuiltCurse();

            case EnemyCurse.Flupin: return roster.ChooseFlupinCurse();

            case EnemyCurse.MaskCharger: return roster.ChooseMaskCurse();

            case EnemyCurse.Dontsee: return roster.ChooseDontseeCurse();

            case EnemyCurse.CorridorChaser: return roster.ChooseCorridorChaserCurse();

            case EnemyCurse.CorridorChaserSpeed:

                if (!roster.CorridorChaserSelected) return false;

                GetComponent<CorridorChaserEncounter>().IncreaseSpeed(); return true;

            case EnemyCurse.CorridorChaserHaste:

                if (!roster.CorridorChaserSelected) return false;

                GetComponent<CorridorChaserEncounter>().IncreaseImpatience(); return true;

            case EnemyCurse.FlupinHaste:

                if (!roster.FlupinSelected) return false;

                foreach (var stat in new[] { FlupinStat.InitialDelay, FlupinStat.TimeBetweenAttacks, FlupinStat.RecoveryDuration })

                    roster.AddFlupinModifier(source, stat, StatModifierType.PercentMult, -0.2f);

                return true;

            case EnemyCurse.FlupinFury:

                if (!roster.FlupinSelected) return false;

                roster.AddFlupinModifier(source, FlupinStat.Aggressiveness, StatModifierType.Percent, 0.2f);

                return true;

            case EnemyCurse.MaskHaste:

                if (!roster.MaskChargerSelected) return false;

                foreach (var stat in new[] { MaskChargerStat.TelegraphDuration, MaskChargerStat.AttackInterval, MaskChargerStat.RecoveryDuration })

                    roster.AddMaskChargerModifier(source, stat, StatModifierType.PercentMult, -0.2f);

                return true;

            case EnemyCurse.MaskFury:

                if (!roster.MaskChargerSelected) return false;

                roster.AddMaskChargerModifier(source, MaskChargerStat.Aggressiveness, StatModifierType.Percent, 0.2f);

                return true;

            case EnemyCurse.MaskStalking:

                if (!roster.MaskChargerSelected) return false;

                foreach (var stat in new[] { MaskChargerStat.FollowSpeed, MaskChargerStat.CatchUpBonus, MaskChargerStat.OrbitSpeed, MaskChargerStat.AttackRange })

                    roster.AddMaskChargerModifier(source, stat, StatModifierType.Percent, 0.3f);

                return true;

            case EnemyCurse.DontseeFury:

                if (!roster.DontseeSelected) return false;

                roster.AddDontseeModifier(source, DontseeStat.Aggressiveness, StatModifierType.Percent, 0.2f);

                return true;

            default: return false;

        }

    }

    void NotifyReward(string name, string description, ItemAcquisitionSource source)

    {

        var definition = catalog != null ? catalog.Find(name) : null;

        ItemAcquisitionHud.Show(rooms.Player, name, description, definition != null && definition.Icon != null ? definition.Icon : commonSprite, source);

    }

    void CreateStage(string name)

    {

        view = rooms.Player.GetComponentInChildren<Camera>();

        score = rooms.Player.GetComponent<PlayerScore>();

        stage = new GameObject(name);

        stage.transform.SetParent(rooms.CurrentRoom.transform, false);

        var safe = rooms.CurrentRoom.GetComponent<SafeRoomLayout>();

        if (safe != null) stage.transform.position = safe.FloorCenter;

    }

    void OnCompleted(TowerCompletedEvent e)

    {

        Clear();

        ApplyTowerCurse();

        if (rooms.CurrentRoom == null || rooms.Player == null) return;

        if (rooms.IsFinalTower)

        {

            var safe = rooms.CurrentRoom.GetComponent<SafeRoomLayout>();

            if (safe != null) safe.SetAltarsActive(false);

            CreateStage("Victoria");

            var victory = Cube("Ganar partida", stage.transform, new Vector3(0f, 0.45f, -3.5f), new Vector3(0.7f, 0.9f, 0.5f), new Color(0.75f, 0.55f, 0.15f));

            victory.AddComponent<RewardPedestal>().InitializeVictory(this, rooms.TotalTowers);

            return;

        }

        CreateStage("Tienda de torre");

        BuildOffers();

        GameObject reroll;

        var shopRoom = rooms.CurrentRoom.GetComponent<SafeRoomLayout>();

        roomReroll = shopRoom != null ? shopRoom.Reroll : null;

        if (roomReroll != null)

        {

            reroll = roomReroll.gameObject;

            reroll.SetActive(true);

            // The sphere and pedestal can have separate renderers: both should respond to aiming.

            foreach (var renderer in reroll.GetComponentsInChildren<MeshRenderer>())

            {

                if (renderer.GetComponent<Collider>() != null) continue;

                var filter = renderer.GetComponent<MeshFilter>();

                if (filter == null || filter.sharedMesh == null) continue;

                var box = renderer.gameObject.AddComponent<BoxCollider>();

                box.center = filter.sharedMesh.bounds.center;

                box.size = filter.sharedMesh.bounds.size;

                roomRerollHitboxes.Add(box);

            }

        }

        else if (rerollPrefab != null)

        {

            reroll = Instantiate(rerollPrefab, stage.transform);

            reroll.name = "Renovar tienda";

            reroll.transform.localPosition = new Vector3(2f, 0.45f, -3.5f);

            if (reroll.GetComponentInChildren<Collider>() == null) reroll.AddComponent<BoxCollider>();

        }

        else reroll = Cube("Renovar tienda", stage.transform, new Vector3(2f, 0.45f, -3.5f), new Vector3(0.7f, 0.9f, 0.5f), new Color(0.35f, 0.25f, 0.55f));

        var interaction = reroll.AddComponent<RewardPedestal>();

        interaction.InitializeReroll(this, RerollPrice);

        if (roomReroll != null) roomRerollInteraction = interaction;

    }

    void BuildOffers()

    {

        if (offersRoot != null) { offersRoot.SetActive(false); Destroy(offersRoot); }

        pedestals.Clear();

        offersRoot = new GameObject("Ofertas"); offersRoot.transform.SetParent(stage.transform, false);

        var offered = new HashSet<string>();

        for (int i = 0; i < 3; i++)

        {

            float roll = UnityEngine.Random.value;

            float legendaryWeight = Mathf.Clamp01(legendaryChance);

            float rareWeight = Mathf.Min(Mathf.Clamp01(rareChance), 1f - legendaryWeight);

            int tier = debugAllLegendary || roll < legendaryWeight ? 3 : roll < legendaryWeight + rareWeight ? 2 : 1;

            Reward[] pool = tier == 3 ? legendary : tier == 2 ? rare : common;

            var available = new List<Reward>();

            foreach (var reward in pool)

                if (!offered.Contains(reward.name) && !previousOffers.Contains(reward.name)) available.Add(reward);

            if (available.Count == 0)

                foreach (var reward in pool) if (!offered.Contains(reward.name)) available.Add(reward);

            var chosen = available[UnityEngine.Random.Range(0, available.Count)];

            offered.Add(chosen.name);

            AddPedestal(i, chosen, tier);

        }

        previousOffers.Clear();

        foreach (var name in offered) previousOffers.Add(name);

    }

    void AddPedestal(int index, Reward reward, int tier, bool curse = false)

    {

        var safe = rooms.CurrentRoom.GetComponent<SafeRoomLayout>();

        var altars = safe != null ? safe.GetAltars() : Array.Empty<Transform>();

        GameObject pedestal;

        Vector3 visualPosition;

        if (index < altars.Length)

        {

            var altar = altars[index];

            altar.gameObject.SetActive(true);

            var bounds = RoomAutoLayout.BoundsOf(altar);

            pedestal = new GameObject("Interacción altar " + (index + 1));

            pedestal.transform.SetParent(offersRoot.transform, false);

            pedestal.transform.SetPositionAndRotation(bounds.center, Quaternion.identity);

            var hitbox = pedestal.AddComponent<BoxCollider>();

            hitbox.size = new Vector3(Mathf.Max(0.5f, bounds.size.x), Mathf.Max(0.5f, bounds.size.y), Mathf.Max(0.5f, bounds.size.z));

            visualPosition = new Vector3(bounds.center.x, bounds.max.y + 0.5f, bounds.center.z);

        }

        else

        {

            pedestal = Cube("Pilar " + (index + 1), offersRoot.transform, new Vector3((index - 1) * 2.5f, 0.65f, 2.6f), new Vector3(0.9f, 1.3f, 0.9f), curse ? new Color(0.32f, 0.14f, 0.36f) : new Color(0.28f, 0.27f, 0.31f));

            visualPosition = pedestal.transform.position + Vector3.up * 1.15f;

        }

        if (!curse) visualPosition += Vector3.up * Mathf.Max(0f, offerExtraHeight);

        var point = pedestal.AddComponent<RewardPedestal>();

        float discount = Mathf.Clamp(rooms.Player.Stats.Evaluate(PlayerStat.ShopDiscount, 0f), 0f, 0.2f);

        int basePrice = tier == 3 ? legendaryPrice : tier == 2 ? rarePrice : commonPrice;

        int price = Mathf.Max(1, Mathf.CeilToInt(basePrice * MenuUIBase.Core.RunDifficulty.PriceMultiplier(rooms.TowerNumber) * (1f + purchases * Mathf.Max(0f, priceGrowthPerPurchase) + rerolls * Mathf.Max(0f, priceGrowthPerReroll)) * (1f - discount)));

        point.Initialize(this, reward.name, reward.description, reward.stat, reward.amount, reward.type, tier, price, curse, reward.enemy);

        pedestals.Add(point);

        ItemDefinition definition = catalog != null ? catalog.Find(reward.name) : null;

        if (tier == 3)

        {

            GameObject prefab = definition != null && definition.LegendaryModel != null ? definition.LegendaryModel : rareModel;

            if (prefab != null)

            {

                var model = Instantiate(prefab, offersRoot.transform);

                model.transform.position = visualPosition;

                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;

                point.SetRotation(model.transform, offerModelRotationSpeed);

                AttachVisual(point, model, tier, curse, index);

            }

            else

            {

                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);

                placeholder.name = "Modelo legendario pendiente";

                placeholder.transform.SetParent(offersRoot.transform, false);

                placeholder.transform.position = visualPosition;

                placeholder.transform.localScale = Vector3.one * 0.5f;

                Destroy(placeholder.GetComponent<Collider>());

                SetColor(placeholder.GetComponent<Renderer>(), new Color(1f, 0.7f, 0.2f));

                AttachVisual(point, placeholder, tier, curse, index);

            }

        }

        else

        {

            Sprite sprite = definition != null && definition.Icon != null ? definition.Icon : commonSprite;

            // Reuse existing icons for offers whose individual artwork is still pending.

            if (sprite == null)

            {

                string fallback = reward.name == "Reloj atrasado" ? "Impulso" :

                    reward.name == "Resorte" ? "Resorte encantado" :

                    reward.name == "Cupón del mercader" || reward.name == "Monóculo de la fortuna" ? "Moneda de la suerte" : null;

                if (fallback != null) sprite = Resources.Load<Sprite>("Sprite/Items/" + fallback);

            }

            var visual = new GameObject(sprite != null ? "Ítem 2D" : "Sprite pendiente");

            visual.transform.SetParent(offersRoot.transform, false);

            visual.transform.position = visualPosition;

            if (sprite != null)

            {

                var renderer = visual.AddComponent<SpriteRenderer>();

                renderer.sprite = sprite;

                float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);

                if (size > 0f) visual.transform.localScale = Vector3.one * (0.75f / size);

            }

            else

            {

                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Quad);

                placeholder.transform.SetParent(visual.transform, false);

                placeholder.transform.localScale = Vector3.one * 0.5f;

                Destroy(placeholder.GetComponent<Collider>());

                SetColor(placeholder.GetComponent<Renderer>(), new Color(0.8f, 0.8f, 0.6f));

            }

            if (!curse && auraSprite != null)

            {

                var aura = new GameObject("Aura de rareza");

                aura.transform.SetParent(visual.transform, false);

                aura.transform.localPosition = new Vector3(0f, 0f, 0.03f);

                var auraRenderer = aura.AddComponent<SpriteRenderer>();

                auraRenderer.sprite = auraSprite;

                if (auraMaterial != null) auraRenderer.sharedMaterial = auraMaterial;

                Color auraColor = tier == 1 ? Color.white : tier == 2 ? new Color(0.25f, 1f, 0.3f) : new Color(0.25f, 0.5f, 1f);

                var auraProperties = new MaterialPropertyBlock();

                auraProperties.SetColor("_Color", auraColor);

                auraRenderer.SetPropertyBlock(auraProperties);

                auraRenderer.sortingOrder = -1;

                float iconSize = sprite != null ? Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y) : 0.5f;

                float auraSize = Mathf.Max(auraSprite.bounds.size.x, auraSprite.bounds.size.y);

                if (auraSize > 0f) aura.transform.localScale = Vector3.one * (iconSize * 1.65f / auraSize);

            }

            point.SetBillboard(visual.transform);

            AttachVisual(point, visual, tier, curse, index);

        }

    }

    void AttachVisual(RewardPedestal point, GameObject visual, int tier, bool curse, int index)

    {

        visual.transform.SetParent(point.transform, true);

        point.SetVisual(visual);

        // Include the displayed mesh or sprite, not merely the altar below it.

        var renderers = visual.GetComponentsInChildren<Renderer>();

        Bounds bounds = new Bounds(visual.transform.position, Vector3.one * 0.5f);

        bool hasBounds = false;

        foreach (var renderer in renderers)

        {

            if (renderer.gameObject.name == "Aura de rareza") continue;

            if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }

            else bounds.Encapsulate(renderer.bounds);

        }

        Vector3 min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

        Vector3 max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);

        for (int x = 0; x < 2; x++) for (int y = 0; y < 2; y++) for (int z = 0; z < 2; z++)

        {

            var corner = new Vector3(x == 0 ? bounds.min.x : bounds.max.x, y == 0 ? bounds.min.y : bounds.max.y, z == 0 ? bounds.min.z : bounds.max.z);

            var local = visual.transform.InverseTransformPoint(corner);

            min = Vector3.Min(min, local); max = Vector3.Max(max, local);

        }

        var hitbox = visual.AddComponent<BoxCollider>();

        hitbox.center = (min + max) * 0.5f;

        var size = max - min;

        hitbox.size = new Vector3(Mathf.Max(0.1f, size.x), Mathf.Max(0.1f, size.y), Mathf.Max(0.25f, size.z));

        if (!curse)

        {

            point.SetFloating(visual.transform, offerFloatHeight, offerFloatSpeed, index);

            Color color = tier == 1 ? Color.white : tier == 2 ? new Color(0.25f, 1f, 0.3f) : tier == 3 ? new Color(1f, 0.72f, 0.22f) : new Color(0.25f, 0.5f, 1f);

            if (offerEnergyPrefab != null)

            {

                // Particle Image is UI: use a world-space canvas that follows the offer.

                var energyCanvas = new GameObject("Aura energética", typeof(RectTransform), typeof(Canvas));

                energyCanvas.transform.SetParent(visual.transform, false);

                var rect = (RectTransform)energyCanvas.transform;

                rect.sizeDelta = new Vector2(200f, 200f);

                rect.localPosition = new Vector3(0f, 0f, 0.08f);

                Vector3 scale = visual.transform.lossyScale;

                float worldScale = Mathf.Max(0.0001f, offerEnergyWorldScale);

                rect.localScale = new Vector3(worldScale / Mathf.Max(0.001f, Mathf.Abs(scale.x)),

                    worldScale / Mathf.Max(0.001f, Mathf.Abs(scale.y)), worldScale / Mathf.Max(0.001f, Mathf.Abs(scale.z)));

                var canvas = energyCanvas.GetComponent<Canvas>();

                canvas.renderMode = RenderMode.WorldSpace;

                canvas.worldCamera = view;

                var effect = Instantiate(offerEnergyPrefab, energyCanvas.transform, false);

                effect.name = "EnergySpread";

                var mainParticle = effect.GetComponent<ParticleImage>();

                if (mainParticle != null) mainParticle.Stop(true);

                foreach (var particle in effect.GetComponentsInChildren<ParticleImage>(true))

                {

                    // Particle Image writes each particle's startColor into its mesh;

                    // Graphic.color alone does not tint that mesh in this package version.

                    Color tint = color;

                    tint.a = particle.startColor.Evaluate(0f, 0.5f).a;

                    particle.startColor = new ParticleSystem.MinMaxGradient(tint);

                    particle.color = Color.white;

                    particle.raycastTarget = false;

                    if (particle == mainParticle)

                    {

                        // The root also has a 1500 x 300 beam in the demo prefab.

                        var particleSize = particle.startSize;

                        particleSize.xCurve = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, offerEnergyRingSize));

                        particleSize.yCurve = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, offerEnergyRingSize * 0.45f));

                        particle.startSize = particleSize;

                    }

                    else if (string.Equals(particle.gameObject.name, "Rings", StringComparison.OrdinalIgnoreCase))

                    {

                        particle.startSize = new SeparatedMinMaxCurve(Mathf.Max(1f, offerEnergyRingSize));

                        if (offerEnergyRingSprite != null) particle.sprite = offerEnergyRingSprite;

                    }

                    else if (string.Equals(particle.gameObject.name, "Glow", StringComparison.OrdinalIgnoreCase))

                        particle.startSize = new SeparatedMinMaxCurve(Mathf.Max(1f, offerEnergyRingSize * 0.8f));

                }

                if (mainParticle != null) mainParticle.Play();

                if (tier == 3)

                    point.SetEnergyBillboard(rect, visual.transform.InverseTransformPoint(bounds.center), bounds.extents.magnitude + Mathf.Max(0f, offerModelAuraPadding));

            }

            else

            {

                var lightObject = new GameObject("Luz de rareza (respaldo)");

                lightObject.transform.SetParent(visual.transform, false);

                var light = lightObject.AddComponent<Light>();

                light.type = LightType.Point; light.color = color;

                light.intensity = Mathf.Max(0f, offerLightIntensity);

                light.range = Mathf.Max(0f, offerLightRange);

            }

        }

    }

    GameObject Cube(string name, Transform parent, Vector3 pos, Vector3 scale, Color color)

    {

        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;

        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale;

        SetColor(go.GetComponent<Renderer>(), color); return go;

    }

    public void SetColor(Renderer renderer, Color color)

    {

        var shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null) return;

        var material = new Material(shader); material.color = color; ownedMaterials.Add(material); renderer.sharedMaterial = material;

    }

    void Update()

    {

        if (stage == null || view == null || rooms.Player == null || !rooms.Player.enabled) return;
        if (rooms.Player.TutorialBlocksActions) { focused = null; return; }

        focused = null;

        if (Cursor.lockState == CursorLockMode.Locked && Physics.Raycast(view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var hit, interactionRange, ~0, QueryTriggerInteraction.Ignore))

            focused = hit.collider.GetComponentInParent<RewardPedestal>();

        foreach (var pedestal in stage.GetComponentsInChildren<RewardPedestal>()) pedestal.SetFocused(pedestal == focused, view.transform, score != null ? score.Coins : 0);

        if (!curseSelection && offersRoot != null && !rooms.HasLinkedNextTower && Keyboard.current != null && Keyboard.current.nKey.wasPressedThisFrame)

        { rooms.StartNextTower(); return; }

        if (focused != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)

        {

            if (focused.IsReroll)

            {

                if (score != null && score.TrySpend(RerollPrice)) { rerolls++; focused.SetPrice(RerollPrice); BuildOffers(); focused = null; }

            }

            else if (focused.IsVictory)

            {

                if (!focused.Purchased && rooms.DeclareRunVictory()) focused.MarkPurchased();

            }

            else if (focused.IsButton) rooms.StartNextTower();

            else if (focused.IsCurse && curseSelection)

            {

                if (focused.Enemy != EnemyCurse.None)

                {

                    bool applied = ApplyEnemyCurse(focused.Enemy);

                    if (!applied) { Debug.LogWarning("No se pudo aplicar la maldición de enemigo seleccionada.", this); return; }

                }

                else rooms.Player.Stats.AddModifier(new object(), focused.Stat, focused.Type, focused.Amount);

                NotifyReward(focused.RewardName, focused.Description, ItemAcquisitionSource.Curse);

                rooms.SetStartingExitLocked(false);

                Clear();

            }

            else if (!focused.Purchased && score != null && score.TrySpend(focused.Price))

            {

                var source = new object();

                if (focused.RewardName == "Gota de rocío" || focused.RewardName == "Musgo vital" || focused.RewardName == "Zapatillas de mecha" || focused.RewardName == "Botas del meteorito" || focused.RewardName == "Corazón de carrera" || focused.RewardName == "Corazón de cazador" || focused.RewardName == "Corona del temerario")

                {

                    var effects = rooms.Player.GetComponent<RewardItemEffects>();

                    if (effects == null) effects = rooms.Player.gameObject.AddComponent<RewardItemEffects>();

                    effects.Acquire(focused.RewardName, rooms);

                }

                else rooms.Player.Stats.AddModifier(source, focused.Stat, focused.Type, focused.Amount);

                focused.MarkPurchased();

                NotifyReward(focused.RewardName, focused.Description, ItemAcquisitionSource.Shop);

                purchases++;

            }

        }

    }

    void OnGUI()

    {

        if (stage == null) return;

        if (curseSelection)

            GUI.Box(new Rect(Screen.width * 0.5f - 190f, 20f, 380f, 28f), "Elige una maldición para abrir la trampilla");

        else if (offersRoot != null)

            GUI.Box(new Rect(Screen.width * 0.5f - 240f, 20f, 480f, 28f), rooms.HasLinkedNextTower ? "Rompe la trampilla para bajar a la siguiente torre" : "N - Continuar a la siguiente torre");

        if (focused != null && !focused.Purchased)

        {

            var style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true, fontSize = Mathf.Clamp(Screen.height / 42, 16, 28) };

            float width = Mathf.Min(Screen.width * 0.8f, 620f);

            GUI.Box(new Rect((Screen.width - width) * 0.5f, Screen.height * 0.53f, width, Screen.height * 0.17f), focused.DisplayText(score != null ? score.Coins : 0), style);

        }

    }

    void Clear()

    {

        if (curseSelection && rooms != null)

        {

            rooms.SetStartingExitLocked(false);

            if (rooms.CurrentRoom != null)

            {

                var safe = rooms.CurrentRoom.GetComponent<SafeRoomLayout>();

                if (safe != null) safe.SetAltarsActive(false);

            }

        }

        curseSelection = false; offersRoot = null;

        if (roomRerollInteraction != null) { Destroy(roomRerollInteraction); roomRerollInteraction = null; }

        foreach (var hitbox in roomRerollHitboxes) if (hitbox != null) Destroy(hitbox);

        roomRerollHitboxes.Clear();

        if (roomReroll != null) { roomReroll.gameObject.SetActive(false); roomReroll = null; }

        if (stage != null) { stage.SetActive(false); Destroy(stage); stage = null; }

        foreach (var material in ownedMaterials) if (material != null) Destroy(material);

        ownedMaterials.Clear(); pedestals.Clear(); focused = null; view = null; score = null;

    }

}



public sealed class RewardPedestal : MonoBehaviour

{

    public bool IsButton { get; private set; }

    public bool IsVictory { get; private set; }

    public bool IsReroll { get; private set; }

    public bool IsCurse { get; private set; }

    public EnemyCurse Enemy { get; private set; }

    public bool Purchased { get; private set; }

    public int Price { get; private set; }

    GameObject itemVisual;

    public void SetVisual(GameObject visual) => itemVisual = visual;

    public void MarkPurchased() { Purchased = true; if (itemVisual != null) itemVisual.SetActive(false); }

    public void SetPrice(int price) => Price = price;

    public PlayerStat Stat { get; private set; }

    public float Amount { get; private set; }

    public StatModifierType Type { get; private set; }

    string title, description;

    public string RewardName { get; private set; }

    public string Description => description;

    Transform billboard, floating, energyBillboard, rotating;

    Vector3 energyCenter;

    float energyDistance, rotationSpeed;

    public void SetEnergyBillboard(Transform visual, Vector3 localCenter, float distance)

    { energyBillboard = visual; energyCenter = localCenter; energyDistance = distance; }

    public void SetRotation(Transform visual, float degreesPerSecond)

    { rotating = visual; rotationSpeed = Mathf.Max(0f, degreesPerSecond); }

    Vector3 floatingBase;

    float floatHeight, floatSpeed, floatPhase;

    public void SetFloating(Transform visual, float height, float speed, int index)

    {

        floating = visual;

        floatingBase = visual.localPosition;

        floatHeight = Mathf.Max(0f, height);

        floatSpeed = Mathf.Max(0f, speed);

        floatPhase = index * 1.3f;

    }

    public void SetBillboard(Transform visual) { billboard = visual; }

    TowerRewardStation station;

    public void Initialize(TowerRewardStation owner, string name, string effect, PlayerStat stat, float amount, StatModifierType type, int tier, int price, bool curse = false, EnemyCurse enemy = EnemyCurse.None)

    { RewardName = name; Enemy = enemy; IsCurse = curse; Price = price; station = owner; title = name; description = effect; Stat = stat; Amount = amount; Type = type; }

    public void InitializeButton(TowerRewardStation owner) { station = owner; IsButton = true; title = "SIGUIENTE TORRE"; description = "Continúa el descenso."; }

    public void InitializeVictory(TowerRewardStation owner, int totalTowers) { station = owner; IsVictory = true; title = "VICTORIA"; description = $"Completaste {totalTowers} torres."; }

    public void InitializeReroll(TowerRewardStation owner, int price) { station = owner; IsReroll = true; Price = price; title = "RENOVAR TIENDA"; description = "Cambia las ofertas y sube sus precios."; }

    public string DisplayText(long coins)

    {

        string action = IsVictory ? "E - Ganar partida" : IsButton ? "E - Continuar" :

            IsReroll ? $"{Price} monedas\n" + (coins >= Price ? "E - Renovar" : "Monedas insuficientes") :

            IsCurse ? "E - Elegir maldición" : $"{Price} monedas\n" + (coins >= Price ? "E - Comprar" : "Monedas insuficientes");

        return title + "\n" + description + "\n" + action;

    }

    public void SetFocused(bool focused, Transform cameraTransform, long coins)

    {

        if (floating != null && floating.gameObject.activeSelf)

        {

            Vector3 worldOffset = Vector3.up * (Mathf.Sin(Time.time * floatSpeed + floatPhase) * floatHeight);

            floating.localPosition = floatingBase + (floating.parent != null ? floating.parent.InverseTransformVector(worldOffset) : worldOffset);

        }

        if (rotating != null && rotating.gameObject.activeSelf)

            rotating.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);

        if (energyBillboard != null && floating != null && cameraTransform != null)

        {

            Vector3 center = floating.TransformPoint(energyCenter);

            Vector3 energyDirection = center - cameraTransform.position;

            if (energyDirection.sqrMagnitude > 0.0001f)

            {

                energyDirection.Normalize();

                energyBillboard.position = center + energyDirection * energyDistance;

                energyBillboard.rotation = Quaternion.LookRotation(energyDirection, Vector3.up);

            }

        }

        if (billboard != null)

        {

            Vector3 direction = billboard.position - cameraTransform.position;

            if (direction.sqrMagnitude > 0.0001f) billboard.rotation = Quaternion.LookRotation(direction, Vector3.up);

        }

    }

}

