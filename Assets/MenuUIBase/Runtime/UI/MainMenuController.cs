using MenuUIBase.Audio;
using MenuUIBase.Core;
using MenuUIBase.Scenes;
using UnityEngine;

namespace MenuUIBase.UI
{
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] PanelNavigator navigator;
        [SerializeField] GameObject optionsPanel;
        [SerializeField] GameObject creditsPanel;
        [SerializeField] string gameplaySceneName = "e";
        [SerializeField] AudioCue menuMusic;
        [SerializeField] AudioCue clickSound;
        void Start()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            var playlist = Resources.Load<MusicPlaylist>("Music/MenuPlaylist");
            if (playlist != null && playlist.HasTracks) MusicPlaylistPlayer.Instance.SetBase(playlist, true);
            else if (menuMusic != null) AudioManager.Instance.Play(menuMusic);
        }
        public void Play()
        {
            if (!Application.CanStreamedLevelBeLoaded(gameplaySceneName))
            {
                Debug.LogError($"Añade {gameplaySceneName} a Build Profiles.", this);
                return;
            }
            PlayClick();
            GameSettings.Instance.Save();
            SceneLoader.Instance.LoadScene(gameplaySceneName);
        }
        public void OpenOptions() { PlayClick(); navigator.Open(optionsPanel); }
        public void OpenCredits() { PlayClick(); navigator.Open(creditsPanel); }
        public void Back() { PlayClick(); GameSettings.Instance.Save(); navigator.Back(); }
        public void PlayClick() { if (clickSound != null) AudioManager.Instance.Play(clickSound); }
        public void ResetSettings()
        {
            AudioManager.Instance.SetMasterVolume(1f);
            AudioManager.Instance.SetMusicVolume(0.8f);
            AudioManager.Instance.SetSoundEffectsVolume(1f);
            GameSettings.Instance.SetMouseSensitivity(1f);
            GameSettings.Instance.Save();
        }
        public void Quit() { GameSettings.Instance.Save(); SceneLoader.Instance.QuitApplication(); }
    }
}
