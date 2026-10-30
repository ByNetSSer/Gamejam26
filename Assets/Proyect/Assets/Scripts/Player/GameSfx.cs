using System.Collections.Generic;
using MenuUIBase.Audio;
using UnityEngine;

// Shared presentation routing, available when entering any gameplay scene directly.
public sealed class GameSfx : ScriptableObject
{
    [SerializeField] AudioBank bank;
    [Header("Respuesta del salto")]
    [SerializeField, Range(0f, 1f)] float jumpMinimumVolume = 0.2f;
    [SerializeField, Min(0.01f)] float jumpFullVolumeSpeed = 24f;
    [Header("Tono del combo")]
    [SerializeField, Range(0.5f, 2f)] float comboLowestPitch = 1f;
    [SerializeField, Range(0.5f, 2f)] float comboHighestPitch = 1.5f;

    public static void PlayJump(float speed)
    {
        var cue = Find("player.jump");
        if (cue == null || settings == null) return;
        float scale = Mathf.Lerp(settings.jumpMinimumVolume, 1f, Mathf.Clamp01(speed / Mathf.Max(0.01f, settings.jumpFullVolumeSpeed)));
        AudioManager.Instance.PlayAdjusted(cue, scale, 1f);
    }

    public static void PlayCombo(int tier, int highestTier)
    {
        var cue = Find("combo.add");
        if (cue == null || settings == null) return;
        float pitch = Mathf.Lerp(settings.comboLowestPitch, settings.comboHighestPitch, Mathf.Clamp01((float)tier / Mathf.Max(1, highestTier)));
        AudioManager.Instance.PlayAdjusted(cue, 1f, pitch);
    }

    static GameSfx settings;
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { settings = null; lastPlayed.Clear(); }

    public static AudioCue Find(string id)
    {
        if (settings == null) settings = Resources.Load<GameSfx>("GameSfx");
        return settings != null && settings.bank != null ? settings.bank.Find(id) : null;
    }

    public static void PlayAt(string id, Vector3 position)
    {
        var cue = Find(id);
        if (cue != null) AudioManager.Instance.PlayAt(cue, position);
    }

    public static void PlayFollowing(string id, Transform emitter)
    {
        var cue = Find(id);
        if (cue != null) AudioManager.Instance.PlayFollowing(cue, emitter);
    }

    public static void StartTarRoom(Transform room, Vector3 position)
    {
        var cue = Find("surface.tar");
        if (cue != null) AudioManager.Instance.PlayRoomLoop(cue, room, position);
    }

    public static void BeginDeath(float duration)
    {
        AudioManager.Instance.FadeCurrentEffects(duration);
        AudioManager.Instance.StopMusic(duration);
        Play("death.start");
    }

    public static float DeathTransitionDuration(float minimum)
    {
        var cue = Find("death.start");
        return cue != null && cue.Clip != null ? Mathf.Max(minimum, cue.Clip.length) : minimum;
    }

    public static void Play(string id, float minimumInterval = 0.05f)
    {
        if (settings == null) settings = Resources.Load<GameSfx>("GameSfx");
        if (settings == null || settings.bank == null) return;
        float now = Time.unscaledTime;
        if (lastPlayed.TryGetValue(id, out float previous) && now - previous < minimumInterval) return;
        lastPlayed[id] = now;
        settings.bank.Play(id);
    }
}
