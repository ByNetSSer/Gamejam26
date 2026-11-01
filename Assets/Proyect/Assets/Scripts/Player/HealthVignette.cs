using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Uses the scene volume's runtime profile instance, preserving the shared asset.
[RequireComponent(typeof(Volume))]
public sealed class HealthVignette : MonoBehaviour
{
    [SerializeField] PlayerHealth health;
    [SerializeField, Range(0f, 1f)] float maximumIntensity = 0.65f;
    [SerializeField] Color lowHealthColor = new Color(0.65f, 0.015f, 0.01f, 1f);
    [SerializeField, Min(0.01f)] float responseTime = 0.3f;
    [SerializeField, Min(0.1f)] float responseExponent = 1.5f;
    Volume volume;
    Vignette vignette;
    Color originalColor;
    float originalIntensity;
    bool originalActive, originalColorOverride, originalIntensityOverride, ready;
    float severity;

    void Start()
    {
        volume = GetComponent<Volume>();
        if (health == null || volume == null) return;
        var profile = volume.profile;
        if (profile == null) return;
        if (!profile.TryGet(out vignette)) vignette = profile.Add<Vignette>(true);
        originalActive = vignette.active;
        originalColor = vignette.color.value;
        originalIntensity = vignette.intensity.value;
        originalColorOverride = vignette.color.overrideState;
        originalIntensityOverride = vignette.intensity.overrideState;
        ready = true;
    }
    void LateUpdate()
    {
        if (!ready || health == null || vignette == null) return;
        float fraction = health.MaxHealth > 0f ? Mathf.Clamp01(health.CurrentHealth / health.MaxHealth) : 0f;
        float target = Mathf.Pow(1f - fraction, Mathf.Max(0.1f, responseExponent));
        severity = Mathf.Lerp(severity, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, responseTime)));
        bool affected = severity > 0.001f;
        vignette.active = originalActive || affected;
        vignette.color.overrideState = originalColorOverride || affected;
        vignette.intensity.overrideState = originalIntensityOverride || affected;
        vignette.color.value = Color.Lerp(originalColor, lowHealthColor, severity);
        float baseline = originalActive && originalIntensityOverride ? originalIntensity : 0f;
        vignette.intensity.value = Mathf.Lerp(baseline, Mathf.Max(baseline, maximumIntensity), severity);
    }
    void OnDisable()
    {
        if (!ready || vignette == null) return;
        vignette.active = originalActive;
        vignette.color.value = originalColor;
        vignette.intensity.value = originalIntensity;
        vignette.color.overrideState = originalColorOverride;
        vignette.intensity.overrideState = originalIntensityOverride;
        severity = 0f;
    }
}
