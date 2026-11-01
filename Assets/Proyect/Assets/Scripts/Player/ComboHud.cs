using System.Collections.Generic;
using AssetKits.ParticleImage;
using MoreMountains.Feedbacks;
using MenuUIBase.Core;
using UnityEngine;
using UnityEngine.UI;

// The main scene supplies the visuals; a small fallback keeps test scenes usable.
[RequireComponent(typeof(PlayerScore))]
public sealed class ComboHud : MonoBehaviour
{
    [Header("UI editable")]
    [SerializeField] RectTransform rankVisual;
    [SerializeField] Text rankLabel, rankTitle, statsLabel, log, timerLabel;
    [SerializeField] Image timer, accent, background, maskingColor;
    [SerializeField] CanvasGroup group;
    [SerializeField] ParticleImage rankParticles, maskingParticles;
    [SerializeField] float[] maskingRates = { 8f, 13f, 20f, 30f, 45f, 65f };
    [Header("FEEL opcional")]
    [SerializeField] MMF_Player actionFeedback, rankFeedback, payoutFeedback;
    [Header("Rangos")]
    [SerializeField, Min(1)] int visibleEntries = 6;
    [Tooltip("Puntos de una sola cadena necesarios para D, C, B, A, S y SS.")]
    [SerializeField] int[] easyThresholds = { 0, 120, 350, 800, 2000, 4000 };
    [SerializeField] int[] normalThresholds = { 0, 180, 500, 1300, 3000, 6500 };
    [SerializeField] int[] impossibleThresholds = { 0, 250, 800, 2000, 5000, 10000 };
    [SerializeField] string[] tierNames = { "D", "C", "B", "A", "S", "SS" };
    [SerializeField] string[] tierTitles = { "SOMBRAS INQUIETAS", "TE ESTÁN MIRANDO", "SIN DESCANSO", "FURIA DESATADA", "NO TE DETENDRÁN", "FUERA DE CONTROL" };
    [SerializeField] Color[] tierColors = { new Color(0.72f, 0.76f, 0.72f), new Color(0.53f, 0.87f, 0.72f), new Color(0.35f, 0.85f, 0.95f), new Color(0.78f, 0.54f, 1f), new Color(1f, 0.72f, 0.28f), new Color(1f, 0.25f, 0.18f) };
    [Header("Movimiento")]
    [SerializeField, Min(0f)] float rankPunch = 0.35f;
    [SerializeField, Min(0.01f)] float punchDuration = 0.35f;
    [SerializeField, Range(0f, 1f)] float urgentFraction = 0.25f;
    [SerializeField, Min(0f)] float idleFadeDelay = 2.5f;
    [SerializeField] RectTransform shakeTarget;
    [SerializeField, Min(0f)] float shakePerRank = 0.45f;
    [SerializeField, Min(0f)] float shakeFrequency = 19f;
    readonly Queue<KeyValuePair<string, int>> entries = new Queue<KeyValuePair<string, int>>();
    bool displayedEnglish;
    PlayerScore score;
    GameObject fallbackCanvas;
    Vector3 rankBaseScale;
    Vector2 shakeBasePosition;
    Color timerBaseColor;
    CharacterCounterFeedback headerFeedback, rankTextFeedback, titleTextFeedback;
    float window, punch, showUntil;
    int displayedTier;
    public RectTransform PayoutOrigin => rankVisual != null ? rankVisual : statsLabel != null ? statsLabel.rectTransform : null;

