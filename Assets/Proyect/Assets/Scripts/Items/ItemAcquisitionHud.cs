using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public enum ItemAcquisitionSource { Pickup, Curse, Shop, Tower }

// Presentation only: call after successfully granting an item or applying a curse.
public sealed class ItemAcquisitionHud : MonoBehaviour
{
    [SerializeField, Min(1f)] float displaySeconds = 3.6f;
    [SerializeField] Sprite glowSprite;
    struct Notice
    {
        public string Name, Description;
        public Sprite Icon;
        public ItemAcquisitionSource Source;
    }
    readonly Queue<Notice> pending = new Queue<Notice>();
    CanvasGroup group;
    RectTransform card, iconRoot;
    Image icon, glow;
    Text origin, title, description, fallback;
    GameObject canvasObject;
    Texture2D generatedTexture;
    Sprite generatedGlow;
    bool showing;
    float elapsed;

    public static string Describe(PlayerStat stat, StatModifierType type, float value)
    {
        string[] names = { "velocidad", "aceleración", "frenado", "control aéreo", "salto", "velocidad de pisotón",
            "daño de impacto", "control de cámara", "duración del combo", "crecimiento del combo", "puntos",
            "aceleración de giro", "gravedad", "velocidad máxima de caída", "velocidad por sala", "pisotón por sala",
            "margen para saltar", "anticipación del salto", "adherencia al suelo", "sensibilidad", "penalización de cámara",
            "control mínimo de cámara", "límite vertical de cámara", "descuento de tienda" };
        int index = (int)stat;
        string name = index >= 0 && index < names.Length ? names[index] : stat.ToString();
        float amount = type == StatModifierType.Flat ? value : value * 100f;
        return $"{(amount >= 0f ? "+" : "")}{amount:0.##}{(type == StatModifierType.Flat ? "" : " %")} de {name}.";
    }

    public static void Show(PlayerController player, string name, string effect, Sprite sprite, ItemAcquisitionSource source)
    {
        if (player == null) return;
        var hud = player.GetComponent<ItemAcquisitionHud>();
        if (hud == null) hud = player.gameObject.AddComponent<ItemAcquisitionHud>();
        hud.pending.Enqueue(new Notice { Name = name, Description = effect, Icon = sprite, Source = source });
    }
    void Update()
    {
        if (!showing && pending.Count > 0) Begin(pending.Dequeue());
        if (!showing) return;
        elapsed += Time.unscaledDeltaTime;
        float duration = Mathf.Max(1f, displaySeconds);
        group.alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.18f), Mathf.Clamp01((duration - elapsed) / 0.55f));
        float intro = Mathf.Clamp01(elapsed / 0.5f);
        float bounce = Mathf.Sin(intro * Mathf.PI);
        iconRoot.anchoredPosition = new Vector2(-186f, 8f + bounce * 22f);
        iconRoot.localScale = Vector3.one * (Mathf.Lerp(0.65f, 1f, Mathf.Clamp01(intro * 3f)) + bounce * 0.12f);
        if (elapsed >= duration) { showing = false; group.alpha = 0f; }
    }
    void Begin(Notice notice)
    {
        if (group == null) Build();
        Color color;
        switch (notice.Source)
        {
            case ItemAcquisitionSource.Curse: origin.text = "MALDICIÓN"; color = new Color(0.79f, 0.47f, 1f); break;
            case ItemAcquisitionSource.Shop: origin.text = "TIENDA"; color = new Color(1f, 0.79f, 0.32f); break;
            case ItemAcquisitionSource.Tower: origin.text = "LA TORRE"; color = new Color(1f, 0.36f, 0.38f); break;
            default: origin.text = "HALLAZGO"; color = new Color(0.34f, 0.91f, 0.8f); break;
        }
        origin.color = color;
        glow.color = new Color(color.r, color.g, color.b, 0.65f);
        title.text = notice.Name;
        description.text = notice.Description;
        icon.sprite = notice.Icon;
        icon.enabled = notice.Icon != null;
        fallback.text = notice.Source == ItemAcquisitionSource.Tower || notice.Source == ItemAcquisitionSource.Curse ? "!" : "+";
        fallback.enabled = notice.Icon == null;
        elapsed = 0f; showing = true; group.alpha = 0f;
    }
    void Build()
    {
        canvasObject = new GameObject("Adquisiciones", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 80;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
        group = canvasObject.GetComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false; group.alpha = 0f;
        card = Rect("Aviso", canvasObject.transform, new Vector2(520, 156), new Vector2(0, -125));
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 1f);
        var background = card.gameObject.AddComponent<Image>(); background.color = new Color(0.035f, 0.025f, 0.055f, 0.86f); background.raycastTarget = false;
        iconRoot = Rect("Salto", card, new Vector2(100, 100), new Vector2(-186, 8));
        glow = Picture("Resplandor", iconRoot, new Vector2(205, 205));
        glow.sprite = glowSprite != null ? glowSprite : CreateGlow();
        icon = Picture("Icono", iconRoot, new Vector2(88, 88)); icon.preserveAspect = true;
        fallback = Label("Símbolo provisional", iconRoot, Vector2.zero, new Vector2(88, 88), 60); fallback.alignment = TextAnchor.MiddleCenter;
        origin = Label("Origen", card, new Vector2(55, 52), new Vector2(360, 24), 18);
        title = Label("Nombre", card, new Vector2(55, 21), new Vector2(360, 36), 25);
        title.fontStyle = FontStyle.Bold;
        description = Label("Efecto", card, new Vector2(55, -30), new Vector2(360, 62), 20);
    }
    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }
    static Image Picture(string name, Transform parent, Vector2 size)
    {
        var image = Rect(name, parent, size, Vector2.zero).gameObject.AddComponent<Image>();
        image.raycastTarget = false; return image;
    }
    static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var text = Rect(name, parent, size, position).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize;
        text.color = Color.white; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        text.supportRichText = false; text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 14; text.resizeTextMaxSize = fontSize;
        return text;
    }
    Sprite CreateGlow()
    {
        const int size = 128;
        generatedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        generatedTexture.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float radius = new Vector2((x + 0.5f) / size * 2f - 1f, (y + 0.5f) / size * 2f - 1f).magnitude;
            float alpha = Mathf.Pow(Mathf.Clamp01(1f - radius), 2f);
            pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
        }
        generatedTexture.SetPixels(pixels); generatedTexture.Apply(false, true);
        generatedGlow = Sprite.Create(generatedTexture, new Rect(0, 0, size, size), Vector2.one * 0.5f);
        return generatedGlow;
    }
    void OnDisable() { pending.Clear(); showing = false; if (group != null) group.alpha = 0f; }
    void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
        if (generatedGlow != null) Destroy(generatedGlow);
        if (generatedTexture != null) Destroy(generatedTexture);
    }
}
