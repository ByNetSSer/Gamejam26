using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public sealed class DoorInteractor : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float range = 3f;
    Camera view;
    PlayerController player;
    RoomDoor focused;
    void Awake() { view = GetComponent<Camera>(); player = GetComponentInParent<PlayerController>(); }
    void Update()
    {
        focused = null;
        if (player == null || !player.enabled || Cursor.lockState != CursorLockMode.Locked) return;
        var ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (Physics.Raycast(ray, out var hit, range, ~0, QueryTriggerInteraction.Ignore))
            focused = hit.collider.GetComponentInParent<RoomDoor>();
        if (focused == null || focused.IsOpen) { focused = null; return; }
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        { focused.Open(); focused = null; }
    }
    void OnGUI()
    {
        if (focused == null) return;
        GUI.Box(new Rect(Screen.width * 0.5f - 70f, Screen.height * 0.5f + 24f, 140f, 26f), "E - Abrir puerta");
    }
}
