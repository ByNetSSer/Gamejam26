using System;
using System.Collections;
using MenuUIBase.Core;
using UnityEngine;

namespace MenuUIBase.Audio
{
    [DisallowMultipleComponent]
    public sealed class AudioManager : PersistentService<AudioManager>, IAudioService
    {
        private const string MasterVolumeKey = "MenuUIBase.Audio.Master";
        private const string MusicVolumeKey = "MenuUIBase.Audio.Music";
        private const string SoundEffectsVolumeKey = "MenuUIBase.Audio.SoundEffects";

        [Header("Optional preconfigured sources")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource soundEffectsSource;

        [SerializeField, Range(4, 64)] private int soundPoolSize = 24;
        private AudioSource[] voices;
        private float[] voiceVolumes;
        private float[] voiceFades;
        private AudioSource surfaceSource;
        private float surfaceVolume;
        private float surfaceFade = 1f;
        private bool fadingSurface;
        private double[] voiceStarted;
        private long playSequence;
        private AudioCue[] voiceCues;
        private Transform[] voiceTargets;
        private bool[] voiceFollows;
        private int[] voiceEmitters;
        private float[] voiceRequested;
        private sealed class RoomLoop
        {
            public AudioSource Source;
            public Transform Owner;
            public float Volume;
            public float Gain;
            public bool Retiring;
        }
        private RoomLoop[] roomLoops;

        public int DroppedSoundRequests { get; private set; }

        public int SoundPoolCapacity => voices?.Length ?? 0;
        private Coroutine musicTransition;
        private AudioSource currentMusicTarget;
        public bool IsMusicTransitioning { get; private set; }
        public AudioClip CurrentMusicClip => currentMusicTarget != null ? currentMusicTarget.clip : null;
        public float MusicRemaining => currentMusicTarget != null && currentMusicTarget.isPlaying && currentMusicTarget.clip != null
            ? Mathf.Max(0f, currentMusicTarget.clip.length - currentMusicTarget.time) : 0f;
        public void PlayPlaylistTrack(AudioClip clip, float duration, float volume, bool restart)
        {
            if (clip == null) return;
            EnsureSources();
            if (musicTransition != null) StopCoroutine(musicTransition);
            IsMusicTransitioning = true;
            musicTransition = StartCoroutine(SwitchMusic(clip, false, duration, Normalize(volume), restart));
        }
        private AudioSource secondMusicSource;
        private float musicGain, secondMusicGain;
        [SerializeField, Range(0f, 0.1f)] private float sfxAttack = 0.015f;
        [SerializeField, Range(0f, 0.2f)] private float sfxRelease = 0.04f;
        private AudioSource[] tails;
        private float[] tailVolumes, tailRemaining;

        private float masterVolume;
        private float musicVolume;
        private float soundEffectsVolume;

        public event Action VolumesChanged;

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SoundEffectsVolume => soundEffectsVolume;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState() => ResetServiceState();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureServiceExists()
        {
            _ = Instance;
        }

        protected override void OnServiceReady()
        {
            EnsureSources();
            UnityEngine.SceneManagement.SceneManager.sceneUnloaded += OnSceneUnloaded;
            masterVolume = Normalize(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            musicVolume = Normalize(PlayerPrefs.GetFloat(MusicVolumeKey, 0.8f));
            soundEffectsVolume = Normalize(PlayerPrefs.GetFloat(SoundEffectsVolumeKey, 1f));
            ApplyVolumes();
        }

        void OnSceneUnloaded(UnityEngine.SceneManagement.Scene scene) => StopSoundEffects();
        void OnDestroy() => UnityEngine.SceneManagement.SceneManager.sceneUnloaded -= OnSceneUnloaded;

        public void Play(AudioCue cue)
        {
            if (cue == null || cue.Clip == null)
                return;

            if (cue.Channel == AudioChannel.Music)
                PlayMusic(cue.Clip, cue.Loop, cue.FadeDuration, cue.Volume);
            else
                PlayPooled(cue.Clip, cue.Volume, Vector3.zero, false, cue);
        }

        public void PlayMusic(
            AudioClip clip,
            bool loop = true,
            float fadeDuration = 0.35f,
            float volumeScale = 1f)
        {
            if (clip == null)
                return;

            EnsureSources();
            if (musicTransition != null) StopCoroutine(musicTransition);
            IsMusicTransitioning = true;
            musicTransition = StartCoroutine(SwitchMusic(clip, loop, Mathf.Max(0f, fadeDuration), Normalize(volumeScale)));
        }

        public void StopMusic(float fadeDuration = 0.35f)
        {
            GetComponent<MusicPlaylistPlayer>()?.Suspend();
            FadePlaylistToSilence(fadeDuration);
        }

        // An empty selected playlist is silent without disabling future playlist changes.
        public void FadePlaylistToSilence(float fadeDuration)
        {
            IsMusicTransitioning = false;
            EnsureSources();
            if (musicTransition != null) StopCoroutine(musicTransition);
            musicTransition = StartCoroutine(FadeMusic(0f, 0f, Mathf.Max(0f, fadeDuration)));
        }

        public void PlaySoundEffect(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null)
                return;

            EnsureSources();
            PlayPooled(clip, volumeScale, Vector3.zero, false);
        }

        // Fixed capacity: reuse idle voices, enforce cue limits, then steal by priority and age.
        public void PlayAt(AudioCue cue, Vector3 position)
        {
            if (cue == null || cue.Clip == null) return;
            if (cue.Channel == AudioChannel.Music) { Play(cue); return; }
            PlayPooled(cue.Clip, cue.Volume, position, true, cue);
        }

        public void PlayFollowing(AudioCue cue, Transform emitter)
        {
            if (cue == null || cue.Clip == null || emitter == null || !emitter.gameObject.activeInHierarchy) return;
            if (cue.Channel == AudioChannel.Music) { Play(cue); return; }
            PlayPooled(cue.Clip, cue.Volume, emitter.position, true, cue, emitter);
        }

        // Room-owned ambience survives player departure until its room is unloaded.
        public void PlayRoomLoop(AudioCue cue, Transform owner, Vector3 position)
        {
            if (cue == null || cue.Clip == null || owner == null) return;
            EnsureSources();
            RoomLoop slot = null;
            foreach (var item in roomLoops)
            {
                if (item.Owner == owner && item.Source.isPlaying) return;
                if (slot == null && !item.Source.isPlaying) slot = item;
            }
            if (slot == null) { DroppedSoundRequests++; return; }
            slot.Owner = owner; slot.Volume = cue.Volume; slot.Gain = 0f; slot.Retiring = false;
            var source = slot.Source;
            source.clip = cue.Clip; source.transform.position = position;
            source.minDistance = cue.MinDistance; source.maxDistance = cue.MaxDistance;
            source.priority = cue.Priority; source.volume = 0f; source.Play();
        }

        void LateUpdate()
        {
            if (voices == null) return;
            foreach (var room in roomLoops)
            {
                if (!room.Source.isPlaying) continue;
                if (room.Owner == null || !room.Owner.gameObject.activeInHierarchy) room.Retiring = true;
                room.Gain = Mathf.MoveTowards(room.Gain, room.Retiring ? 0f : 1f, Time.unscaledDeltaTime / 0.4f);
                if (room.Retiring && room.Gain <= 0f) { room.Source.Stop(); room.Owner = null; }
            }
            for (int i = 0; i < tails.Length; i++)
            {
                tailRemaining[i] = Mathf.Max(0f, tailRemaining[i] - Time.unscaledDeltaTime);
                if (tailRemaining[i] <= 0f) tails[i].Stop();
            }
            for (int i = 0; i < voices.Length; i++)
            {
                if (!voiceFollows[i]) continue;
                var target = voiceTargets[i];
                if (!voices[i].isPlaying || target == null || !target.gameObject.activeInHierarchy)
                {
                    PreserveTail(i);
                    voices[i].Stop();
                    voiceTargets[i] = null;
                    voiceFollows[i] = false;
                }
                else voices[i].transform.position = target.position;
            }
            ApplyVolumes();
        }

        private void PlayPooled(AudioClip clip, float volume, Vector3 position, bool spatial, AudioCue cue = null, Transform target = null)
        {
            EnsureSources();
            int emitter = target != null ? target.GetInstanceID() : 0;
            int slot = -1, victim = -1, groupSlot = -1, instances = 0;
            int priority = cue != null ? cue.Priority : 128;
            for (int i = 0; i < voices.Length; i++)
            {
                if (cue != null && voiceCues[i] == cue && voiceEmitters[i] == emitter &&
                    Time.unscaledTime - voiceRequested[i] < cue.Cooldown)
                { DroppedSoundRequests++; return; }
                if (!voices[i].isPlaying) { if (slot < 0) slot = i; continue; }
                if (cue != null && voiceCues[i] == cue) instances++;
                if (cue != null && !string.IsNullOrEmpty(cue.ExclusiveGroup) && voiceCues[i] != null &&
                    voiceCues[i].ExclusiveGroup == cue.ExclusiveGroup && voiceEmitters[i] == emitter)
                    groupSlot = i;
                if (victim < 0 || voices[i].priority > voices[victim].priority ||
                    (voices[i].priority == voices[victim].priority && voiceStarted[i] < voiceStarted[victim])) victim = i;
            }
            if (groupSlot >= 0) slot = groupSlot;
            else if (cue != null && instances >= cue.MaxInstances) { DroppedSoundRequests++; return; }
            if (slot < 0)
            {
                // A lower-priority request cannot interrupt a more important warning.
                if (victim < 0 || voices[victim].priority < priority) { DroppedSoundRequests++; return; }
                slot = victim;
            }
            AudioSource voice = voices[slot];
            PreserveTail(slot);
            voice.Stop();
            voice.clip = clip;
            voice.loop = false;
            voice.priority = priority;
            voice.rolloffMode = AudioRolloffMode.Linear;
            voice.minDistance = cue != null ? cue.MinDistance : 2f;
            voice.maxDistance = cue != null ? cue.MaxDistance : 35f;
            voice.spread = 0f;
            voice.dopplerLevel = 0f;
            voice.panStereo = 0f;
            voiceCues[slot] = cue;
            voiceTargets[slot] = target;
            voiceFollows[slot] = target != null;
            voiceEmitters[slot] = emitter;
            voiceRequested[slot] = Time.unscaledTime;
            voice.pitch = 1f;
            voice.spatialBlend = spatial ? 1f : 0f;
            voice.ignoreListenerPause = !spatial;
            voice.transform.position = position;
            voiceVolumes[slot] = Normalize(volume);
            voiceFades[slot] = 1f;
            voiceStarted[slot] = ++playSequence;
            voice.volume = sfxAttack > 0f ? 0f : masterVolume * soundEffectsVolume * voiceVolumes[slot];
            voice.Play();
        }

        // Dedicated contact loop: never competes with pooled one-shot voices.
        public void SetSurfaceLoop(AudioCue cue)
        {
            if (fadingSurface) return;
            if (cue == null || cue.Clip == null)
            {
                if (surfaceSource != null) { surfaceSource.Stop(); surfaceSource.clip = null; }
                return;
            }
            if (surfaceSource == null)
            {
                surfaceSource = gameObject.AddComponent<AudioSource>();
                surfaceSource.playOnAwake = false;
                surfaceSource.loop = true;
            }
            surfaceFade = 1f;
            surfaceVolume = cue.Volume;
            surfaceSource.volume = masterVolume * soundEffectsVolume * surfaceVolume * surfaceFade;
            if (surfaceSource.clip == cue.Clip && surfaceSource.isPlaying) return;
            surfaceSource.clip = cue.Clip;
            surfaceSource.Play();
        }

        public void FadeCurrentEffects(float duration)
        {
            EnsureSources();
            fadingSurface = surfaceSource != null && surfaceSource.isPlaying;
            foreach (var room in roomLoops) room.Retiring = true;
            StartCoroutine(FadeVoices(Mathf.Max(0.01f, duration), (double[])voiceStarted.Clone()));
        }

        private IEnumerator FadeVoices(float duration, double[] started)
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                for (int i = 0; i < voices.Length; i++)
                    if (voiceStarted[i] == started[i]) voiceFades[i] = 1f - Mathf.Clamp01(t / duration);
                if (fadingSurface) surfaceFade = 1f - Mathf.Clamp01(t / duration);
                ApplyVolumes();
                yield return null;
            }
            if (fadingSurface) { fadingSurface = false; SetSurfaceLoop(null); }
            for (int i = 0; i < voices.Length; i++)
                if (voiceStarted[i] == started[i]) { voices[i].Stop(); voiceFades[i] = 1f; }
            ApplyVolumes();
        }

