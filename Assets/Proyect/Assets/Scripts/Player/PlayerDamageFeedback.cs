using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// A short, bounded pulse on accepted damage; independent of health and movement rules.
[DisallowMultipleComponent, RequireComponent(typeof(PlayerEvents))]
public sealed class PlayerDamageFeedback : MonoBehaviour
{
    [Header("Pulso de dano")]
    [SerializeField, Min(0.01f)] float duration = 0.45f;
    [SerializeField, Range(0f, 1f)] float minimumStrength = 0.5f;
    [Tooltip("Fraccion de vida maxima perdida que produce el pulso completo.")]
    [SerializeField, Range(0.01f, 1f)] float fullStrengthHealthFraction = 0.3f;
    [SerializeField, Range(0f, 1f)] float vignetteIntensity = 0.4f;
    [SerializeField, Range(-2f, 0f)] float exposure = -0.35f;
    [SerializeField, Range(-0.6f, 0f)] float lensDistortion = -0.38f;
    [SerializeField, Range(0f, 1f)] float chromaticAberration = 0.15f;

    PlayerEvents events;
    PlayerHealth health;
    Volume volume;
    VolumeProfile profile;
    UniversalAdditionalCameraData cameraData;
    float remaining;
    float peak;

    void OnEnable()
    {
        events = GetComponent<PlayerEvents>();
        health = GetComponent<PlayerHealth>();
        events.DamageReceived += OnDamage;
    }

    void OnDamage(float amount)
    {
        if (amount <= 0f || !EnsureVolume()) return;
        float referenceHealth = health != null ? health.MaxHealth : 10f;
        float strength = Mathf.Lerp(minimumStrength, 1f,
            Mathf.Clamp01(amount / Mathf.Max(0.01f, referenceHealth * fullStrengthHealthFraction)));
        // Repeated hits refresh the pulse, never stack darkness beyond the configured cap.
        peak = Mathf.Max(volume.weight, strength);
        remaining = Mathf.Max(0.01f, duration);
        ApplyPulse(peak);
    }

    bool EnsureVolume()
    {
        if (volume != null) return true;
        var playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) return false;
        cameraData = playerCamera.GetUniversalAdditionalCameraData();
        int mask = cameraData.volumeLayerMask.value;
        if (mask == 0) return false;
        var go = new GameObject("Damage Feedback Volume");
        go.transform.SetParent(transform, false);
        for (int layer = 0; layer < 32; layer++)
            if ((mask & (1 << layer)) != 0) { go.layer = layer; break; }
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 200f;
        volume.weight = 0f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var vignette = profile.Add<Vignette>();
        vignette.color.Override(Color.black);
        vignette.intensity.Override(vignetteIntensity);
        vignette.smoothness.Override(0.55f);
        profile.Add<ColorAdjustments>().postExposure.Override(exposure);
        profile.Add<LensDistortion>().intensity.Override(lensDistortion);
        profile.Add<ChromaticAberration>().intensity.Override(chromaticAberration);
        volume.sharedProfile = profile;
        return true;
    }

    void LateUpdate()
    {
        if (volume == null || remaining <= 0f) return;
        // Recover even when defeat stops gameplay time.
        remaining = Mathf.Max(0f, remaining - Time.unscaledDeltaTime);
        float t = Mathf.Clamp01(remaining / Mathf.Max(0.01f, duration));
        ApplyPulse(peak * t * t * (3f - 2f * t));
    }

    void ApplyPulse(float weight)
    {
        volume.weight = weight;
        if (weight > 0f) cameraData.renderPostProcessing = true;
    }

    void OnDisable()
    {
        if (events != null) events.DamageReceived -= OnDamage;
        remaining = peak = 0f;
        if (volume != null) volume.weight = 0f;
    }

    void OnDestroy()
    {
        if (volume != null) Destroy(volume.gameObject);
        if (profile != null)
        {
            foreach (var component in profile.components) Destroy(component);
            Destroy(profile);
        }
    }
}
