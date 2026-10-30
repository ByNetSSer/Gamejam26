using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Runtime uGUI display: no scene setup or UI Toolkit dependency.
[RequireComponent(typeof(PlayerScore))]
public sealed class ComboHud : MonoBehaviour
{
    [SerializeField, Min(1)] int visibleEntries = 6;
    [SerializeField] int[] tierThresholds = { 0, 10, 30, 70, 150, 300 };
    readonly string[] tierNames = { "D", "C", "B", "A", "S", "SS" };
    readonly Queue<string> entries = new Queue<string>();
    PlayerScore score;
    GameObject canvasObject;
    Text header, log, wallet;
    Image timer;
    float window;
    void Awake()
    {
        score = GetComponent<PlayerScore>();
        canvasObject = new GameObject("Combo HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
        panel.transform.SetParent(canvasObject.transform, false);
        var rect = panel.GetComponent<RectTransform>(); rect.anchorMin = rect.anchorMax = new Vector2(1, 0.5f); rect.pivot = new Vector2(1, 0.5f); rect.anchoredPosition = new Vector2(-35, 0); rect.sizeDelta = new Vector2(350, 300);
        panel.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.05f, 0.8f);
        header = MakeText(panel.transform, "Tier", new Vector2(12, -8), new Vector2(326, 45), 26);
        wallet = MakeText(panel.transform, "Monedas", new Vector2(12, -52), new Vector2(326, 30), 18);
        var bar = new GameObject("Tiempo", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(panel.transform, false);
        var br = bar.GetComponent<RectTransform>(); br.anchorMin = br.anchorMax = new Vector2(0, 1); br.pivot = new Vector2(0, 1); br.anchoredPosition = new Vector2(12, -87); br.sizeDelta = new Vector2(326, 7);
        timer = bar.GetComponent<Image>(); timer.color = new Color(1f, 0.7f, 0.2f); timer.type = Image.Type.Filled; timer.fillMethod = Image.FillMethod.Horizontal;
        log = MakeText(panel.transform, "Registro", new Vector2(12, -105), new Vector2(326, 180), 18);
        Refresh();
    }
    static Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0, 1); r.pivot = new Vector2(0, 1); r.anchoredPosition = position; r.sizeDelta = size;
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = Color.white; text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate; return text;
    }
    void OnEnable() { score.Awarded += OnAward; score.ComboPaid += OnPaid; score.CoinsChanged += OnCoins; }
    void OnDisable() { score.Awarded -= OnAward; score.ComboPaid -= OnPaid; score.CoinsChanged -= OnCoins; }
    void OnDestroy() { if (canvasObject != null) Destroy(canvasObject); }
    void OnAward(ScoreAward award)
    {
        if (award.ComboCount == 1) entries.Clear();
        entries.Enqueue($"{award.Reason}  +{award.Points}");
        while (entries.Count > Mathf.Max(1, visibleEntries)) entries.Dequeue();
        window = Mathf.Max(0.01f, score.ComboDuration); Refresh();
    }
    void OnPaid(long points, long coins) { entries.Clear(); entries.Enqueue($"Combo: {points}  →  +{coins} monedas"); Refresh(); }
    void OnCoins(long coins) => Refresh();
    void Update() { if (timer != null) timer.fillAmount = score.ComboCount > 0 ? Mathf.Clamp01(score.ComboRemaining / Mathf.Max(0.01f, window)) : 0f; }
    void Refresh()
    {
        if (header == null) return;
        int tier = 0;
        if (tierThresholds != null) for (int i = 0; i < tierThresholds.Length && i < tierNames.Length; i++) if (score.ComboPoints >= tierThresholds[i]) tier = i;
        header.text = $"{tierNames[tier]}   COMBO {score.ComboCount}   {score.ComboPoints} pts";
        wallet.text = $"MONEDAS  {score.Coins}";
        log.text = string.Join("\n", entries.ToArray());
    }
}
