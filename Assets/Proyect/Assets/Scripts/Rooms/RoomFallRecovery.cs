using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Runs after room transitions, so the checkpoint always belongs to the current room.
[DefaultExecutionOrder(200)]
[RequireComponent(typeof(RoomManager))]
public sealed class RoomFallRecovery : MonoBehaviour
{
    [SerializeField, Min(1f)] float fallMargin = 8f;
    [SerializeField, Min(1)] int bonusPoints = 100;
    [SerializeField, Min(0.1f)] float messageDuration = 2.5f;
    [SerializeField, Min(0f)] float messageShake = 32f;
    RoomManager rooms;
    RoomDefinition cachedCurrent, cachedNext, cachedPrevious;
    float lowestY;
    bool hasBounds, hasCheckpoint;
    Vector3 checkpoint;
    float checkpointDistance;
    Coroutine message;
    GameObject messageCanvas;
    float nextRetry;

    void Awake() => rooms = GetComponent<RoomManager>();

    void LateUpdate()
    {
        var player = rooms.Player;
        var current = rooms.CurrentRoom;
        if (player == null || current == null || rooms.IsLost || rooms.IsWon) return;
        if (cachedCurrent != current || cachedNext != rooms.NextRoom || cachedPrevious != rooms.PreviousRoom)
        {
            bool changedRoom = cachedCurrent != current;
            cachedCurrent = current; cachedNext = rooms.NextRoom; cachedPrevious = rooms.PreviousRoom;
            RefreshBounds();
            if (changedRoom)
            {
                hasCheckpoint = current.PlayerStart != null;
                if (hasCheckpoint) checkpoint = current.PlayerStart.position;
                checkpointDistance = float.PositiveInfinity;
            }
        }
        if (hasBounds && player.transform.position.y < lowestY - fallMargin)
        {
            if (Time.unscaledTime >= nextRetry) { nextRetry = Time.unscaledTime + 1f; Recover(player, current); }
            return;
        }
        if (!player.IsGrounded) return;
        // Keep the first safe ground near the entrance as a fallback when geometry
        // cannot be sampled (for example, a custom room without floor colliders).
        Vector3 origin = current.EntryAnchor != null ? current.EntryAnchor.position : current.PlayerStart != null ? current.PlayerStart.position : current.transform.position;
        float distance = Vector3.ProjectOnPlane(player.transform.position - origin, Vector3.up).sqrMagnitude;
        if (!hasCheckpoint || distance < checkpointDistance)
        {
            checkpoint = player.transform.position;
            checkpointDistance = distance;
            hasCheckpoint = true;
        }
    }

    void RefreshBounds()
    {
        hasBounds = false;
        lowestY = float.PositiveInfinity;
        IncludeRoom(cachedPrevious); IncludeRoom(cachedCurrent); IncludeRoom(cachedNext);
    }

    void IncludeRoom(RoomDefinition room)
    {
        if (room == null) return;
        foreach (var collider in room.GetComponentsInChildren<Collider>())
        {
            if (!collider.enabled || collider.isTrigger || !collider.gameObject.activeInHierarchy) continue;
            lowestY = Mathf.Min(lowestY, collider.bounds.min.y);
            hasBounds = true;
        }
    }

    void Recover(PlayerController player, RoomDefinition room)
    {
        var controller = player.GetComponent<CharacterController>();
        bool wasEnabled = controller != null && controller.enabled;
        if (wasEnabled) controller.enabled = false;
        bool found = TryFindGround(room, controller, out Vector3 destination);
        if (!found && hasCheckpoint) { destination = checkpoint; found = true; }
        if (!found)
        {
            if (wasEnabled) controller.enabled = true;
            Debug.LogWarning("No se encontró suelo seguro para recuperar al jugador en " + room.name, room);
            return;
        }
        player.transform.position = destination;
        player.ResetMotion();
        Physics.SyncTransforms();
        if (wasEnabled) controller.enabled = true;
        var score = player.GetComponent<PlayerScore>();
        if (score != null) score.Award("Exploiter", bonusPoints);
        if (message != null) StopCoroutine(message);
        if (messageCanvas != null) Destroy(messageCanvas);
        message = StartCoroutine(ShowMessage());
        Debug.LogWarning("Jugador recuperado tras caer fuera del mapa en " + room.name, room);
    }

    bool TryFindGround(RoomDefinition room, CharacterController controller, out Vector3 destination)
    {
        destination = default;
        Transform anchor = room.EntryAnchor != null ? room.EntryAnchor : room.PlayerStart;
        if (anchor == null || controller == null) return false;
        Vector3 scale = controller.transform.lossyScale;
        float radius = Mathf.Max(0.1f, controller.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
        float height = Mathf.Max(radius * 2f, controller.height * Mathf.Abs(scale.y));
        float centerOffset = controller.center.y * scale.y;
        float bottomOffset = centerOffset - height * 0.5f;
        // Search outwards from the entry; the hatch opening itself is deliberately skipped
        // when it has no floor. The ray only accepts colliders belonging to this room.
        for (int ring = 0; ring <= 12; ring++)
        {
            float distance = ring * 0.75f;
            int steps = ring == 0 ? 1 : 16;
            for (int i = 0; i < steps; i++)
            {
                float angle = i * Mathf.PI * 2f / steps;
                Vector3 origin = anchor.position + new Vector3(Mathf.Cos(angle) * distance, 2f, Mathf.Sin(angle) * distance);
                var hits = Physics.RaycastAll(origin, Vector3.down, 100f, ~0, QueryTriggerInteraction.Ignore);
                System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                foreach (var hit in hits)
                {
                    if (!hit.collider.transform.IsChildOf(room.transform) || hit.normal.y < 0.7f) continue;
                    Vector3 position = hit.point + Vector3.up * (-bottomOffset + 0.08f);
                    Vector3 center = position + Vector3.up * centerOffset;
                    float half = Mathf.Max(0f, height * 0.5f - radius);
                    float checkRadius = Mathf.Max(0.01f, radius - controller.skinWidth);
                    if (Physics.CheckCapsule(center + Vector3.up * half, center - Vector3.up * half, checkRadius, ~0, QueryTriggerInteraction.Ignore)) continue;
                    destination = position;
                    return true;
                }
            }
        }
        return false;
    }

    IEnumerator ShowMessage()
    {
        var canvasObject = new GameObject("Exploiter", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        messageCanvas = canvasObject;
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920f, 1080f);
        var labelObject = new GameObject("Texto", typeof(RectTransform), typeof(Text), typeof(Outline));
        labelObject.transform.SetParent(canvasObject.transform, false);
        var rect = labelObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.7f); rect.sizeDelta = new Vector2(800f, 140f);
        var label = labelObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.text = "Exploiter";
        label.fontSize = 100; label.fontStyle = FontStyle.Bold; label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.15f, 0.1f); label.raycastTarget = false;
        var outline = labelObject.GetComponent<Outline>(); outline.effectColor = Color.black; outline.effectDistance = new Vector2(4f, -4f);
        for (float elapsed = 0f; elapsed < messageDuration; elapsed += Time.unscaledDeltaTime)
        {
            float fade = Mathf.Clamp01((messageDuration - elapsed) / 0.4f);
            rect.anchoredPosition = new Vector2(Mathf.Sin(elapsed * 71f), Mathf.Cos(elapsed * 83f)) * messageShake * fade;
            label.color = new Color(1f, 0.15f, 0.1f, fade);
            yield return null;
        }
        Destroy(canvasObject);
        messageCanvas = null;
        message = null;
    }
}