    void Awake()
    {
        score = GetComponent<PlayerScore>();
        var view = FindFirstObjectByType<ComboHudView>();
        if (view != null)
        {
            rankVisual = view.rankVisual; shakeTarget = view.shakeTarget;
            rankLabel = view.rankLabel; rankTitle = view.rankTitle; statsLabel = view.statsLabel;
            log = view.log; timerLabel = view.timerLabel; timer = view.timer; accent = view.accent;
            background = view.background; maskingColor = view.maskingColor;
            group = view.group; rankParticles = view.rankParticles;
            maskingParticles = view.maskingParticles;
            if (view.maskingRates != null && view.maskingRates.Length > 0) maskingRates = view.maskingRates;
        }
        if (rankLabel == null || statsLabel == null || log == null || timer == null) BuildFallback();
        if (maskingParticles == null && maskingColor != null) maskingParticles = maskingColor.GetComponentInParent<ParticleImage>();
        if (rankVisual != null) rankBaseScale = rankVisual.localScale;
        if (shakeTarget != null) shakeBasePosition = shakeTarget.anchoredPosition;
        if (timer != null) timerBaseColor = timer.color;
        if (statsLabel != null) headerFeedback = statsLabel.GetComponent<CharacterCounterFeedback>();
        if (rankLabel != null) rankTextFeedback = rankLabel.GetComponent<CharacterCounterFeedback>();
        if (rankTitle != null) titleTextFeedback = rankTitle.GetComponent<CharacterCounterFeedback>();
        if (group != null) group.alpha = 0f;
        Refresh(false);
    }
    void OnEnable() { if (score != null) { score.Awarded += OnAward; score.ComboPaid += OnPaid; score.CoinsChanged += OnCoins; } }
    void OnDisable() { if (score != null) { score.Awarded -= OnAward; score.ComboPaid -= OnPaid; score.CoinsChanged -= OnCoins; } }
    void OnDestroy() { if (fallbackCanvas != null) Destroy(fallbackCanvas); }
    void OnAward(ScoreAward award)
    {
        if (award.ComboCount == 1) { entries.Clear(); displayedTier = 0; }
        int newTier = TierFor(score.ComboPoints);
        GameSfx.PlayCombo(newTier, Mathf.Max(1, tierNames != null ? tierNames.Length - 1 : 1));
        entries.Enqueue(new KeyValuePair<string, int>(award.Reason, award.Points));
        while (entries.Count > Mathf.Max(1, visibleEntries)) entries.Dequeue();
        window = Mathf.Max(0.01f, score.ComboWindowDuration);
        showUntil = Time.time + idleFadeDelay;
        if (actionFeedback != null) actionFeedback.PlayFeedbacks();
        if (newTier > displayedTier)
        {
            displayedTier = newTier;
            punch = 1f;
            GameSfx.Play("combo.tier");
            if (rankFeedback != null) rankFeedback.PlayFeedbacks();
            if (rankParticles != null) rankParticles.Play();
        }
        Refresh(false);
    }
    void OnPaid(long points, long coins)
    {
        bool damaged = score.LastComboEndReason == ComboEndReason.Damage;
        GameSfx.Play(damaged ? "combo.damage" : "combo.end");
        if (coins > 0) GameSfx.Play("coins.gain");
        entries.Clear();
        showUntil = Time.time + idleFadeDelay;
        if (payoutFeedback != null) payoutFeedback.PlayFeedbacks();
        Refresh(true);
    }
    void OnCoins(long coins) { }
    int TierFor(long points)
    {
        int result = 0;
        int[] thresholds = RunDifficulty.Selected == GameDifficulty.Easy ? easyThresholds :
            RunDifficulty.Selected == GameDifficulty.Impossible ? impossibleThresholds : normalThresholds;
        if (thresholds != null && tierNames != null)
            for (int i = 0; i < thresholds.Length && i < tierNames.Length; i++) if (points >= thresholds[i]) result = i;
        return result;
    }
    void Update()
    {
        if (displayedEnglish != LanguageOptions.IsEnglish) Refresh(false);
        float remaining = score.ComboCount > 0 ? score.ComboRemaining : 0f;
        float fraction = score.ComboCount > 0 ? Mathf.Clamp01(remaining / Mathf.Max(0.01f, window)) : 0f;
        if (timerLabel != null)
            timerLabel.text = score.ComboCount > 0 ? $"{LanguageOptions.Choose("TIEMPO", "TIME")}  {remaining:0.0} / {window:0.0} s" : "";
        if (timer != null)
        {
            timer.fillAmount = fraction;
            Color baseColor = timerBaseColor;
            bool urgent = score.ComboCount > 0 && fraction <= urgentFraction;
            timer.color = urgent ? Color.Lerp(baseColor, new Color(1f, 0.16f, 0.08f), 0.55f + 0.45f * Mathf.Sin(Time.time * 15f)) : baseColor;
        }
        if (group != null)
        {
            float wanted = score.ComboCount > 0 || Time.time < showUntil ? 1f : 0f;
            group.alpha = Mathf.MoveTowards(group.alpha, wanted, Time.deltaTime * 4f);
        }
        if (shakeTarget != null)
        {
            float strength = score.ComboCount > 0 ? Mathf.Max(0f, displayedTier) * Mathf.Max(0f, shakePerRank) : 0f;
            float phase = Time.time * Mathf.Max(0f, shakeFrequency);
            shakeTarget.anchoredPosition = shakeBasePosition + new Vector2(Mathf.Sin(phase * 1.13f), Mathf.Cos(phase)) * strength;
        }
        if (rankVisual != null)
        {
            punch = Mathf.Max(0f, punch - Time.deltaTime / Mathf.Max(0.01f, punchDuration));
            float bounce = Mathf.Sin(punch * Mathf.PI) * Mathf.Max(0f, rankPunch);
            rankVisual.localScale = rankBaseScale * (1f + bounce);
        }
    }
    Color ColorFor(int tier) => tierColors != null && tier >= 0 && tier < tierColors.Length ? tierColors[tier] : Color.white;
    void Refresh(bool paid)
    {
        displayedEnglish = LanguageOptions.IsEnglish;
        int tier = Mathf.Clamp(displayedTier, 0, tierNames != null && tierNames.Length > 0 ? tierNames.Length - 1 : 0);
        Color color = ColorFor(tier);
        if (rankLabel != null) rankLabel.text = tierNames != null && tier < tierNames.Length ? tierNames[tier] : "D";
        if (rankTitle != null) rankTitle.text = tierTitles != null && tier < tierTitles.Length ? GameplayTranslation.Text(tierTitles[tier]) : "";
        if (maskingColor != null) maskingColor.color = new Color(color.r, color.g, color.b, 0.35f);
        if (maskingParticles != null && maskingRates != null && maskingRates.Length > 0)
            maskingParticles.rateOverTime = Mathf.Max(0f, maskingRates[Mathf.Min(tier, maskingRates.Length - 1)]);
        float intensity = 1f + 0.35f * tier;
        if (headerFeedback != null) headerFeedback.Intensity = intensity;
        if (rankTextFeedback != null) rankTextFeedback.Intensity = intensity;
        if (titleTextFeedback != null) titleTextFeedback.Intensity = intensity;
        if (statsLabel != null) statsLabel.text = paid ? "" : $"COMBO {score.ComboCount}   ·   {score.ComboPoints} pts";
        if (log != null)
        {
            var lines = new List<string>();
            foreach (var entry in entries) lines.Add($"{GameplayTranslation.Text(entry.Key)}  +{entry.Value}");
            log.text = string.Join("\n", lines.ToArray());
        }
    }
    void BuildFallback()
    {
        fallbackCanvas = new GameObject("Combo HUD provisional", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        fallbackCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = fallbackCanvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080);
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        panel.transform.SetParent(fallbackCanvas.transform, false);
        var rect = (RectTransform)panel.transform; rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f); rect.anchoredPosition = new Vector2(-220f, 0f); rect.sizeDelta = new Vector2(380f, 300f);
        panel.GetComponent<Image>().color = new Color(0.03f, 0.03f, 0.05f, 0.8f); group = panel.GetComponent<CanvasGroup>();
        rankLabel = MakeText(panel.transform, "Rango", new Vector2(-120, 100), new Vector2(100, 70), 50); rankVisual = rankLabel.rectTransform;
        rankTitle = MakeText(panel.transform, "Título", new Vector2(35, 100), new Vector2(240, 50), 20);
        statsLabel = MakeText(panel.transform, "Combo", new Vector2(0, 35), new Vector2(340, 35), 22);
        timerLabel = MakeText(panel.transform, "Tiempo restante", new Vector2(0, -12), new Vector2(340, 20), 16);
        log = MakeText(panel.transform, "Registro", new Vector2(0, -70), new Vector2(340, 100), 18);
        var bar = new GameObject("Tiempo", typeof(RectTransform), typeof(Image)); bar.transform.SetParent(panel.transform, false);
        var br = (RectTransform)bar.transform; br.anchoredPosition = new Vector2(0, 5); br.sizeDelta = new Vector2(340, 7);
        timer = bar.GetComponent<Image>(); timer.type = Image.Type.Filled; timer.fillMethod = Image.FillMethod.Horizontal;
    }
    static Text MakeText(Transform parent, string name, Vector2 position, Vector2 size, int fontSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var r = (RectTransform)go.transform; r.anchoredPosition = position; r.sizeDelta = size;
        var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = Color.white; text.raycastTarget = false; return text;
    }
}
