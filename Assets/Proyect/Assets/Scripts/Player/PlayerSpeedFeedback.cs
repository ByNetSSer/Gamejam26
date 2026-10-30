using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Presentation only: both effects follow the current horizontal sprint target.
[DisallowMultipleComponent, RequireComponent(typeof(PlayerController))]
public sealed class PlayerSpeedFeedback : MonoBehaviour
{
    [Header("Ojo de pez por velocidad relativa")]
    [SerializeField, Min(1f)] float responseExponent = 2f;
    [SerializeField, Range(-0.6f, 0f)] float maximumDistortion = -0.12f;
    [SerializeField, Min(0.01f)] float smoothingTime = 0.3f;

    [Header("Aberracion cromatica respecto al sprint maximo")]
    [SerializeField, Range(0f, 1f)] float maximumChromaticAberration = 1f;
    [SerializeField, Min(0.01f)] float chromaticSmoothingTime = 0.12f;

    PlayerController player;
    UniversalAdditionalCameraData cameraData;
    Volume volume;
    VolumeProfile profile;
    LensDistortion distortion;
    ChromaticAberration chromatic;
    float chromaticStrength;
    float strength;

    void Start()
    {
        player = GetComponent<PlayerController>();
        var playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null) { enabled = false; return; }
        cameraData = playerCamera.GetUniversalAdditionalCameraData();
        cameraData.renderPostProcessing = true;

        var volumeObject = new GameObject("Speed Feedback Volume");
        volumeObject.transform.SetParent(transform, false);
        // Use a layer already visible to this camera's volume stack.
        int mask = cameraData.volumeLayerMask.value;
        if (mask == 0) { Destroy(volumeObject); enabled = false; return; }
        for (int layer = 0; layer < 32; layer++)
            if ((mask & (1 << layer)) != 0) { volumeObject.layer = layer; break; }
        volume = volumeObject.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 100f;
        profile = ScriptableObject.CreateInstance<VolumeProfile>();
        distortion = profile.Add<LensDistortion>();
        distortion.intensity.Override(0f);
        distortion.xMultiplier.Override(1f);
        distortion.yMultiplier.Override(1f);
        distortion.center.Override(new Vector2(0.5f, 0.5f));
        distortion.scale.Override(1f);
        chromatic = profile.Add<ChromaticAberration>();
        chromatic.intensity.Override(0f);
        volume.sharedProfile = profile;
    }

    void LateUpdate()
    {
        if (volume == null) return;
        float speedFraction = player.isActiveAndEnabled ? player.MaximumRunSpeedFraction : 0f;
        float target = Mathf.Pow(speedFraction, Mathf.Max(1f, responseExponent));
        strength = Mathf.Lerp(strength, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothingTime)));
        distortion.intensity.value = strength * maximumDistortion;
        chromaticStrength = Mathf.Lerp(chromaticStrength, speedFraction,
            1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, chromaticSmoothingTime)));
        chromatic.intensity.value = chromaticStrength * maximumChromaticAberration;
        volume.enabled = true;
        // Post processing is shared with other feedback (e.g. parry).
        cameraData.renderPostProcessing = true;
    }

    void OnDisable()
    {
        strength = chromaticStrength = 0f;
        if (volume != null) volume.enabled = false;
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
