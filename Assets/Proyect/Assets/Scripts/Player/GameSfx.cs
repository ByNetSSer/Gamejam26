using System.Collections.Generic;
using MenuUIBase.Audio;
using UnityEngine;

// Shared presentation routing, available when entering any gameplay scene directly.
public sealed class GameSfx : ScriptableObject
{
    [SerializeField] AudioBank bank;
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
