using UnityEngine;

namespace MenuUIBase.Audio
{
    [CreateAssetMenu(fileName = "AudioCue", menuName = "Menu UI Base/Audio Cue")]
    public sealed class AudioCue : ScriptableObject
    {
        [SerializeField] private AudioClip clip;
        [SerializeField] private AudioChannel channel = AudioChannel.SoundEffects;
        [SerializeField, Range(0f, 1f)] private float volume = 1f;
        [SerializeField] private bool loop;
        [SerializeField, Min(0f)] private float fadeDuration = 0.35f;

        [Header("SFX spatialization and concurrency")]
        [SerializeField, Min(0.1f)] private float minDistance = 2f;
        [SerializeField, Min(0.2f)] private float maxDistance = 35f;
        [SerializeField, Range(0, 256)] private int priority = 128;
        [SerializeField, Min(1)] private int maxInstances = 3;
        [SerializeField, Min(0f)] private float cooldown = 0.08f;
        [Tooltip("Cues in the same group replace each other on the same emitter.")]
        [SerializeField] private string exclusiveGroup;
        public float MinDistance => Mathf.Max(0.1f, minDistance);
        public float MaxDistance => Mathf.Max(MinDistance + 0.1f, maxDistance);
        public int Priority => priority;
        public int MaxInstances => Mathf.Max(1, maxInstances);
        public float Cooldown => Mathf.Max(0f, cooldown);
        public string ExclusiveGroup => exclusiveGroup;

        public AudioClip Clip => clip;
        public AudioChannel Channel => channel;
        public float Volume => volume;
        public bool Loop => loop;
        public float FadeDuration => fadeDuration;
    }
}
