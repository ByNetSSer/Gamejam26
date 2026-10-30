using System;

namespace MenuUIBase.Scenes
{
    public interface ISceneLoader
    {
        event Action<float> LoadProgressChanged;
        bool IsLoading { get; }
        void LoadScene(string sceneName);
        void ReloadCurrentScene();
        void QuitApplication();
    }
}
