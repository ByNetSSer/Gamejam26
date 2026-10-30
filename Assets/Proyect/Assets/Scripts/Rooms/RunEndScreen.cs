using System;
using System.Collections;
using System.Collections.Generic;
using MoreMountains.Feedbacks;
using MoreMountains.Tools;
using MenuUIBase.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Minimal result screen, driven by the room manager's run events.
[RequireComponent(typeof(RoomManager))]
public sealed class RunEndScreen : MonoBehaviour
{
    [SerializeField, Min(0.1f)] float darknessDuration = 1.6f;
    [SerializeField, Range(0f, 30f)] float skullShake = 12f;
    [SerializeField, Min(0.05f)] float clockSlideDuration = 0.45f;
    MMF_Player clockEnter, clockExit;
    bool clockVisible;
    int lastTickSecond = -1;
    RoomManager rooms;
    Text clockText;
    GameObject clockCanvas;
    Transform content;
    IDisposable won, lost;
    GameObject screen;
    bool showing;

    void Awake() => rooms = GetComponent<RoomManager>();
    void OnEnable()
    {
        if (rooms == null) rooms = GetComponent<RoomManager>();
        won = rooms.Events.Subscribe<RunWonEvent>(e => Show(true, e.Score));
        lost = rooms.Events.Subscribe<RunLostEvent>(e => Show(false, e.Score, e.Reason));
    }
    void OnDisable() { won?.Dispose(); lost?.Dispose(); won = lost = null; }
    void OnDestroy() { if (showing) Time.timeScale = 1f; }

