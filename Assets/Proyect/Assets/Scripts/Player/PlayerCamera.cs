using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-10)]
[RequireComponent(typeof(Camera))]
public sealed class PlayerCamera : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField, Min(0f)] float mouseSensitivity = 0.12f;
    [SerializeField, Min(0.01f)] float speedForMaxPenalty = 30f;
    [SerializeField, Range(0f, 1f)] float minimumControl = 0.3f;
    [SerializeField, Min(0f)] float baseControl = 1f;
    [SerializeField, Range(1f, 89f)] float pitchLimit = 85f;
    float pitch;
    void Awake() { if (GetComponent<DoorInteractor>() == null) gameObject.AddComponent<DoorInteractor>(); }
    public void SetPlayer(PlayerController target) => player = target;
    void OnEnable() { if (Application.isPlaying) LockCursor(true); }
    void OnDisable() { if (Application.isPlaying) LockCursor(false); }
    static void LockCursor(bool locked) { Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None; Cursor.visible = !locked; }
    void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) LockCursor(false);
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);
        if (player == null || Mouse.current == null || Cursor.lockState != CursorLockMode.Locked) return;
        float speedFraction = Mathf.Clamp01(player.HorizontalSpeed / Mathf.Max(0.01f, player.Stats.Evaluate(PlayerStat.CameraPenaltySpeed, speedForMaxPenalty)));
        float control = player.Stats.Evaluate(PlayerStat.CameraControl, baseControl) * Mathf.Lerp(1f, Mathf.Clamp01(player.Stats.Evaluate(PlayerStat.CameraMinimumControl, minimumControl)), speedFraction);
        Vector2 look = Mouse.current.delta.ReadValue() * (player.Stats.Evaluate(PlayerStat.CameraSensitivity, mouseSensitivity) * control);
        player.transform.Rotate(0f, look.x, 0f, Space.World);
        float maxPitch = Mathf.Clamp(player.Stats.Evaluate(PlayerStat.CameraPitchLimit, pitchLimit), 1f, 89f);
        pitch = Mathf.Clamp(pitch - look.y, -maxPitch, maxPitch);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
