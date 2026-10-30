using MenuUIBase.Audio;
using UnityEngine;

namespace MenuUIBase.Demo
{
    /// <summary>
    /// Generates tiny procedural clips so the demo has no bundled audio files.
    /// Replace this component with AudioCue assets in a real project.
    /// </summary>
    public sealed class DemoAudioProvider : MonoBehaviour
    {
        private static DemoAudioProvider instance;

        private AudioClip musicClip;
        private AudioClip clickClip;

        public static DemoAudioProvider Instance
        {
            get
            {
                if (instance == null)
                    instance = FindFirstObjectByType<DemoAudioProvider>();

                if (instance == null)
                {
                    GameObject providerObject = new GameObject(nameof(DemoAudioProvider));
                    instance = providerObject.AddComponent<DemoAudioProvider>();
                }

                return instance;
            }
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            CreateClips();
        }

        public void EnsureMusic()
        {
            CreateClips();
            AudioManager.Instance.PlayMusic(musicClip, true, 0.6f, 0.42f);
        }

        public void PlayClick()
        {
            CreateClips();
            AudioManager.Instance.PlaySoundEffect(clickClip, 0.7f);
        }

        private void CreateClips()
        {
            if (musicClip == null)
                musicClip = CreateMusicClip();

            if (clickClip == null)
                clickClip = CreateClickClip();
        }

        private static AudioClip CreateMusicClip()
        {
            const int sampleRate = 22050;
            const int seconds = 6;
            int sampleCount = sampleRate * seconds;
            float[] samples = new float[sampleCount];
            float[] notes = { 220f, 277.18f, 329.63f, 415.3f };

            for (int i = 0; i < sampleCount; i++)
            {
                float time = i / (float)sampleRate;
                int chord = Mathf.FloorToInt(time / 1.5f) % notes.Length;
                float root = notes[chord];
                float wave = Mathf.Sin(2f * Mathf.PI * root * time);
                wave += 0.45f * Mathf.Sin(2f * Mathf.PI * root * 1.5f * time);
                wave += 0.2f * Mathf.Sin(2f * Mathf.PI * root * 2f * time);
                float pulse = 0.72f + 0.28f * Mathf.Sin(2f * Mathf.PI * 0.33f * time);
                samples[i] = wave * pulse * 0.08f;
            }

            AudioClip clip = AudioClip.Create("Menu UI Base - Generated Music", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static AudioClip CreateClickClip()
        {
            const int sampleRate = 22050;
            int sampleCount = Mathf.RoundToInt(sampleRate * 0.12f);
            float[] samples = new float[sampleCount];

            for (int i = 0; i < sampleCount; i++)
            {
                float normalized = i / (float)sampleCount;
                float envelope = 1f - normalized;
                float frequency = Mathf.Lerp(820f, 520f, normalized);
                samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * i / sampleRate) * envelope * 0.22f;
            }

            AudioClip clip = AudioClip.Create("Menu UI Base - Generated Click", sampleCount, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
