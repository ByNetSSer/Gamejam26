using System;
using UnityEngine;
using UnityEngine.Rendering;

public enum PlayerHandAction
{
    Punch,
    Parry
}

public readonly struct PlayerHandActionEvent
{
    public readonly PlayerController Player;
    public readonly PlayerHandAction Action;

    public PlayerHandActionEvent(PlayerController player, PlayerHandAction action)
    {
        Player = player;
        Action = action;
    }
}

// Presentation-only first-person hands. SpriteRenderers are camera children, so they
// remain world-space meshes while the overlay shader keeps them above scene geometry.
[ExecuteAlways]
[DefaultExecutionOrder(20)]
[RequireComponent(typeof(PlayerController))]
public sealed class FirstPersonHandAnimation : MonoBehaviour
{
    const string RunFramesPath = "Anjimations/HandAnimation";
    const string PunchFramesPath = "Anjimations/PunitoAnimation";
    const string ParryFramesPath = "Anjimations/ParryAnimation";
    const string GrabFramesPath = "Anjimations/AgarrarFinale";

    [Header("Velocidad de animación")]
    [SerializeField, Min(1f)] float actionFramesPerSecond = 16f;
    [Tooltip("Cadencia de los brazos cuando apenas comienza el movimiento.")]
    [SerializeField, Min(1f)] float slowRunFramesPerSecond = 9f;
    [Tooltip("Cadencia de los brazos durante una carrera rápida.")]
    [SerializeField, Min(1f)] float fastRunFramesPerSecond = 22f;
    [Tooltip("Oculta las manos solo cuando no hay input y la inercia cae por debajo de esta velocidad.")]
    [SerializeField, Min(0f)] float minimumVisibleRunSpeed = 0.35f;

    [Header("Respuesta a Base Speed")]
    [Tooltip("Target Speed calculada desde Base Speed a la que se usan Walking Hand Distance y Slow Run Frames Per Second.")]
    [SerializeField, Min(0f)] float minimumBaseSpeed = 6f;
    [Tooltip("Target Speed calculada desde Base Speed a la que se alcanzan la distancia mínima y Fast Run Frames Per Second. Los valores superiores quedan limitados a este máximo.")]
    [SerializeField, Min(0f)] float maximumBaseSpeed = 1000f;

    [Header("Presentación delante de cámara")]
    [SerializeField, Min(0.01f)] float planeDistance = 0.4f;
    [SerializeField, Range(0.1f, 1.5f)] float screenWidthFraction = 1f;
    [SerializeField, Range(-0.2f, 0.2f)] float bottomMarginFraction;
    [Tooltip("Distancia de la mano activa respecto al centro cuando el jugador apenas camina.")]
    [SerializeField, Range(0f, 0.5f)] float walkingHandDistance = 0.3f;
    [Tooltip("Límite de acercamiento de la mano activa respecto al centro.")]
    [SerializeField, Range(0f, 0.5f)] float minimumHandDistanceAtMaximumSpeed = 0.02f;
    [SerializeField] int sortingOrder = 32000;

    [Header("Previsualización en Edit Mode")]
    [Tooltip("Muestra las manos antes de pulsar Play. Solo afecta a la vista previa.")]
    [SerializeField] bool previewInEditMode = true;
    [Tooltip("Valor de Target Speed usado para probar separación y cadencia sin entrar en Play Mode.")]
    [SerializeField, Min(0f)] float previewSpeed = 6f;
    [Tooltip("Fotograma de HandAnimation mostrado durante la previsualización.")]
    [SerializeField, Range(0f, 1f)] float previewAnimationPhase = 0.25f;
    [Tooltip("Muestra ambos lados a la vez para comparar su posición. En juego continúan alternándose.")]
    [SerializeField] bool previewBothHands = true;

    PlayerController player;
    PlayerRepel repel;
    Camera view;
    Material overlayMaterial;
    GameObject presentationRoot;
    SpriteRenderer leftRunHand, rightRunHand, actionHand;
    Sprite[] runFrames, punchFrames, parryFrames, grabFrames, activeActionFrames;
    IDisposable actionSubscription;
    float runClock, actionClock;

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        EnsureInitialized();
        if (!Application.IsPlaying(gameObject))
        {
            UpdateEditModePreview();
            return;
        }

        if (repel == null) repel = GetComponent<PlayerRepel>();
        actionSubscription?.Dispose();
        actionSubscription = repel != null ? repel.Events.Subscribe<PlayerHandActionEvent>(OnActionRequested) : null;
    }

    void OnDisable()
    {
        actionSubscription?.Dispose();
        actionSubscription = null;
        activeActionFrames = null;
        SetRenderersVisible(false, false);
    }

    void OnDestroy()
    {
        actionSubscription?.Dispose();
        actionSubscription = null;

        if (presentationRoot != null)
        {
            if (Application.isPlaying) Destroy(presentationRoot);
            else DestroyImmediate(presentationRoot);
        }

        if (overlayMaterial != null)
        {
            if (Application.isPlaying) Destroy(overlayMaterial);
            else DestroyImmediate(overlayMaterial);
        }
    }

    void OnValidate()
    {
        maximumBaseSpeed = Mathf.Max(minimumBaseSpeed, maximumBaseSpeed);

#if UNITY_EDITOR
        if (!Application.isPlaying)
            UnityEditor.EditorApplication.delayCall += RefreshEditModePreview;
#endif
    }

