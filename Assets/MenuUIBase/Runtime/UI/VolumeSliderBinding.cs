using MenuUIBase.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace MenuUIBase.UI
{
    [RequireComponent(typeof(Slider))]
    public sealed class VolumeSliderBinding : MonoBehaviour
    {
        [SerializeField] private Slider slider;
        [SerializeField] private AudioVolumeChannel channel;

        private AudioManager audioManager;

        private void Reset()
        {
            slider = GetComponent<Slider>();
        }

        private void OnEnable()
        {
            if (slider == null)
                slider = GetComponent<Slider>();

            audioManager = AudioManager.Instance;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.onValueChanged.AddListener(OnValueChanged);
            audioManager.VolumesChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerPrefs.Save();
            if (slider != null)
                slider.onValueChanged.RemoveListener(OnValueChanged);

            if (audioManager != null)
                audioManager.VolumesChanged -= Refresh;
        }

        private void OnValueChanged(float value)
        {
            switch (channel)
            {
                case AudioVolumeChannel.Master:
                    audioManager.SetMasterVolume(value);
                    break;
                case AudioVolumeChannel.Music:
                    audioManager.SetMusicVolume(value);
                    break;
                case AudioVolumeChannel.SoundEffects:
                    audioManager.SetSoundEffectsVolume(value);
                    break;
            }
        }

        private void Refresh()
        {
            if (slider == null || audioManager == null)
                return;

            float value = channel switch
            {
                AudioVolumeChannel.Master => audioManager.MasterVolume,
                AudioVolumeChannel.Music => audioManager.MusicVolume,
                AudioVolumeChannel.SoundEffects => audioManager.SoundEffectsVolume,
                _ => 1f
            };

            slider.SetValueWithoutNotify(value);
        }
    }
}
