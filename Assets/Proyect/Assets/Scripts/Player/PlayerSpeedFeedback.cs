using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Presentation only: absolute speed thresholds remain useful as tower speed increases.
[DisallowMultipleComponent, RequireComponent(typeof(PlayerController))]
public sealed class PlayerSpeedFeedback : MonoBehaviour
{
    [Header("Distorsion por velocidad (unidades/segundo)")]
    [SerializeField, Min(0f)] float startSpeed = 8f;
    [SerializeField, Min(0.01f)] float fullEffectSpeed = 45f;
    [SerializeField, Min(1f)] float responseExponent = 2f;
    [SerializeField, Range(-0.6f, 0.6f)] float maximumDistortion = -0.3f;
    [SerializeField, Min(0.01f)] float smoothingTime = 0.3f;
    [Tooltip("Desactivado: solo carrera horizontal. Activado: incluye caidas y pisoton.")]
    [SerializeField] bool includeVerticalSpeed;

    PlayerController player;
    UniversalAdditionalCameraData cameraData;
    Volume volume;
    VolumeProfile profile;
    LensDistortion distortion;
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
        volume.sharedProfile = profile;
    }

    void LateUpdate()
    {
        if (volume == null) return;
        float speed = player.isActiveAndEnabled
            ? (includeVerticalSpeed ? player.Velocity.magnitude : player.HorizontalSpeed) : 0f;
        float normalized = Mathf.InverseLerp(startSpeed, Mathf.Max(startSpeed + 0.01f, fullEffectSpeed), speed);
        float target = Mathf.Pow(normalized, Mathf.Max(1f, responseExponent));
        strength = Mathf.Lerp(strength, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.01f, smoothingTime)));
        distortion.intensity.value = strength * maximumDistortion;
        volume.enabled = true;
        // Post processing is shared with other feedback (e.g. parry).
        cameraData.renderPostProcessing = true;
    }

    void OnDisable()
    {
        strength = 0f;
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