#if UNITY_EDITOR
    void RefreshEditModePreview()
    {
        if (this == null || !isActiveAndEnabled || Application.isPlaying) return;
        EnsureInitialized();
        UpdateEditModePreview();
        UnityEditor.SceneView.RepaintAll();
    }
#endif

    public void PlayGrab()
    {
        BeginAction(grabFrames);
    }

    void OnActionRequested(PlayerHandActionEvent message)
    {
        if (message.Player != player) return;
        BeginAction(message.Action == PlayerHandAction.Punch ? punchFrames : parryFrames);
    }

    void BeginAction(Sprite[] frames)
    {
        if (frames == null || frames.Length == 0 || player == null || player.IsReadingTutorial) return;
        activeActionFrames = frames;
        actionClock = 0f;
        if (actionHand != null) actionHand.sprite = frames[0];
    }

    void LateUpdate()
    {
        if (!Application.IsPlaying(gameObject))
        {
            EnsureInitialized();
            UpdateEditModePreview();
            return;
        }

        if (player == null || view == null || player.IsReadingTutorial || GameplayPauseMenu.IsPaused ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            activeActionFrames = null;
            SetRenderersVisible(false, false);
            return;
        }

        if (activeActionFrames != null)
        {
            UpdateAction(Time.deltaTime);
            return;
        }

        UpdateRunning(Time.deltaTime);
    }

    void UpdateAction(float deltaTime)
    {
        if (actionHand == null)
        {
            activeActionFrames = null;
            return;
        }

        actionClock += Mathf.Max(0f, deltaTime) * Mathf.Max(1f, actionFramesPerSecond);
        int frame = Mathf.FloorToInt(actionClock);
        if (frame >= activeActionFrames.Length)
        {
            activeActionFrames = null;
            actionClock = 0f;
            UpdateRunning(0f);
            return;
        }

        actionHand.sprite = activeActionFrames[frame];
        Layout(actionHand, 0f);
        SetRenderersVisible(false, true);
    }

    void UpdateRunning(float deltaTime)
    {
        if (leftRunHand == null || rightRunHand == null || runFrames == null || runFrames.Length == 0 ||
            (!player.HasMovementInput && player.HorizontalSpeed < minimumVisibleRunSpeed))
        {
            SetRenderersVisible(false, false);
            return;
        }

        // TargetSpeed is derived from baseSpeed and its stat/progression modifiers. Unlike the
        // instantaneous velocity, it does not collapse when CharacterController touches a wall.
        float speed = player.TargetSpeed;
        float speedProgress = SpeedProgress(speed);
        float fps = Mathf.Lerp(slowRunFramesPerSecond, fastRunFramesPerSecond, speedProgress);
        float alternatingCycleLength = runFrames.Length * 2f;
        runClock = Mathf.Repeat(
            runClock + Mathf.Max(0f, deltaTime) * Mathf.Max(1f, fps),
            alternatingCycleLength);

        int cycleFrame = Mathf.FloorToInt(runClock);
        bool showLeftHand = cycleFrame < runFrames.Length;
        Sprite frame = runFrames[cycleFrame % runFrames.Length];
        leftRunHand.sprite = frame;
        rightRunHand.sprite = frame;

        float separation = SeparationAtSpeed(speed);
        Layout(leftRunHand, -separation);
        Layout(rightRunHand, separation);
        leftRunHand.enabled = showLeftHand;
        rightRunHand.enabled = !showLeftHand;
        actionHand.enabled = false;
    }

    void UpdateEditModePreview()
    {
        if (!previewInEditMode || leftRunHand == null || rightRunHand == null ||
            runFrames == null || runFrames.Length == 0)
        {
            SetRenderersVisible(false, false);
            return;
        }

        int frameIndex = Mathf.Clamp(
            Mathf.RoundToInt(previewAnimationPhase * (runFrames.Length - 1)),
            0,
            runFrames.Length - 1);
        Sprite frame = runFrames[frameIndex];
        leftRunHand.sprite = frame;
        rightRunHand.sprite = frame;

        float separation = SeparationAtSpeed(previewSpeed);
        Layout(leftRunHand, -separation);
        Layout(rightRunHand, separation);
        leftRunHand.enabled = previewBothHands;
        rightRunHand.enabled = true;
        if (actionHand != null) actionHand.enabled = false;
    }

    float SeparationAtSpeed(float speed)
    {
        return Mathf.Lerp(walkingHandDistance, minimumHandDistanceAtMaximumSpeed, SpeedProgress(speed));
    }

    float SpeedProgress(float speed)
    {
        if (maximumBaseSpeed <= minimumBaseSpeed)
            return speed >= maximumBaseSpeed ? 1f : 0f;

        return Mathf.InverseLerp(minimumBaseSpeed, maximumBaseSpeed, speed);
    }

    void EnsureInitialized()
    {
        if (player == null) player = GetComponent<PlayerController>();
        if (repel == null) repel = GetComponent<PlayerRepel>();
        if (view == null) view = GetComponentInChildren<Camera>(true);

        if (runFrames == null || runFrames.Length == 0) runFrames = LoadFrames(RunFramesPath);
        if (punchFrames == null || punchFrames.Length == 0) punchFrames = LoadFrames(PunchFramesPath);
        if (parryFrames == null || parryFrames.Length == 0) parryFrames = LoadFrames(ParryFramesPath);
        if (grabFrames == null || grabFrames.Length == 0) grabFrames = LoadFrames(GrabFramesPath);
        if (presentationRoot == null || leftRunHand == null || rightRunHand == null || actionHand == null)
            CreatePresentation();
    }

    void CreatePresentation()
    {
        if (view == null || presentationRoot != null) return;
        Shader shader = Shader.Find("Runner/TutorialSheet");
        if (shader == null)
        {
            Debug.LogWarning("No se encontró el shader Runner/TutorialSheet para las manos en primera persona.", this);
            return;
        }

        overlayMaterial = new Material(shader)
        {
            name = "First Person Hands Overlay (Runtime)",
            hideFlags = HideFlags.HideAndDontSave,
            renderQueue = (int)RenderQueue.Overlay
        };

        presentationRoot = new GameObject("First Person Hands")
        {
            hideFlags = Application.IsPlaying(gameObject)
                ? HideFlags.DontSave
                : HideFlags.HideAndDontSave
        };
        presentationRoot.transform.SetParent(view.transform, false);
        leftRunHand = CreateRenderer("Run Hand Left", presentationRoot.transform, true);
        rightRunHand = CreateRenderer("Run Hand Right", presentationRoot.transform, false);
        actionHand = CreateRenderer("Hand Action", presentationRoot.transform, false);
        SetRenderersVisible(false, false);
    }

    SpriteRenderer CreateRenderer(string objectName, Transform parent, bool mirror)
    {
        var hand = new GameObject(objectName)
        {
            hideFlags = parent.gameObject.hideFlags
        };
        hand.transform.SetParent(parent, false);
        var renderer = hand.AddComponent<SpriteRenderer>();
        renderer.sharedMaterial = overlayMaterial;
        renderer.flipX = mirror;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    void Layout(SpriteRenderer renderer, float horizontalOffsetFraction)
    {
        if (renderer == null || renderer.sprite == null || view == null) return;
        float distance = Mathf.Max(view.nearClipPlane + 0.02f, planeDistance);
        float worldHeight = 2f * distance * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * 0.5f);
        float worldWidth = worldHeight * Mathf.Max(0.01f, view.aspect);
        float scale = worldWidth * screenWidthFraction / Mathf.Max(0.001f, renderer.sprite.bounds.size.x);
        float renderedHeight = renderer.sprite.bounds.size.y * scale;
        float x = worldWidth * horizontalOffsetFraction;
        float y = -worldHeight * 0.5f + renderedHeight * 0.5f + worldHeight * bottomMarginFraction;

        renderer.transform.localPosition = new Vector3(x, y, distance);
        renderer.transform.localRotation = Quaternion.identity;
        renderer.transform.localScale = new Vector3(scale, scale, 1f);
    }

    void SetRenderersVisible(bool running, bool action)
    {
        if (leftRunHand != null) leftRunHand.enabled = running;
        if (rightRunHand != null) rightRunHand.enabled = running;
        if (actionHand != null) actionHand.enabled = action;
    }

    static Sprite[] LoadFrames(string resourcesPath)
    {
        Sprite[] frames = Resources.LoadAll<Sprite>(resourcesPath);
        Array.Sort(frames, CompareFrames);
        return frames;
    }

    static int CompareFrames(Sprite left, Sprite right)
    {
        int leftNumber = TrailingNumber(left != null ? left.name : string.Empty);
        int rightNumber = TrailingNumber(right != null ? right.name : string.Empty);
        int order = leftNumber.CompareTo(rightNumber);
        return order != 0 ? order : string.CompareOrdinal(left != null ? left.name : string.Empty, right != null ? right.name : string.Empty);
    }

    static int TrailingNumber(string value)
    {
        int start = value.Length;
        while (start > 0 && char.IsDigit(value[start - 1])) start--;
        return start < value.Length && int.TryParse(value.Substring(start), out int number) ? number : int.MaxValue;
    }
}