    void Show(bool victory, long score, RunLossReason reason = RunLossReason.PlayerHealthDepleted)
    {
        if (showing) return;
        showing = true;
        if (!victory) GameSfx.BeginDeath(Mathf.Max(0.1f, darknessDuration));
        if (rooms.Player != null)
        {
            rooms.Player.enabled = false;
            var cameraController = rooms.Player.GetComponentInChildren<PlayerCamera>();
            if (cameraController != null) cameraController.enabled = false;
        }
        Time.timeScale = 0f;
        if (clockCanvas != null) clockCanvas.SetActive(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (EventSystem.current == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        screen = new GameObject("Resultado de partida", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        screen.transform.SetParent(transform, false);
        var canvas = screen.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var scaler = screen.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var background = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(screen.transform, false);
        var bg = background.GetComponent<RectTransform>();
        bg.anchorMin = Vector2.zero; bg.anchorMax = Vector2.one; bg.offsetMin = bg.offsetMax = Vector2.zero;
        var shade = background.GetComponent<Image>();
        shade.color = new Color(0.025f, 0.02f, 0.04f, victory ? 0.96f : 0f);
        var panel = new GameObject("Contenido", typeof(RectTransform), typeof(CanvasGroup));
        panel.transform.SetParent(screen.transform, false);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
        content = panel.transform;
        RectTransform skull = victory ? null : CreateSkull();
        Text("Título", victory ? "¡GANASTE!" : "HAS MUERTO", new Vector2(0f, 110f), 54, new Color(0.95f, 0.78f, 0.35f));
        Text("Causa", victory ? "Has conquistado la torre." : DeathMessage(reason), new Vector2(0f, 0f), 28, Color.white);
        Text("Puntuación", $"Puntos: {score}", new Vector2(0f, -90f), 28, Color.white);
        var retry = Button("Reiniciar", new Vector2(0f, -185f), () => SceneLoader.Instance.LoadScene(gameObject.scene.name));
        Button("Menú principal", new Vector2(0f, -275f), () => SceneLoader.Instance.LoadScene("MainMenu"));
        StartCoroutine(Reveal(shade, panel.GetComponent<CanvasGroup>(), skull, retry, victory));
    }

    IEnumerator Reveal(Image shade, CanvasGroup group, RectTransform skull, GameObject retry, bool victory)
    {
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;
        if (!victory)
        {
            float duration = GameSfx.DeathTransitionDuration(Mathf.Max(0.1f, darknessDuration));
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                shade.color = new Color(0.025f, 0.02f, 0.04f, Mathf.Clamp01(t / duration) * 0.96f);
                yield return null;
            }
            shade.color = new Color(0.025f, 0.02f, 0.04f, 0.96f);
            skull.gameObject.SetActive(true);
            GameSfx.Play("death.screen");
            for (float t = 0; t < 0.45f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.45f);
                skull.localScale = Vector3.one * Mathf.Lerp(2.2f, 1f, 1f - Mathf.Pow(1f - k, 3f));
                skull.anchoredPosition = new Vector2(Mathf.Sin(t * 95f), Mathf.Cos(t * 73f)) * skullShake * (1f - k) + new Vector2(0f, 260f);
                yield return null;
            }
            skull.localScale = Vector3.one; skull.anchoredPosition = new Vector2(0f, 260f);
        }
        for (float t = 0; t < 0.65f; t += Time.unscaledDeltaTime)
        {
            group.alpha = Mathf.Clamp01(t / 0.65f);
            yield return null;
        }
        group.alpha = 1f; group.interactable = true; group.blocksRaycasts = true;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(retry);
    }

    RectTransform CreateSkull()
    {
        // Pixel silhouette made of UI geometry: no font glyph or external texture required.
        var root = new GameObject("Calavera", typeof(RectTransform));
        root.transform.SetParent(screen.transform, false);
        var rect = root.GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(0f, 260f);
        string[] pixels = { "  ######  ", " ######## ", "##########", "##  ##  ##", "##  ##  ##", "####  ####", " ######## ", "  # ## #  ", "  # ## #  " };
        for (int y = 0; y < pixels.Length; y++)
            for (int x = 0; x < pixels[y].Length; x++)
                if (pixels[y][x] == '#')
                {
                    var pixel = new GameObject("Pixel", typeof(RectTransform), typeof(Image));
                    pixel.transform.SetParent(root.transform, false);
                    var r = pixel.GetComponent<RectTransform>();
                    r.sizeDelta = new Vector2(18f, 18f); r.anchoredPosition = new Vector2((x - 4.5f) * 18f, (4f - y) * 18f);
                    pixel.GetComponent<Image>().color = new Color(0.95f, 0.91f, 0.8f);
                    pixel.GetComponent<Image>().raycastTarget = false;
                }
        root.SetActive(false);
        return rect;
    }

    static string DeathMessage(RunLossReason reason)
    {
        switch (reason)
        {
            case RunLossReason.TimeExpired: return "El tiempo te ha alcanzado.\nLlega a la sala final antes de que termine la cuenta atrás.";
            case RunLossReason.Flupin: return "Flupin te ha golpeado.\nPulsa G cuando esté cerca para repelerlo.";
            case RunLossReason.Dontsee: return "Dontsee te ha alcanzado.\nMantenlo a la vista para detener su avance.";
            case RunLossReason.MaskCharger: return "La máscara te ha arrollado.\nSal de la línea de aviso antes de su embestida.";
            case RunLossReason.Crusher: return "La aplanadora te ha aplastado.\nSal de debajo de ella durante el aviso.";
            case RunLossReason.HazardousSurface: return "Una superficie peligrosa agotó tu vida.\nEvita permanecer sobre la lava.";
            default: return "Te has quedado sin vida.\nEvita los peligros y los ataques de los enemigos.";
        }
    }

    void LateUpdate()
    {
        if (showing) return;
        if (clockCanvas == null)
        {
            if (!rooms.TimerRunning) return;
            clockCanvas = new GameObject("Tiempo de torre", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            clockCanvas.transform.SetParent(transform, false);
            clockCanvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            clockCanvas.GetComponent<Canvas>().sortingOrder = 90;
            var scale = clockCanvas.GetComponent<CanvasScaler>();
            scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1920f, 1080f);
            var label = new GameObject("Cuenta atrás", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(clockCanvas.transform, false);
            clockText = label.GetComponent<Text>();
            clockText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            clockText.fontSize = 30; clockText.alignment = TextAnchor.MiddleCenter; clockText.raycastTarget = false;
            var r = clockText.rectTransform;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, 90f); r.sizeDelta = new Vector2(600f, 100f);
            clockEnter = CreateClockSlide("Mostrar reloj FEEL", label, -65f);
            clockExit = CreateClockSlide("Ocultar reloj FEEL", label, 90f);
        }
        if (clockVisible != rooms.TimerRunning)
        {
            clockVisible = rooms.TimerRunning;
            clockEnter.StopFeedbacks();
            clockExit.StopFeedbacks();
            (clockVisible ? clockEnter : clockExit).PlayFeedbacks();
        }
        int seconds = Mathf.CeilToInt(rooms.TimeRemaining);
        bool lastMinute = rooms.TimeRemaining <= 60f;
        if (rooms.TimerRunning && Time.deltaTime > 0f && lastMinute && seconds > 0)
        {
            if (lastTickSecond != seconds) GameSfx.Play("clock.tick", 0.1f);
            lastTickSecond = seconds;
        }
        else lastTickSecond = -1;
        clockText.text = $"{seconds / 60:00}:{seconds % 60:00}" + (lastMinute ? "\n¡ÚLTIMO MINUTO!" : "");
        clockText.color = lastMinute ? new Color(1f, 0.35f, 0.25f) : Color.white;
    }

    MMF_Player CreateClockSlide(string name, GameObject target, float destinationY)
    {
        var go = new GameObject(name);
        go.transform.SetParent(clockCanvas.transform, false);
        var feedback = go.AddComponent<MMF_Player>();
        feedback.FeedbacksList = new List<MMF_Feedback>
        {
            new MMF_Position
            {
                AnimatePositionTarget = target,
                Mode = MMF_Position.Modes.ToDestination,
                Space = MMF_Position.Spaces.RectTransform,
                RelativePosition = false,
                DeterminePositionsOnPlay = true,
                DestinationPosition = new Vector3(0f, destinationY, 0f),
                AnimatePositionDuration = Mathf.Max(0.05f, clockSlideDuration),
                AnimatePositionTween = new MMTweenType(AnimationCurve.EaseInOut(0f, 0f, 1f, 1f)),
                Timing = new MMFeedbackTiming { TimescaleMode = TimescaleModes.Unscaled }
            }
        };
        feedback.Initialization();
        return feedback;
    }

    Text Text(string name, string value, Vector2 position, int size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(content, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(1000f, 100f);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value; text.fontSize = size; text.color = color; text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        return text;
    }
    GameObject Button(string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(content, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(360f, 65f);
        go.GetComponent<Image>().color = new Color(0.32f, 0.25f, 0.15f);
        go.GetComponent<Button>().onClick.AddListener(action);
        var text = Text(label + " texto", label, position, 28, Color.white);
        text.transform.SetParent(go.transform, false);
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        return go;
    }
}
