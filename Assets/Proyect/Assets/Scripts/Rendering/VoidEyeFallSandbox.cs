using UnityEngine;
using UnityEngine.InputSystem;

// Ayuda de prueba para SkyboxTesting. No participa en Gameplay.
public sealed class VoidEyeFallSandbox : MonoBehaviour
{
    [SerializeField] PlayerController player;
    [SerializeField] Transform roomRoot;
    [SerializeField] Vector3 resetPosition;
    [SerializeField] Quaternion resetRotation = Quaternion.identity;
    [SerializeField] float dropOffset = 2f;
    [SerializeField] bool autoStartOnPlay = true;
    [SerializeField, Min(0f)] float autoStartDelay = 3f;

    Bounds roomBounds;
    bool hasRoomBounds;
    bool autoStarted;
    float startedAt;

    public void Configure(PlayerController target, Transform geometryRoot, Vector3 position, Quaternion rotation)
    {
        player = target;
        roomRoot = geometryRoot;
        resetPosition = position;
        resetRotation = rotation;
        CacheRoomBounds();
    }

    void Awake()
    {
        CacheRoomBounds();
        startedAt = Time.unscaledTime;
    }

    void Update()
    {
        if (autoStartOnPlay && !autoStarted && Time.unscaledTime - startedAt >= autoStartDelay)
        {
            autoStarted = true;
            StartFreeFall();
        }

        var keyboard = Keyboard.current;
        if (keyboard == null || player == null) return;
        if (keyboard.fKey.wasPressedThisFrame) { autoStarted = true; StartFreeFall(); }
        if (keyboard.rKey.wasPressedThisFrame) { autoStarted = true; Teleport(resetPosition, resetRotation); }
    }

    void CacheRoomBounds()
    {
        hasRoomBounds = false;
        if (roomRoot == null) return;
        foreach (var collider in roomRoot.GetComponentsInChildren<Collider>(true))
        {
            if (!collider.enabled || collider.isTrigger) continue;
            if (!hasRoomBounds) { roomBounds = collider.bounds; hasRoomBounds = true; }
            else roomBounds.Encapsulate(collider.bounds);
        }
    }

    void StartFreeFall()
    {
        if (!hasRoomBounds) CacheRoomBounds();
        Vector3 position = player.transform.position;
        if (hasRoomBounds)
        {
            position.x = roomBounds.center.x;
            position.z = roomBounds.center.z;
            position.y = roomBounds.min.y - dropOffset;
        }
        else position.y -= 5f;
        Teleport(position, player.transform.rotation);
    }

    void Teleport(Vector3 position, Quaternion rotation)
    {
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (controller != null) controller.enabled = false;
        player.transform.SetPositionAndRotation(position, rotation);
        if (controller != null) controller.enabled = wasEnabled;
    }

    void OnGUI()
    {
        if (player == null) return;
        const int width = 430;
        GUI.Box(new Rect(16, 16, width, 82), "Prueba de ojos del vacío");
        GUI.Label(new Rect(30, 43, width - 20, 22), "Caída automática en 3 s   |   F: caer   |   R: volver");
        GUI.Label(new Rect(30, 65, width - 20, 22), $"Altura del jugador: {player.transform.position.y:0.0} m");
    }
}
