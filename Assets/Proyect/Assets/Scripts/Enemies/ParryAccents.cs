using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Presentation accompanies a confirmed repel, without modifying movement or camera aim.
[RequireComponent(typeof(PlayerRepel))]
public sealed class ParryAccents : MonoBehaviour
{
    [SerializeField] AudioClip sound;
    [SerializeField, Range(0f, 1f)] float volume = 0.35f;
    [SerializeField, Range(0, 40)] int sparkCount = 14;
    [SerializeField] Color sparkColor = new Color(1f, 0.72f, 0.25f, 1f);
    [SerializeField, Min(0.01f)] float sceneEffectDuration = 0.2f;
    [SerializeField, Range(0f, 1f)] float sceneEffectStrength = 0.85f;
    IDisposable subscription;
    AudioSource source;
    ParticleSystem sparks;
    Volume sceneVolume;
    VolumeProfile sceneProfile;
    UniversalAdditionalCameraData cameraData;
    bool enabledPostProcessing;
    float effectStart;

    void OnEnable() => subscription = GetComponent<PlayerRepel>().Events.Subscribe<EnemyRepelledEvent>(OnRepelled);

    void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
        RestoreSceneEffect();
        if (sceneVolume != null) Destroy(sceneVolume.gameObject);
        if (sceneProfile != null) Destroy(sceneProfile);
        sceneVolume = null;
        sceneProfile = null;
        if (sparks != null) Destroy(sparks.gameObject);
        if (source != null) Destroy(source);
        sparks = null;
        source = null;
    }

    void OnRepelled(EnemyRepelledEvent hit)
    {
        if (source == null)
        {
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.ignoreListenerPause = false;
        }
        if (sound != null)
        {
            source.Stop();
            source.PlayOneShot(sound, volume);
        }
        PlaySparks();
        PlaySceneEffect();
    }

    void PlaySceneEffect()
    {
        var playerCamera = GetComponentInChildren<PlayerCamera>();
        if (playerCamera == null) return;
        var camera = playerCamera.GetComponent<Camera>();
        if (camera == null) return;
        if (sceneVolume == null)
        {
            cameraData = camera.GetUniversalAdditionalCameraData();
            int mask = cameraData.volumeLayerMask.value;
            if (mask == 0) return;
            int layer = 0;
            while ((mask & (1 << layer)) == 0 && layer < 31) layer++;
            var go = new GameObject("Parry - escena");
            go.layer = layer;
            go.transform.SetParent(transform, false);
            sceneVolume = go.AddComponent<Volume>();
            sceneVolume.isGlobal = true;
            sceneVolume.priority = 10000f;
            sceneProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            var color = sceneProfile.Add<ColorAdjustments>();
            color.saturation.Override(-100f);
            color.contrast.Override(35f);
            sceneVolume.sharedProfile = sceneProfile;
        }
        if (!cameraData.renderPostProcessing)
        {
            cameraData.renderPostProcessing = true;
            enabledPostProcessing = true;
        }
        effectStart = Time.unscaledTime;
        sceneVolume.weight = Mathf.Clamp01(sceneEffectStrength);
    }

    void Update()
    {
        if (sceneVolume == null || sceneVolume.weight <= 0f) return;
        float t = (Time.unscaledTime - effectStart) / Mathf.Max(0.01f, sceneEffectDuration);
        if (t >= 1f) { RestoreSceneEffect(); return; }
        sceneVolume.weight = Mathf.Clamp01(sceneEffectStrength) * (1f - t);
    }

    void RestoreSceneEffect()
    {
        if (sceneVolume != null) sceneVolume.weight = 0f;
        if (enabledPostProcessing && cameraData != null) cameraData.renderPostProcessing = false;
        enabledPostProcessing = false;
    }

    void PlaySparks()
    {
        if (sparkCount == 0) return;
        var playerCamera = GetComponentInChildren<PlayerCamera>();
        if (playerCamera == null) return;
        if (sparks == null)
        {
            var material = Resources.Load<Material>("Parry/ParrySpark");
            if (material == null) return;
            var go = new GameObject("Parry - chispas");
            go.transform.SetParent(playerCamera.transform, false);
            go.transform.localPosition = Vector3.forward * 0.8f;
            sparks = go.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.playOnAwake = false;
            main.loop = false;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.065f);
            main.maxParticles = 40;
            var emission = sparks.emission;
            emission.enabled = false;
            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.025f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 2f;
            renderer.velocityScale = 0.08f;
        }
        sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var settings = sparks.main;
        settings.startColor = sparkColor;
        sparks.Play();
        sparks.Emit(sparkCount);
    }

}
