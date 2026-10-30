using System;
using System.Collections;
using MenuUIBase.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MenuUIBase.Scenes
{
    [DisallowMultipleComponent]
    public sealed class SceneLoader : PersistentService<SceneLoader>, ISceneLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => ResetServiceState();

        public event Action<float> LoadProgressChanged;

        public bool IsLoading { get; private set; }

        public void LoadScene(string sceneName)
        {
            if (IsLoading || string.IsNullOrWhiteSpace(sceneName))
                return;

            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogError($"Scene '{sceneName}' is not enabled in Build Profiles.");
                return;
            }
            StartCoroutine(LoadSceneRoutine(sceneName));
        }

        public void ReloadCurrentScene()
        {
            LoadScene(SceneManager.GetActiveScene().name);
        }

        public void QuitApplication()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator LoadSceneRoutine(string sceneName)
        {
            IsLoading = true;
            Time.timeScale = 1f;
            LoadProgressChanged?.Invoke(0f);

            AsyncOperation operation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
            if (operation == null)
            {
                Debug.LogError($"Menu UI Base could not load scene '{sceneName}'. Is it in Build Settings?");
                IsLoading = false;
                yield break;
            }

            while (!operation.isDone)
            {
                float progress = Mathf.Clamp01(operation.progress / 0.9f);
                LoadProgressChanged?.Invoke(progress);
                yield return null;
            }

            LoadProgressChanged?.Invoke(1f);
            IsLoading = false;
        }
    }
}
