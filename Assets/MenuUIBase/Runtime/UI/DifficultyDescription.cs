using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MenuUIBase.UI
{
    // The description remains an editable scene object; this only controls visibility.
    public sealed class DifficultyDescription : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] GameObject description;
        static DifficultyDescription visible;
        Selectable selectable;

        void Awake() => selectable = GetComponent<Selectable>();
        public void OnPointerEnter(PointerEventData eventData) => Show();
        public void OnPointerExit(PointerEventData eventData) => Hide();
        public void OnSelect(BaseEventData eventData) => Show();
        public void OnDeselect(BaseEventData eventData) => Hide();
        void OnDisable() => Hide();

        void Show()
        {
            if (description == null || selectable == null || !selectable.IsInteractable()) return;
            if (visible != null && visible != this) visible.Hide();
            visible = this;
            description.SetActive(true);
        }
        void Hide()
        {
            if (description != null) description.SetActive(false);
            if (visible == this) visible = null;
        }
    }
}
