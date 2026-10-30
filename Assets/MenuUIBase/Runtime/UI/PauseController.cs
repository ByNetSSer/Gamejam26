using UnityEngine;

namespace MenuUIBase.UI
{
    public sealed class PauseController : MonoBehaviour
    {
        [SerializeField] private GameObject pausePanel;

        private float previousTimeScale = 1f;

        public bool IsPaused { get; private set; }

        private void Start()
        {
            if (pausePanel != null)
                pausePanel.SetActive(false);
        }

        public void TogglePause()
        {
            if (IsPaused)
                Resume();
            else
                Pause();
        }

        public void Pause()
        {
            if (IsPaused)
                return;

            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
            IsPaused = true;

            if (pausePanel != null)
                pausePanel.SetActive(true);
        }

        public void Resume()
        {
            if (!IsPaused)
                return;

            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            IsPaused = false;

            if (pausePanel != null)
                pausePanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (IsPaused)
                Time.timeScale = 1f;
        }
    }
}
