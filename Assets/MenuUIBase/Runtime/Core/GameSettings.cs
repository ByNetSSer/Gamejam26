using System;
using UnityEngine;

namespace MenuUIBase.Core
{
    public sealed class GameSettings : PersistentService<GameSettings>
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetState() => ResetServiceState();

        const string SensitivityKey = "Game.Settings.MouseSensitivity";
        public float MouseSensitivity { get; private set; }
        public event Action Changed;
        protected override void OnServiceReady()
        {
            MouseSensitivity = Sanitize(PlayerPrefs.GetFloat(SensitivityKey, 1f));
        }
        static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp(value, 0.1f, 3f);
        public void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Sanitize(value);
            PlayerPrefs.SetFloat(SensitivityKey, MouseSensitivity);
            Changed?.Invoke();
        }
        public void Save() => PlayerPrefs.Save();
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool focused) { if (!focused) Save(); }
        protected override void OnApplicationQuit() { Save(); base.OnApplicationQuit(); }
    }
}
