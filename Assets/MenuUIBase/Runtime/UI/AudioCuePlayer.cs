using MenuUIBase.Audio;
using UnityEngine;

namespace MenuUIBase.UI
{
    public sealed class AudioCuePlayer : MonoBehaviour
    {
        [SerializeField] private AudioCue cue;
        [SerializeField] private AudioClip fallbackClip;
        [SerializeField, Range(0f, 1f)] private float fallbackVolume = 1f;
        [SerializeField] private bool playOnEnable;
        [SerializeField] private bool spatialSound;
        [SerializeField] private bool followEmitter;

        private void OnEnable() { if (playOnEnable) Play(); }

        public void Play()
        {
            if (cue != null)
            {
                if (spatialSound && followEmitter) AudioManager.Instance.PlayFollowing(cue, transform);
                else if (spatialSound) AudioManager.Instance.PlayAt(cue, transform.position);
                else AudioManager.Instance.Play(cue);
            }
            else if (fallbackClip != null)
                AudioManager.Instance.PlaySoundEffect(fallbackClip, fallbackVolume);
        }
    }
}
