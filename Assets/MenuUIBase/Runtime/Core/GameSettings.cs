using System;
using UnityEngine;

namespace MenuUIBase.Core
{
    public enum GameDifficulty { Easy, Normal, Impossible }

    public static class RunDifficulty
    {
        public static GameDifficulty Selected { get; private set; } = GameDifficulty.Normal;
        public static void Select(GameDifficulty value) => Selected = value;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset() => Selected = GameDifficulty.Normal;
        public static int Towers => Selected == GameDifficulty.Easy ? 8 : Selected == GameDifficulty.Impossible ? 25 : 13;
        public static int MaximumRooms => Selected == GameDifficulty.Easy ? 25 : Selected == GameDifficulty.Impossible ? 50 : 32;
        public static int ExtraRooms => Selected == GameDifficulty.Easy ? 2 : 4;
        public static float PriceMultiplier(int tower)
        {
            int lastShop = Towers - 1;
            float target = Selected == GameDifficulty.Easy ? 4.5f : Selected == GameDifficulty.Impossible ? 13f : 6.7f;
            return Mathf.Lerp(1f, target, Mathf.Clamp01((tower - 1f) / (lastShop - 1f)));
        }
    }

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