        public void StopSoundEffects()
        {
            fadingSurface = false;
            if (tails != null) foreach (var tail in tails) tail.Stop();
            if (roomLoops != null) foreach (var room in roomLoops) { room.Source.Stop(); room.Owner = null; }
            SetSurfaceLoop(null);
            if (voices == null) return;
            for (int i = 0; i < voices.Length; i++)
            {
                voices[i].Stop(); voices[i].clip = null;
                voiceTargets[i] = null; voiceFollows[i] = false; voiceCues[i] = null;
            }
        }

        public void SetMasterVolume(float value)
        {
            masterVolume = SaveVolume(MasterVolumeKey, value);
            ApplyVolumesAndNotify();
        }

        public void SetMusicVolume(float value)
        {
            musicVolume = SaveVolume(MusicVolumeKey, value);
            ApplyVolumesAndNotify();
        }

        public void SetSoundEffectsVolume(float value)
        {
            soundEffectsVolume = SaveVolume(SoundEffectsVolumeKey, value);
            ApplyVolumesAndNotify();
        }

        private void EnsureSources()
        {
            EnsurePool();
            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.ignoreListenerPause = true;
            }

            if (secondMusicSource == null)
            {
                secondMusicSource = gameObject.AddComponent<AudioSource>();
                secondMusicSource.playOnAwake = false;
                secondMusicSource.ignoreListenerPause = true;
            }

