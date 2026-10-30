using System;
using UnityEngine;

namespace MenuUIBase.Audio
{
    public interface IAudioService
    {
        event Action VolumesChanged;

        float MasterVolume { get; }
        float MusicVolume { get; }
        float SoundEffectsVolume { get; }

        void Play(AudioCue cue);
        void PlayAt(AudioCue cue, Vector3 position);
        void PlayFollowing(AudioCue cue, Transform emitter);
        void PlayMusic(AudioClip clip, bool loop = true, float fadeDuration = 0.35f, float volumeScale = 1f);
        void StopMusic(float fadeDuration = 0.35f);
        void PlaySoundEffect(AudioClip clip, float volumeScale = 1f);
        void SetMasterVolume(float value);
        void SetMusicVolume(float value);
        void SetSoundEffectsVolume(float value);
    }
}
