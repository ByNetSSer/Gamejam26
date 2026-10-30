using UnityEngine;

namespace MenuUIBase.Scenes
{
    public sealed class SceneButton : MonoBehaviour
    {
        [SerializeField] private string sceneName;

        public void LoadConfiguredScene()
        {
            SceneLoader.Instance.LoadScene(sceneName);
        }

        public void LoadScene(string targetScene)
        {
            SceneLoader.Instance.LoadScene(targetScene);
        }

        public void ReloadCurrentScene()
        {
            SceneLoader.Instance.ReloadCurrentScene();
        }

        public void QuitApplication()
        {
            SceneLoader.Instance.QuitApplication();
        }
    }
}
