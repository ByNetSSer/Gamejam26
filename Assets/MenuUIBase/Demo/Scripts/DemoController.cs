using MenuUIBase.Scenes;
using MenuUIBase.UI;
using UnityEngine;

namespace MenuUIBase.Demo
{
    public sealed class DemoController : MonoBehaviour
    {
        [SerializeField] private PanelNavigator navigator;
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private string menuSceneName = "MenuUIBase_MenuDemo";
        [SerializeField] private string gameplaySceneName = "MenuUIBase_GameplayDemo";

        private void Start()
        {
            DemoAudioProvider.Instance.EnsureMusic();
        }

        public void StartDemo()
        {
            PlayClick();
            SceneLoader.Instance.LoadScene(gameplaySceneName);
        }

        public void ReturnToMenu()
        {
            PlayClick();
            SceneLoader.Instance.LoadScene(menuSceneName);
        }

        public void OpenOptions()
        {
            PlayClick();
            navigator.Open(optionsPanel);
        }

        public void OpenCredits()
        {
            PlayClick();
            navigator.Open(creditsPanel);
        }

        public void Back()
        {
            PlayClick();
            navigator.Back();
        }

        public void PlayClick()
        {
            DemoAudioProvider.Instance.PlayClick();
        }

        public void Quit()
        {
            PlayClick();
            SceneLoader.Instance.QuitApplication();
        }
    }
}
