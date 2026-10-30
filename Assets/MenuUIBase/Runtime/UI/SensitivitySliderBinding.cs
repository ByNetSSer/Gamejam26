using MenuUIBase.Core;
using UnityEngine;
using UnityEngine.UI;

namespace MenuUIBase.UI
{
    [RequireComponent(typeof(Slider))]
    public sealed class SensitivitySliderBinding : MonoBehaviour
    {
        Slider slider;
        GameSettings settings;
        void OnEnable()
        {
            slider = GetComponent<Slider>();
            settings = GameSettings.Instance;
            slider.minValue = 0.1f;
            slider.maxValue = 3f;
            slider.onValueChanged.AddListener(settings.SetMouseSensitivity);
            settings.Changed += Refresh;
            Refresh();
        }
        void Refresh() => slider.SetValueWithoutNotify(settings.MouseSensitivity);
        void OnDisable()
        {
            if (settings == null) return;
            slider.onValueChanged.RemoveListener(settings.SetMouseSensitivity);
            settings.Changed -= Refresh;
            settings.Save();
        }
    }
}
