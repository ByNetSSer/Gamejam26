using System;
using UnityEngine;

// Presentation only: the repel event is emitted after an enemy accepts the hit.
[RequireComponent(typeof(PlayerRepel))]
public sealed class ParryFeedback : MonoBehaviour
{
    [SerializeField] GameObject impactFramePrefab;
    [SerializeField, Range(0.01f, 1f)] float slowMultiplier = 0.02f;
    [SerializeField, Min(0f)] float slowDuration = 0.299f;
    [SerializeField, Min(0f)] float recoveryDuration = 0f;
    [SerializeField, Min(0.01f)] float frameDistance = 0.5f;
    [SerializeField, Min(0.01f)] float frameScale = 0.55f;
    IDisposable subscription;
    GameObject impactFrame;
    float baseScale, appliedScale, startedAt;
    bool slowing;

    void OnEnable()
    {
        subscription = GetComponent<PlayerRepel>().Events.Subscribe<EnemyRepelledEvent>(OnRepelled);
    }

    void OnDisable()
    {
        subscription?.Dispose();
        subscription = null;
        RestoreTime();
        if (impactFrame != null) Destroy(impactFrame);
    }

    void OnRepelled(EnemyRepelledEvent hit)
    {
        // Multiple enemies in one G press should restart the same effect, not stack it.
        if (!slowing)
        {
            if (Time.timeScale <= 0f) return;
            baseScale = Time.timeScale;
            slowing = true;
        }
        else if (!Mathf.Approximately(Time.timeScale, appliedScale))
        {
            // Another system owns the clock now (for example a pause).
            slowing = false;
            return;
        }
        startedAt = Time.unscaledTime;
        appliedScale = baseScale * Mathf.Clamp(slowMultiplier, 0.01f, 1f);
        Time.timeScale = appliedScale;
        PlayImpactFrame();
    }

    void Update()
    {
        if (!slowing) return;
        if (!Mathf.Approximately(Time.timeScale, appliedScale)) { slowing = false; return; }
        float elapsed = Time.unscaledTime - startedAt;
        if (elapsed < slowDuration) return;
        if (recoveryDuration <= 0f || elapsed >= slowDuration + recoveryDuration)
        { RestoreTime(); return; }
        float t = (elapsed - slowDuration) / recoveryDuration;
        appliedScale = Mathf.Lerp(baseScale * Mathf.Clamp(slowMultiplier, 0.01f, 1f), baseScale, t);
        Time.timeScale = appliedScale;
    }

    void RestoreTime()
    {
        if (slowing && Mathf.Approximately(Time.timeScale, appliedScale)) Time.timeScale = baseScale;
        slowing = false;
    }

    void PlayImpactFrame()
    {
        if (impactFramePrefab == null) impactFramePrefab = Resources.Load<GameObject>("Parry/ImpactFrameOriginal");
        var camera = GetComponentInChildren<PlayerCamera>();
        if (impactFramePrefab == null || camera == null) return;
        if (impactFrame == null)
        {
            impactFrame = Instantiate(impactFramePrefab, camera.transform);
            impactFrame.transform.localPosition = Vector3.forward * frameDistance;
            impactFrame.transform.localRotation = Quaternion.identity;
            impactFrame.transform.localScale = Vector3.one * Mathf.Max(0.01f, frameScale);
            foreach (var particle in impactFrame.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = particle.main;
                main.useUnscaledTime = true;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }
        foreach (var particle in impactFrame.GetComponentsInChildren<ParticleSystem>(true))
        {
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particle.Play(true);
        }
    }
}

