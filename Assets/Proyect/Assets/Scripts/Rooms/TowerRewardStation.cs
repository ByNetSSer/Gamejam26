using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

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
        public Reward(string name, string description, PlayerStat stat, float amount, StatModifierType type)
        { this.name = name; this.description = description; this.stat = stat; this.amount = amount; this.type = type; }
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
    IDisposable completed;
    GameObject stage;
    Camera view;
    RewardCatalog catalog;
    int purchases;
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
    void Awake() { rooms = GetComponent<RoomManager>(); catalog = Resources.Load<RewardCatalog>("RewardCatalog"); }
    void OnEnable() { if (rooms == null) rooms = GetComponent<RoomManager>(); completed = rooms.Events.Subscribe<TowerCompletedEvent>(OnCompleted); }
    void OnDisable() { completed?.Dispose(); completed = null; Clear(); }
    void OnCompleted(TowerCompletedEvent e)
    {
        Clear();
        if (rooms.CurrentRoom == null || rooms.Player == null) return;
        view = rooms.Player.GetComponentInChildren<Camera>();
        score = rooms.Player.GetComponent<PlayerScore>();
        stage = new GameObject("Recompensas de torre");
        stage.transform.SetParent(rooms.CurrentRoom.transform, false);
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
        var button = Cube("Siguiente torre", stage.transform, new Vector3(0f, 0.45f, -3.5f), new Vector3(0.7f, 0.9f, 0.5f), new Color(0.25f, 0.42f, 0.22f));
        button.AddComponent<RewardPedestal>().InitializeButton(this);
    }
    void AddPedestal(int index, Reward reward, int tier)
    {
        var pedestal = Cube("Pilar " + (index + 1), stage.transform, new Vector3((index - 1) * 2.5f, 0.65f, 2.6f), new Vector3(0.9f, 1.3f, 0.9f), new Color(0.28f, 0.27f, 0.31f));
        var point = pedestal.AddComponent<RewardPedestal>();
        float discount = Mathf.Clamp(rooms.Player.Stats.Evaluate(PlayerStat.ShopDiscount, 0f), 0f, 0.2f);
        int basePrice = tier == 3 ? legendaryPrice : tier == 2 ? rarePrice : commonPrice;
        int price = Mathf.Max(1, Mathf.CeilToInt(basePrice * (1f + purchases * Mathf.Max(0f, priceGrowthPerPurchase)) * (1f - discount)));
        point.Initialize(this, reward.name, reward.description, reward.stat, reward.amount, reward.type, tier, price);
        pedestals.Add(point);
        ItemDefinition definition = catalog != null ? catalog.Find(reward.name) : null;
        if (tier == 3)
        {
            GameObject prefab = definition != null && definition.LegendaryModel != null ? definition.LegendaryModel : rareModel;
            if (prefab != null)
            {
                var model = Instantiate(prefab, stage.transform);
                model.transform.position = pedestal.transform.position + Vector3.up * 1.15f;
                foreach (var collider in model.GetComponentsInChildren<Collider>()) collider.enabled = false;
            }
            else
            {
                var placeholder = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                placeholder.name = "Modelo legendario pendiente";
                placeholder.transform.SetParent(stage.transform, false);
                placeholder.transform.position = pedestal.transform.position + Vector3.up * 1.15f;
                placeholder.transform.localScale = Vector3.one * 0.5f;
                Destroy(placeholder.GetComponent<Collider>());
                SetColor(placeholder.GetComponent<Renderer>(), new Color(1f, 0.7f, 0.2f));
            }
        }
        else
        {
            Sprite sprite = definition != null && definition.Icon != null ? definition.Icon : commonSprite;
            var visual = new GameObject(sprite != null ? "Ítem 2D" : "Sprite pendiente");
            visual.transform.SetParent(stage.transform, false);
            visual.transform.position = pedestal.transform.position + Vector3.up * 1.15f;
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
            if (focused.IsButton) rooms.StartNextTower();
            else if (!focused.Purchased && score != null && score.TrySpend(focused.Price))
            {
                var source = new object();
                rooms.Player.Stats.AddModifier(source, focused.Stat, focused.Type, focused.Amount);
                focused.MarkPurchased();
                purchases++;
            }
        }
    }
    void Clear()
    {
        if (stage != null) { stage.SetActive(false); Destroy(stage); stage = null; }
        foreach (var material in ownedMaterials) if (material != null) Destroy(material);
        ownedMaterials.Clear(); pedestals.Clear(); view = null; score = null;
    }
}

public sealed class RewardPedestal : MonoBehaviour
{
    public bool IsButton { get; private set; }
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
    public void Initialize(TowerRewardStation owner, string name, string effect, PlayerStat stat, float amount, StatModifierType type, int tier, int price)
    { Price = price; station = owner; title = name + (tier == 3 ? "  [LEGENDARIO]" : tier == 2 ? "  [RARO]" : "  [COMÚN]"); description = effect; Stat = stat; Amount = amount; Type = type; CreatePanel(); }
    public void InitializeButton(TowerRewardStation owner) { station = owner; IsButton = true; title = "SIGUIENTE TORRE"; description = "Continúa el descenso."; CreatePanel(); }
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
        label.text = title + "\n" + description + "\n" + (IsButton ? "E - Continuar" : Purchased ? "Comprado" : $"{Price} monedas (tienes {coins})\n" + (coins >= Price ? "E - Comprar" : "Monedas insuficientes"));
    }
}
