using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.UI;

// The health value is authoritative in PlayerHealth; this component only animates its display.
public sealed class HealthHud : MonoBehaviour
{
    [SerializeField] PlayerHealth health;
    [SerializeField] Text amountLabel;
    [SerializeField] RectTransform numberVisual;
    [SerializeField, Min(0.05f)] float countDuration = 0.65f;
    [SerializeField, Min(0f)] float growthPerSecond = 0.055f;
    [SerializeField, Min(1f)] float maximumScale = 1.2f;
    [Header("Corazón: latidos y vida crítica")]
    [SerializeField] RectTransform heartVisual;
    [SerializeField] Image heartImage;
    [Tooltip("Segundos entre latidos con la vida completa.")]
    [SerializeField, Min(0.1f)] float fullHealthBeatInterval = 1.25f;
    [Tooltip("Segundos entre latidos al agotarse la vida.")]
    [SerializeField, Min(0.1f)] float criticalBeatInterval = 0.3f;
    [SerializeField, Range(0f, 1f)] float criticalHealthThreshold = 0.35f;
    [SerializeField, Min(0f)] float healthyPulseScale = 0.045f;
    [SerializeField, Min(0f)] float criticalPulseScale = 0.23f;
    [SerializeField, Min(0f)] float criticalShake = 7f;
    [SerializeField, Min(0f)] float criticalRotation = 12f;
    [SerializeField] Color criticalTint = new Color(1f, 0.48f, 0.43f, 1f);
    [Header("FEEL opcional")]
    [SerializeField] MMF_Player damageFeedback, criticalFeedback;
    float displayed, target, startValue, elapsed;
    Vector3 heartBaseScale;
    Vector2 heartBasePosition;
    Quaternion heartBaseRotation;
    Color heartBaseColor;
    float heartbeatPhase, heartClock, impact;
    bool wasCritical, initialized;
    bool counting;
    Vector3 originalScale;
    void Start()
    {
        if (health == null) return;
        if (numberVisual != null) originalScale = numberVisual.localScale;
        if (heartVisual != null)
        {
            heartBaseScale = heartVisual.localScale;
            heartBasePosition = heartVisual.anchoredPosition;
            heartBaseRotation = heartVisual.localRotation;
        }
        if (heartImage != null) heartBaseColor = heartImage.color;
        initialized = true;
        wasCritical = health.MaxHealth > 0f && health.CurrentHealth / health.MaxHealth <= criticalHealthThreshold;
        displayed = target = health.CurrentHealth;
        Refresh();
    }
    void Update()
    {
        if (health == null || GameplayPauseMenu.IsPaused) return;
        float actual = health.CurrentHealth;
        if (!Mathf.Approximately(actual, target))
        {
            if (actual < target)
            {
                impact = Mathf.Clamp01(0.35f + (target - actual) / Mathf.Max(1f, health.MaxHealth) * 4f);
                if (damageFeedback != null) damageFeedback.PlayFeedbacks();
            }
            startValue = displayed; target = actual; elapsed = 0f; counting = true;
        }
        if (counting)
        {
            elapsed += Time.unscaledDeltaTime;
            float fraction = Mathf.Clamp01(elapsed / Mathf.Max(0.05f, countDuration));
            displayed = Mathf.Lerp(startValue, target, fraction);
            if (fraction >= 1f) { displayed = target; counting = false; }
            Refresh();
        }
        AnimateHeart();
        if (numberVisual != null)
        {
            float scale = counting ? Mathf.Min(maximumScale, 1f + elapsed * growthPerSecond) : 1f;
            numberVisual.localScale = Vector3.Lerp(numberVisual.localScale, originalScale * scale, 10f * Time.unscaledDeltaTime);
        }
    }
    void AnimateHeart()
    {
        if (heartVisual == null) return;
        float dt = Mathf.Max(0f, Time.deltaTime);
        float fraction = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
        float urgency = 1f - fraction;
        float panic = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, Mathf.Max(0.01f, criticalHealthThreshold), fraction));
        bool critical = fraction <= criticalHealthThreshold && health.CurrentHealth > 0f;
        if (critical && !wasCritical && criticalFeedback != null) criticalFeedback.PlayFeedbacks();
        wasCritical = critical;
        float interval = Mathf.Lerp(Mathf.Max(0.1f, fullHealthBeatInterval), Mathf.Max(0.1f, criticalBeatInterval), urgency * urgency);
        heartbeatPhase = Mathf.Repeat(heartbeatPhase + dt / interval, 1f);
        heartClock += dt;
        // A quick double beat makes it read as a heartbeat even at low frequency.
        float first = Mathf.Exp(-Mathf.Pow((heartbeatPhase - 0.14f) / 0.075f, 2f));
        float second = Mathf.Exp(-Mathf.Pow((heartbeatPhase - 0.34f) / 0.09f, 2f)) * 0.65f;
        float pulse = (first + second) * Mathf.Lerp(healthyPulseScale, criticalPulseScale, urgency);
        impact = Mathf.MoveTowards(impact, 0f, dt * 2.8f);
        heartVisual.localScale = heartBaseScale * (1f + pulse + impact * 0.15f);
        float tremor = Mathf.SmoothStep(0f, 1f, panic);
        float noiseX = Mathf.PerlinNoise(heartClock * 23f, 0.3f) * 2f - 1f;
        float noiseY = Mathf.PerlinNoise(0.7f, heartClock * 27f) * 2f - 1f;
        heartVisual.anchoredPosition = heartBasePosition + new Vector2(noiseX, noiseY) * criticalShake * tremor;
        float rotation = Mathf.Sin(heartClock * 18f) * criticalRotation * tremor + noiseX * 5f * impact;
        heartVisual.localRotation = heartBaseRotation * Quaternion.Euler(0f, 0f, rotation);
        if (heartImage != null) heartImage.color = Color.Lerp(heartBaseColor, criticalTint, tremor * (0.3f + 0.35f * first));
    }
    void OnDisable()
    {
        if (!initialized) return;
        if (heartVisual != null)
        {
            heartVisual.localScale = heartBaseScale;
            heartVisual.anchoredPosition = heartBasePosition;
            heartVisual.localRotation = heartBaseRotation;
        }
        if (heartImage != null) heartImage.color = heartBaseColor;
    }
    void Refresh() { if (amountLabel != null) amountLabel.text = Mathf.CeilToInt(Mathf.Max(0f, displayed)).ToString(); }
}
