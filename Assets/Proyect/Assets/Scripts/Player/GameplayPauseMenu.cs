using System.Collections;
using MagicPigGames.MagicTime;
using MenuUIBase.Audio;
using MenuUIBase.Core;
using MenuUIBase.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// The UI and all positions live in the scene prefab; animations use unscaled time.
[DefaultExecutionOrder(-20)]
public sealed class GameplayPauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    public static bool IsSlowing { get; private set; }
    static GameplayPauseMenu current;
    [Header("Referencias editables")]
    [SerializeField] GameObject visuals;
    [SerializeField] RectTransform axe, paper;
    [Tooltip("Pivote padre que controla el arco del hacha.")]
    [SerializeField] RectTransform axePivot;
    [SerializeField] CanvasGroup axeButtons, paperControls;
    [SerializeField] Button resumeButton, optionsButton, menuButton, exitButton, backButton;
    [SerializeField] Slider master, music, effects, sensitivity;
    [Header("Transición de tiempo")]
    [SerializeField, Min(0.01f)] float slowDuration = 0.9f;
    [Header("Movimiento del hacha")]
    [SerializeField, Min(0.01f)] float axeDuration = 0.45f;
    [SerializeField] float axeHiddenAngle = -110f, axeVisibleAngle = 0f;
    [Header("Papel de opciones")]
    [SerializeField, Min(0.01f)] float paperDuration = 0.35f;
    [SerializeField] Vector2 paperVisiblePosition = Vector2.zero, paperHiddenPosition = new Vector2(0, 1200);
    float previousScale;
    CursorLockMode previousLock;
    bool previousVisibility, optionsOpen, transitioning;
    Coroutine transition;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { IsPaused = false; IsSlowing = false; current = null; }
    void Awake()
    {
        current = this;
        if (visuals != null) visuals.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (optionsButton != null) optionsButton.onClick.AddListener(OpenOptions);
        if (menuButton != null) menuButton.enabled = false;
        if (exitButton != null) exitButton.onClick.AddListener(ReturnToMenu);
        if (backButton != null) backButton.onClick.AddListener(CloseOptions);
        if (master != null) master.onValueChanged.AddListener(v => AudioManager.Instance.SetMasterVolume(v));
        if (music != null) music.onValueChanged.AddListener(v => AudioManager.Instance.SetMusicVolume(v));
        if (effects != null) effects.onValueChanged.AddListener(v => AudioManager.Instance.SetSoundEffectsVolume(v));
        if (sensitivity != null) sensitivity.onValueChanged.AddListener(v => GameSettings.Instance.SetMouseSensitivity(v));
    }
    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame || transitioning) return;
        if (IsPaused) { if (optionsOpen) CloseOptions(); else Resume(); }
        else if (Time.timeScale > 0f) Pause();
    }
    void Pause()
    {
        if (visuals == null || axe == null) { Debug.LogError("Faltan referencias del menú de pausa.", this); return; }
        previousScale = Time.timeScale; previousLock = Cursor.lockState; previousVisibility = Cursor.visible;
        IsPaused = true; IsSlowing = true; transitioning = true; optionsOpen = false;
        if (EventSystem.current == null)
        {
            var events = new GameObject("EventSystem pausa", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        transition = StartCoroutine(Enter());
    }
    IEnumerator Enter()
    {
        yield return GameplayTimeFreeze.Freeze(previousScale, slowDuration);
        IsSlowing = false;
        if (!IsPaused) yield break;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        visuals.SetActive(true); if (paper != null) paper.gameObject.SetActive(false);
        SetInteractable(axeButtons, false);
        yield return RotateAxe(axeHiddenAngle, axeVisibleAngle);
        SetInteractable(axeButtons, true); transitioning = false; transition = null;
        if (EventSystem.current != null && resumeButton != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }
    public void Resume()
    {
        if (!IsPaused || transitioning || optionsOpen) return;
        transition = StartCoroutine(Exit());
    }
    IEnumerator Exit()
    {
        transitioning = true; SetInteractable(axeButtons, false);
        yield return RotateAxe(axeVisibleAngle, axeHiddenAngle);
        visuals.SetActive(false); IsPaused = false; Time.timeScale = previousScale;
        Cursor.lockState = previousLock; Cursor.visible = previousVisibility;
        transitioning = false; transition = null;
    }
    void OpenOptions() { if (IsPaused && !transitioning && !optionsOpen) transition = StartCoroutine(Options(true)); }
    void CloseOptions() { if (IsPaused && !transitioning && optionsOpen) transition = StartCoroutine(Options(false)); }
    IEnumerator Options(bool open)
    {
        transitioning = true; SetInteractable(axeButtons, false); SetInteractable(paperControls, false);
        if (open)
        {
            if (master != null) master.SetValueWithoutNotify(AudioManager.Instance.MasterVolume);
            if (music != null) music.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
            if (effects != null) effects.SetValueWithoutNotify(AudioManager.Instance.SoundEffectsVolume);
            if (sensitivity != null) sensitivity.SetValueWithoutNotify(GameSettings.Instance.MouseSensitivity);
            paper.gameObject.SetActive(true); paper.anchoredPosition = paperHiddenPosition;
        }
        Vector2 from = open ? paperHiddenPosition : paperVisiblePosition, to = open ? paperVisiblePosition : paperHiddenPosition;
        for (float t = 0; t < paperDuration; t += Time.unscaledDeltaTime)
        { paper.anchoredPosition = Vector2.Lerp(from, to, Mathf.SmoothStep(0, 1, t / Mathf.Max(.01f, paperDuration))); yield return null; }
        paper.anchoredPosition = to; optionsOpen = open;
        if (!open) { paper.gameObject.SetActive(false); GameSettings.Instance.Save(); }
        SetInteractable(paperControls, open); SetInteractable(axeButtons, !open);
        transitioning = false; transition = null;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(open && backButton != null ? backButton.gameObject : optionsButton != null ? optionsButton.gameObject : null);
    }
    IEnumerator RotateAxe(float from, float to)
    {
        RectTransform pivot = axePivot != null ? axePivot : axe;
        for (float t = 0; t < axeDuration; t += Time.unscaledDeltaTime)
        { pivot.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(from, to, Mathf.SmoothStep(0, 1, t / Mathf.Max(.01f, axeDuration)))); yield return null; }
        pivot.localRotation = Quaternion.Euler(0, 0, to);
    }
    static void SetInteractable(CanvasGroup group, bool active) { if (group != null) { group.interactable = active; group.blocksRaycasts = active; } }
    void ReturnToMenu()
    {
        if (!IsPaused || transitioning || !Application.CanStreamedLevelBeLoaded("MainMenu")) return;
        IsPaused = false; Time.timeScale = previousScale; GameSettings.Instance.Save(); SceneLoader.Instance.LoadScene("MainMenu");
    }
    public static void AbortForEnd()
    {
        if (current == null || !IsPaused) return;
        if (current.transition != null) current.StopCoroutine(current.transition);
        current.transition = null; current.transitioning = false;
        if (current.visuals != null) current.visuals.SetActive(false);
        IsPaused = false; IsSlowing = false;
    }
    void OnDestroy()
    {
        if (current == this) { current = null; IsSlowing = false; }
        if (IsPaused) { IsPaused = false; Time.timeScale = previousScale; }
    }
}

// Magic Time supplies the transition value; the bridge updates Unity global time,
// which is consumed by the project's existing gameplay systems.
public static class GameplayTimeFreeze
{
    public static IEnumerator Freeze(float startScale, float duration)
    {
        var clock = ScriptableObject.CreateInstance<LocalTimeScale>();
        clock.Initialize("Transición de pausa", Mathf.Max(0f, startScale));
        try
        {
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                clock.SetValue(Mathf.Max(0f, startScale) * (1f - Mathf.SmoothStep(0f, 1f, t / Mathf.Max(.01f, duration))));
                Time.timeScale = clock.Value;
                yield return null;
            }
            clock.SetValue(0f); Time.timeScale = clock.Value;
        }
        finally { Object.Destroy(clock); }
    }
}
