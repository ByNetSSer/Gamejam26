using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum EnemyCurse { None, Flupin, Dontsee, MaskCharger }

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
    [SerializeField, Min(1f)] float interactionRange = 4f;
    [SerializeField, Range(0f, 1f)] float legendaryChance = 0.02f;
    [SerializeField, Range(0f, 1f)] float rareChance = 0.23f;
    RoomManager rooms;
    IDisposable completed, started;
    GameObject stage;
    GameObject offersRoot;
    Camera view;
    RewardCatalog catalog;
    int purchases;
    bool curseSelection, rerolled;
    PlayerScore score;
    readonly HashSet<string> previousOffers = new HashSet<string>();
    readonly List<RewardPedestal> pedestals = new List<RewardPedestal>();
    readonly List<Material> ownedMaterials = new List<Material>();
    // Only catalog effects already supported by PlayerStats are offered.
    static readonly Reward[] common = {
        new Reward("Cordones del paje", "+3 % de velocidad de movimiento.", PlayerStat.MoveSpeed, 0.03f, StatModifierType.Percent),
        new Reward("Suelas gastadas", "+5 % de frenado.", PlayerStat.Braking, 0.05f, StatModifierType.Percent),
        new Reward("Reloj atrasado", "+5 % de duración del combo.", PlayerStat.ComboDuration, 0.05f, StatModifierType.Percent),
        new Reward("Moneda de la suerte", "+3 % de puntos.", PlayerStat.ScoreAmplification, 0.03f, StatModifierType.Percent),
        new Reward("Cupón del mercader", "+5 % de descuento (máximo 20 %).", PlayerStat.ShopDiscount, 0.05f, StatModifierType.Flat),
        new Reward("Impulso", "+8 % de aceleración.", PlayerStat.Acceleration, 0.08f, StatModifierType.Percent),
        new Reward("Resorte", "+6 % de salto.", PlayerStat.JumpSpeed, 0.06f, StatModifierType.Percent),
        new Reward("Guantes de piedra", "+10 % de daño por impacto.", PlayerStat.ImpactDamage, 0.1f, StatModifierType.Percent)
    };
    static readonly Reward[] rare = {
        new Reward("Botas del viajero", "+10 % de velocidad de movimiento.", PlayerStat.MoveSpeed, 0.1f, StatModifierType.Percent),
        new Reward("Anillo de maniobra", "+15 % de aceleración de giro.", PlayerStat.TurnAcceleration, 0.15f, StatModifierType.Percent),
        new Reward("Pluma del acróbata", "+15 % de control aéreo.", PlayerStat.AirControl, 0.15f, StatModifierType.Percent),
        new Reward("Reloj del bufón", "+20 % de duración del combo.", PlayerStat.ComboDuration, 0.2f, StatModifierType.Percent),
        new Reward("Suelas del guardia", "+15 % de frenado.", PlayerStat.Braking, 0.15f, StatModifierType.Percent),
        new Reward("Resorte encantado", "+15 % de velocidad de salto.", PlayerStat.JumpSpeed, 0.15f, StatModifierType.Percent),
        new Reward("Monóculo de la fortuna", "+15 % de puntos.", PlayerStat.ScoreAmplification, 0.15f, StatModifierType.Percent)
    };
    static readonly Reward[] legendary = {
        new Reward("Botas del vendaval", "+25 % de velocidad de movimiento.", PlayerStat.MoveSpeed, 0.25f, StatModifierType.Percent),
        new Reward("Alas del halcón", "+25 % de control aéreo.", PlayerStat.AirControl, 0.25f, StatModifierType.Percent),
        new Reward("Ancla del explorador", "+30 % de frenado.", PlayerStat.Braking, 0.3f, StatModifierType.Percent),
        new Reward("Corona del espectáculo", "+25 % de crecimiento del multiplicador de combo.", PlayerStat.ComboMultiplierGrowth, 0.25f, StatModifierType.Percent),
        new Reward("Monóculo dorado", "+25 % de puntos.", PlayerStat.ScoreAmplification, 0.25f, StatModifierType.Percent),
        new Reward("Maldición de la torre", "+30 % al aumento de velocidad por sala.", PlayerStat.SpeedPerRoom, 0.3f, StatModifierType.Percent)
    };
    // Provisional drawbacks until the curse catalog has executable effects.
    static readonly Reward[] curses = {
        new Reward("Paso inestable", "-15 % de frenado.", PlayerStat.Braking, -0.15f, StatModifierType.Percent),
        new Reward("Carga pesada", "-15 % de aceleración.", PlayerStat.Acceleration, -0.15f, StatModifierType.Percent),
        new Reward("Mirada inquieta", "-15 % de control de cámara.", PlayerStat.CameraControl, -0.15f, StatModifierType.Percent),
        new Reward("Salto encadenado", "-10 % de fuerza de salto.", PlayerStat.JumpSpeed, -0.1f, StatModifierType.Percent)
    };
    void Awake() { rooms = GetComponent<RoomManager>(); catalog = Resources.Load<RewardCatalog>("RewardCatalog"); }
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        completed = rooms.Events.Subscribe<TowerCompletedEvent>(OnCompleted);
        started = rooms.Events.Subscribe<TowerStartedEvent>(OnStarted);
    }
    void OnDisable() { completed?.Dispose(); started?.Dispose(); completed = started = null; Clear(); }
    void OnStarted(TowerStartedEvent e)
    {
        Clear();
        if (!TowerSafetyRules.RequiresStartingCurse(rooms.TowerNumber) || rooms.CurrentRoom == null || rooms.Player == null) return;
        CreateStage("Maldición obligatoria");
        curseSelection = true;
        rooms.SetStartingExitLocked(true);
        offersRoot = new GameObject("Maldiciones"); offersRoot.transform.SetParent(stage.transform, false);
        var choices = new List<Reward>(curses);
        var enemyChoices = new List<Reward>();
        var roster = GetComponent<RoomEnemyRoster>();
        if (roster != null)
        {
            if (roster.CanChooseFlupin)
                enemyChoices.Add(new Reward(roster.FlupinSelected ? "Enjambre de Flupin" : "Invocar a Flupin", roster.FlupinSelected ? "Aparece un Flupin adicional en cada torre." : "Flupin aparecerá en las siguientes salas.", EnemyCurse.Flupin));
            if (roster.CanChooseMaskCharger)
                enemyChoices.Add(new Reward(roster.MaskChargerSelected ? "Doble máscara" : "Invocar a la máscara", roster.MaskChargerSelected ? "Aparece una máscara adicional en cada torre." : "La máscara aparecerá en las siguientes salas.", EnemyCurse.MaskCharger));
            if (roster.CanChooseDontsee)
                enemyChoices.Add(new Reward(roster.DontseeSelected ? "Mirada frecuente" : "Invocar a Dontsee", roster.DontseeSelected ? "+15 puntos de probabilidad y una sala menos de espera." : "Dontsee podrá aparecer en las siguientes salas.", EnemyCurse.Dontsee));
        }
        var offered = new List<Reward>();
        if (enemyChoices.Count > 0)
        {
            int pick = UnityEngine.Random.Range(0, enemyChoices.Count);
            offered.Add(enemyChoices[pick]);
            enemyChoices.RemoveAt(pick);
        }
        choices.AddRange(enemyChoices);
        while (offered.Count < 3)
        {
            int pick = UnityEngine.Random.Range(0, choices.Count);
            offered.Add(choices[pick]); choices.RemoveAt(pick);
        }
        for (int i = offered.Count - 1; i > 0; i--)
        { int pick = UnityEngine.Random.Range(0, i + 1); var swap = offered[i]; offered[i] = offered[pick]; offered[pick] = swap; }
        for (int i = 0; i < offered.Count; i++) AddPedestal(i, offered[i], 1, true);
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
        rerolled = false;
        BuildOffers();
        var button = Cube("Siguiente torre", stage.transform, new Vector3(0f, 0.45f, -3.5f), new Vector3(0.7f, 0.9f, 0.5f), new Color(0.25f, 0.42f, 0.22f));
        button.AddComponent<RewardPedestal>().InitializeButton(this);
        var reroll = Cube("Renovar tienda", stage.transform, new Vector3(2f, 0.45f, -3.5f), new Vector3(0.7f, 0.9f, 0.5f), new Color(0.35f, 0.25f, 0.55f));
        reroll.AddComponent<RewardPedestal>().InitializeReroll(this);
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
            int tier = roll < legendaryWeight ? 3 : roll < legendaryWeight + rareWeight ? 2 : 1;
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
        var point = pedestal.AddComponent<RewardPedestal>();
        float discount = Mathf.Clamp(rooms.Player.Stats.Evaluate(PlayerStat.ShopDiscount, 0f), 0f, 0.2f);
        int basePrice = tier == 3 ? legendaryPrice : tier == 2 ? rarePrice : commonPrice;
        int price = Mathf.Max(1, Mathf.CeilToInt(basePrice * (1f + purchases * Mathf.Max(0f, priceGrowthPerPurchase)) * (1f - discount)));
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
            }
        }
        else
        {
            Sprite sprite = definition != null && definition.Icon != null ? definition.Icon : commonSprite;
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
            point.SetBillboard(visual.transform);
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
        RewardPedestal focused = null;
        if (Cursor.lockState == CursorLockMode.Locked && Physics.Raycast(view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f)), out var hit, interactionRange, ~0, QueryTriggerInteraction.Ignore))
            focused = hit.collider.GetComponentInParent<RewardPedestal>();
        foreach (var pedestal in stage.GetComponentsInChildren<RewardPedestal>()) pedestal.SetFocused(pedestal == focused, view.transform, score != null ? score.Coins : 0);
        if (focused != null && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (focused.IsReroll)
            {
                if (!rerolled) { rerolled = true; focused.MarkPurchased(); BuildOffers(); }
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
                    var roster = GetComponent<RoomEnemyRoster>();
                    bool applied = roster != null && (focused.Enemy == EnemyCurse.Flupin ? roster.ChooseFlupinCurse() :
                        focused.Enemy == EnemyCurse.MaskCharger ? roster.ChooseMaskCurse() : roster.ChooseDontseeCurse());
                    if (!applied) { Debug.LogWarning("No se pudo aplicar la maldición de enemigo seleccionada.", this); return; }
                }
                else rooms.Player.Stats.AddModifier(new object(), focused.Stat, focused.Type, focused.Amount);
                rooms.SetStartingExitLocked(false);
                Clear();
            }
            else if (!focused.Purchased && score != null && score.TrySpend(focused.Price))
            {
                var source = new object();
                rooms.Player.Stats.AddModifier(source, focused.Stat, focused.Type, focused.Amount);
                focused.MarkPurchased();
                purchases++;
            }
        }
    }
    void OnGUI()
    {
        if (curseSelection && stage != null)
            GUI.Box(new Rect(Screen.width * 0.5f - 190f, 20f, 380f, 28f), "Elige una maldición para abrir la trampilla");
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
        if (stage != null) { stage.SetActive(false); Destroy(stage); stage = null; }
        foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        ownedMaterials.Clear(); pedestals.Clear(); view = null; score = null;
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
    public void MarkPurchased() => Purchased = true;
    public PlayerStat Stat { get; private set; }
    public float Amount { get; private set; }
    public StatModifierType Type { get; private set; }
    string title, description;
    GameObject panel;
    TextMesh label;
    Transform billboard;
    public void SetBillboard(Transform visual) { billboard = visual; }
    TowerRewardStation station;
    public void Initialize(TowerRewardStation owner, string name, string effect, PlayerStat stat, float amount, StatModifierType type, int tier, int price, bool curse = false, EnemyCurse enemy = EnemyCurse.None)
    { Enemy = enemy; IsCurse = curse; Price = price; station = owner; title = name + (curse ? "  [MALDICIÓN]" : tier == 3 ? "  [LEGENDARIO]" : tier == 2 ? "  [RARO]" : "  [COMÚN]"); description = effect; Stat = stat; Amount = amount; Type = type; CreatePanel(); }
    public void InitializeButton(TowerRewardStation owner) { station = owner; IsButton = true; title = "SIGUIENTE TORRE"; description = "Continúa el descenso."; CreatePanel(); }
    public void InitializeVictory(TowerRewardStation owner, int totalTowers) { station = owner; IsVictory = true; title = "VICTORIA"; description = $"Completaste {totalTowers} torres."; CreatePanel(); }
    public void InitializeReroll(TowerRewardStation owner) { station = owner; IsReroll = true; title = "RENOVAR TIENDA"; description = "Una vez gratis por torre."; CreatePanel(); }
    void CreatePanel()
    {
        panel = new GameObject("Información"); panel.transform.SetParent(transform, false);
        panel.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        // Compensate for the pedestal scale so text is independent of its dimensions.
        var scale = transform.localScale; panel.transform.localScale = new Vector3(1f / scale.x, 1f / scale.y, 1f / scale.z);
        label = panel.AddComponent<TextMesh>(); label.anchor = TextAnchor.MiddleCenter; label.alignment = TextAlignment.Center;
        label.characterSize = 0.04f; label.fontSize = 48; label.color = Color.white;
        var background = GameObject.CreatePrimitive(PrimitiveType.Quad);
        background.name = "Fondo"; background.transform.SetParent(panel.transform, false);
        background.transform.localPosition = new Vector3(0f, 0f, 0.02f);
        background.transform.localScale = new Vector3(3f, 1.1f, 1f);
        var collider = background.GetComponent<Collider>();
        if (collider != null) { collider.enabled = false; Destroy(collider); }
        station.SetColor(background.GetComponent<Renderer>(), new Color(0.06f, 0.05f, 0.08f));
        panel.SetActive(false);
    }
    public void SetFocused(bool focused, Transform cameraTransform, long coins)
    {
        if (billboard != null)
        {
            Vector3 direction = billboard.position - cameraTransform.position;
            if (direction.sqrMagnitude > 0.0001f) billboard.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
        if (panel == null) return;
        panel.SetActive(focused);
        if (!focused) return;
        panel.transform.rotation = Quaternion.LookRotation(panel.transform.position - cameraTransform.position, Vector3.up);
        label.text = title + "\n" + description + "\n" + (IsVictory ? (Purchased ? "Victoria conseguida" : "E - Ganar partida") : IsButton ? "E - Continuar" : IsReroll ? (Purchased ? "Ya utilizado" : "E - Renovar gratis") : IsCurse ? "E - Elegir maldición" : Purchased ? "Comprado" : $"{Price} monedas (tienes {coins})\n" + (coins >= Price ? "E - Comprar" : "Monedas insuficientes"));
    }
}
