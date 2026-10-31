using MenuUIBase.Scenes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Temporary presentation; the pause state is independent of the future axe animation.
[DefaultExecutionOrder(-20)]
public sealed class GameplayPauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }
    GameObject canvasObject;
    GameObject resumeButton;
    float previousScale;
    CursorLockMode previousLock;
    bool previousVisibility;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { IsPaused = false; SceneManager.sceneLoaded -= OnSceneLoaded; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "e" || !scene.isLoaded) return;
        var root = new GameObject("Menú de pausa");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.AddComponent<GameplayPauseMenu>();
    }

    void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (IsPaused) Resume();
        else if (Time.timeScale > 0f) Pause();
    }

    void Pause()
    {
        if (canvasObject == null) CreateMenu();
        previousScale = Time.timeScale;
        previousLock = Cursor.lockState;
        previousVisibility = Cursor.visible;
        IsPaused = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        canvasObject.SetActive(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton);
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Time.timeScale = previousScale;
        canvasObject.SetActive(false);
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisibility;
    }

    void ReturnToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded("MainMenu")) { Debug.LogError("MainMenu no está en Build Settings."); return; }
        Resume();
        SceneLoader.Instance.LoadScene("MainMenu");
    }

    void OnDestroy()
    {
        if (IsPaused) { IsPaused = false; Time.timeScale = previousScale; }
    }

    void CreateMenu()
    {
        if (EventSystem.current == null)
        {
            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }
        canvasObject = new GameObject("Pausa - presentación provisional", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 95;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        var background = new GameObject("Fondo", typeof(RectTransform), typeof(Image));
        background.transform.SetParent(canvasObject.transform, false);
        var rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.GetComponent<Image>().color = new Color(0.025f, 0.02f, 0.04f, 0.8f);
        AddText(canvasObject.transform, "PAUSA", new Vector2(0, 140), 52);
        resumeButton = AddButton("CONTINUAR", new Vector2(0, 30), Resume);
        AddButton("MENÚ PRINCIPAL", new Vector2(0, -60), ReturnToMenu);
    }

    GameObject AddButton(string label, Vector2 position, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(canvasObject.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position; rect.sizeDelta = new Vector2(380, 65);
        go.GetComponent<Image>().color = new Color(0.32f, 0.25f, 0.15f);
        go.GetComponent<Button>().onClick.AddListener(action);
        var text = AddText(go.transform, label, Vector2.zero, 25);
        var textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
        textRect.offsetMin = textRect.offsetMax = Vector2.zero;
        return go;
    }

    static Text AddText(Transform parent, string label, Vector2 position, int size)
    {
        var go = new GameObject(label + " texto", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = label; text.fontSize = size; text.color = Color.white;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false;
        text.rectTransform.anchoredPosition = position; text.rectTransform.sizeDelta = new Vector2(700, 80);
        return text;
    }
}