            if (soundEffectsSource == null)
            {
                soundEffectsSource = gameObject.AddComponent<AudioSource>();
                soundEffectsSource.playOnAwake = false;
            }
        }

        private void EnsurePool()
        {
            if (voices != null) return;
            int count = Mathf.Clamp(soundPoolSize, 4, 64);
            voices = new AudioSource[count];
            voiceVolumes = new float[count];
            voiceFades = new float[count];
            voiceStarted = new double[count];
            voiceCues = new AudioCue[count];
            voiceTargets = new Transform[count];
            voiceFollows = new bool[count];
            voiceEmitters = new int[count];
            voiceRequested = new float[count];
            roomLoops = new RoomLoop[3];
            for (int i = 0; i < roomLoops.Length; i++)
            {
                var child = new GameObject($"Room ambience {i}"); child.transform.SetParent(transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = true; source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear; source.dopplerLevel = 0f;
                roomLoops[i] = new RoomLoop { Source = source };
            }
            tails = new AudioSource[4]; tailVolumes = new float[4]; tailRemaining = new float[4];
            for (int i = 0; i < tails.Length; i++)
            {
                var child = new GameObject($"Sound release {i:00}"); child.transform.SetParent(transform, false);
                tails[i] = child.AddComponent<AudioSource>();
                tails[i].playOnAwake = false; tails[i].dopplerLevel = 0f;
            }
            for (int i = 0; i < count; i++)
            {
                var child = new GameObject($"Sound voice {i:00}");
                child.transform.SetParent(transform, false);
                voices[i] = child.AddComponent<AudioSource>();
                voices[i].playOnAwake = false;
                voices[i].minDistance = 2f;
                voices[i].maxDistance = 40f;
                voices[i].dopplerLevel = 0f;
            }
        }

        private IEnumerator SwitchMusic(AudioClip clip, bool loop, float duration, float scale, bool restart = false)
        {
            bool first = !restart && musicSource.clip == clip && musicSource.isPlaying;
            bool second = !restart && secondMusicSource.clip == clip && secondMusicSource.isPlaying;
            if (!first && !second)
            {
                first = musicGain <= secondMusicGain;
                // Rapid third-song requests release the quieter voice before reusing it.
                if ((first ? musicGain : secondMusicGain) > 0f)
                    yield return FadeMusic(first ? 0f : musicGain, first ? secondMusicGain : 0f, Mathf.Min(0.04f, duration));
                var source = first ? musicSource : secondMusicSource;
                source.Stop(); source.clip = clip; source.loop = loop; source.volume = 0f; source.Play();
            }
            currentMusicTarget = first ? musicSource : secondMusicSource;
            currentMusicTarget.loop = loop;
            yield return FadeMusic(first ? scale : 0f, first ? 0f : scale, duration);
            musicTransition = null;
            IsMusicTransitioning = false;
        }

        private IEnumerator FadeMusic(float targetA, float targetB, float duration)
        {
            float fromA = musicGain, fromB = secondMusicGain;
            for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / duration);
                musicGain = Mathf.Lerp(fromA, targetA, k);
                secondMusicGain = Mathf.Lerp(fromB, targetB, k);
                ApplyVolumes();
                yield return null;
            }
            musicGain = targetA; secondMusicGain = targetB;
            if (targetA == 0f) { musicSource.Stop(); musicSource.clip = null; }
            if (targetB == 0f) { secondMusicSource.Stop(); secondMusicSource.clip = null; }
            ApplyVolumes();
        }

        private float VoiceEnvelope(int slot)
        {
            var voice = voices[slot];
            if (voice.clip == null || !voice.isPlaying) return 0f;
            float attack = sfxAttack > 0f ? Mathf.Clamp01(voice.time / sfxAttack) : 1f;
            float release = sfxRelease > 0f ? Mathf.Clamp01((voice.clip.length - voice.time) / sfxRelease) : 1f;
            return Mathf.Min(attack, release);
        }

        private void PreserveTail(int slot)
        {
            var source = voices[slot];
            if (!source.isPlaying || sfxRelease <= 0f) return;
            int index = -1;
            for (int i = 0; i < tails.Length; i++) if (!tails[i].isPlaying) { index = i; break; }
            // Bounded reserve: never allocate more sources during a burst.
            if (index < 0) return;
            var tail = tails[index];
            tail.clip = source.clip; tail.timeSamples = source.timeSamples;
            tail.transform.position = source.transform.position;
            tail.spatialBlend = source.spatialBlend; tail.rolloffMode = source.rolloffMode;
            tail.minDistance = source.minDistance; tail.maxDistance = source.maxDistance;
            tail.ignoreListenerPause = source.ignoreListenerPause; tail.priority = source.priority;
            tailVolumes[index] = voiceVolumes[slot] * voiceFades[slot] * VoiceEnvelope(slot);
            tailRemaining[index] = sfxRelease;
            tail.volume = masterVolume * soundEffectsVolume * tailVolumes[index];
            tail.Play();
        }

        private float SaveVolume(string key, float value)
        {
            float normalized = Normalize(value);
            PlayerPrefs.SetFloat(key, normalized);
            return normalized;
        }

        static float Normalize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
        void OnApplicationFocus(bool focused) { if (!focused) PlayerPrefs.Save(); }
        protected override void OnApplicationQuit() { PlayerPrefs.Save(); base.OnApplicationQuit(); }

        private void ApplyVolumesAndNotify()
        {
            ApplyVolumes();
            VolumesChanged?.Invoke();
        }

        private void ApplyVolumes()
        {
            if (roomLoops != null)
                foreach (var room in roomLoops) room.Source.volume = masterVolume * soundEffectsVolume * room.Volume * room.Gain;
            if (voices != null)
                for (int i = 0; i < voices.Length; i++)
                    voices[i].volume = masterVolume * soundEffectsVolume * voiceVolumes[i] * voiceFades[i] * VoiceEnvelope(i);
            if (surfaceSource != null) surfaceSource.volume = masterVolume * soundEffectsVolume * surfaceVolume * surfaceFade;
            if (musicSource != null)
                musicSource.volume = masterVolume * musicVolume * musicGain;
            if (secondMusicSource != null) secondMusicSource.volume = masterVolume * musicVolume * secondMusicGain;
            if (tails != null)
                for (int i = 0; i < tails.Length; i++)
                    tails[i].volume = masterVolume * soundEffectsVolume * tailVolumes[i] * Mathf.Clamp01(tailRemaining[i] / Mathf.Max(0.001f, sfxRelease));

            if (soundEffectsSource != null)
                soundEffectsSource.volume = masterVolume * soundEffectsVolume;
        }
    }
}
