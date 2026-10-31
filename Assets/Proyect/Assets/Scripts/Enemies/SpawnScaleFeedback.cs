using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using UnityEngine;

// Explicit playback: enabling a pooled object alone does not restart the animation.
public sealed class SpawnScaleFeedback : MonoBehaviour
{
    [SerializeField, Min(0.05f)] float duration = 0.3f;
    [SerializeField, Range(0.01f, 0.5f)] float startingScale = 0.08f;
    // Serialized so a clone made during the animation retains the full-size baseline.
    [SerializeField, HideInInspector] bool captured;
    [SerializeField, HideInInspector] Vector3 fullScale;
    [SerializeField, HideInInspector] MMF_Player feedback;
    float readyAt;
    public bool IsAppearing => isActiveAndEnabled && Time.time < readyAt;
    public static SpawnScaleFeedback Play(Transform target)
    {
        var effect = target.GetComponent<SpawnScaleFeedback>();
        if (effect == null) effect = target.gameObject.AddComponent<SpawnScaleFeedback>();
        effect.Begin();
        return effect;
    }
    void Begin()
    {
        if (!captured) { fullScale = transform.localScale; captured = true; }
        if (feedback == null)
        {
            // Dedicated player preserves any existing FEEL sequence on the enemy.
            var rig = new GameObject("FEEL - aparición");
            rig.transform.SetParent(transform, false);
            feedback = rig.AddComponent<MMF_Player>();
        }
        feedback.StopFeedbacks();
        transform.localScale = fullScale * startingScale;
        var tween = new MMTweenType(AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
        feedback.FeedbacksList = new List<MMF_Feedback> { new MMF_Scale {
            Mode = MMF_Scale.Modes.ToDestination, AnimateScaleTarget = transform,
            DestinationScale = fullScale, AnimateScaleDuration = Mathf.Max(0.05f, duration),
            AnimateScaleTweenX = tween, AnimateScaleTweenY = tween, AnimateScaleTweenZ = tween
        } };
        feedback.Initialization();
        readyAt = Time.time + Mathf.Max(0.05f, duration);
        feedback.PlayFeedbacks();
    }
    void OnDisable()
    {
        if (feedback != null) feedback.StopFeedbacks();
        if (captured) transform.localScale = fullScale;
        readyAt = 0f;
    }
}
