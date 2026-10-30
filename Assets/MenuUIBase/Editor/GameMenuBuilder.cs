using System.Collections.Generic;
using System.IO;

using MenuUIBase.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MenuUIBase.Editor
{
    public static class GameMenuBuilder
    {
        private const string DemoFolder = "Assets/Scenes";
        private const string MenuScenePath = DemoFolder + "/MainMenu.unity";
        private const string GameplayScenePath = DemoFolder + "/e.unity";

        private static readonly Color Background = new Color(0.035f, 0.025f, 0.065f, 1f);
        private static readonly Color Surface = new Color(0.075f, 0.11f, 0.17f, 0.98f);
        private static readonly Color Primary = new Color(0.83f, 0.65f, 0.32f, 1f);
        private static readonly Color Secondary = new Color(0.46f, 0.38f, 0.95f, 1f);
        private static readonly Color TextPrimary = new Color(0.94f, 0.97f, 1f, 1f);
        private static readonly Color TextMuted = new Color(0.62f, 0.7f, 0.8f, 1f);

        [InitializeOnLoadMethod]
        static void QueueInitialBuild()
        {
            if (!File.Exists(MenuScenePath)) EditorApplication.delayCall += BuildMenu;
        }

        [MenuItem("Tools/Runner/Crear menu de juego")]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (File.Exists(MenuScenePath))
            {
                Debug.Log("MainMenu ya existe; edita sus objetos directamente para conservar tus ajustes.");
                return;
            }
            Scene previous = SceneManager.GetActiveScene();
            CreateMenuScene();
            if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == MenuScenePath || s.path == GameplayScenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));
            scenes.Insert(1, new EditorBuildSettingsScene(GameplayScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log("Menu listo: Assets/Scenes/MainMenu.unity. Play carga e.");
        }

        private static void CreateMenuScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            CreateCamera();
            Canvas canvas = CreateCanvas();
            CreateEventSystem();
            CreateBackground(canvas.transform);

            GameObject content = CreateUiObject("Content", canvas.transform);
            SetRect(content.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(840f, 760f));

            Image card = content.AddComponent<Image>();
            card.color = Surface;

            CreateText("Brand", content.transform, "OUT OF CONTROL", 18, TextMuted, TextAnchor.MiddleCenter, new Vector2(0f, 315f), new Vector2(700f, 38f));
            CreateText("Title", content.transform, "LA TORRE", 44, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, 250f), new Vector2(740f, 74f));
            CreateText("Subtitle", content.transform, "Desciende. Rompe. Sobrevive.", 20, TextMuted, TextAnchor.MiddleCenter, new Vector2(0f, 195f), new Vector2(740f, 50f));

            GameObject mainPanel = CreatePanel("MainPanel", content.transform);
            GameObject optionsPanel = CreatePanel("OptionsPanel", content.transform);
            GameObject creditsPanel = CreatePanel("CreditsPanel", content.transform);
            mainPanel.SetActive(true);
            optionsPanel.SetActive(false);
            creditsPanel.SetActive(false);

            PanelNavigator navigator = content.AddComponent<PanelNavigator>();
            ConfigureNavigator(navigator, mainPanel, new List<GameObject> { mainPanel, optionsPanel, creditsPanel });

            MainMenuController controller = content.AddComponent<MainMenuController>();
            ConfigureMainMenuController(controller, navigator, optionsPanel, creditsPanel);

            CreateButton("PlayButton", mainPanel.transform, "JUGAR", new Vector2(0f, 90f), Primary, controller.Play);
            CreateButton("OptionsButton", mainPanel.transform, "OPCIONES", new Vector2(0f, 10f), Secondary, controller.OpenOptions);
            CreateButton("CreditsButton", mainPanel.transform, "CRÉDITOS", new Vector2(0f, -70f), new Color(0.16f, 0.22f, 0.31f, 1f), controller.OpenCredits);
            CreateButton("QuitButton", mainPanel.transform, "SALIR", new Vector2(0f, -150f), new Color(0.13f, 0.18f, 0.25f, 1f), controller.Quit);

            CreateText("OptionsTitle", optionsPanel.transform, "Opciones", 32, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, 145f), new Vector2(600f, 52f));
            CreateVolumeRow(optionsPanel.transform, "General", AudioVolumeChannel.Master, 85f);
            CreateVolumeRow(optionsPanel.transform, "Música", AudioVolumeChannel.Music, 25f);
            CreateVolumeRow(optionsPanel.transform, "Efectos", AudioVolumeChannel.SoundEffects, -35f);
            CreateVolumeRow(optionsPanel.transform, "Sensibilidad", AudioVolumeChannel.Master, -95f);
            GameObject sensitivity = optionsPanel.transform.Find("SensibilidadSlider").gameObject;
            Object.DestroyImmediate(sensitivity.GetComponent<VolumeSliderBinding>());
            sensitivity.AddComponent<SensitivitySliderBinding>();
            CreateButton("OptionsBackButton", optionsPanel.transform, "VOLVER", new Vector2(0f, -175f), new Color(0.16f, 0.22f, 0.31f, 1f), controller.Back);

            CreateText("CreditsTitle", creditsPanel.transform, "Créditos", 32, TextPrimary, TextAnchor.MiddleCenter, new Vector2(0f, 115f), new Vector2(600f, 52f));
            CreateText("CreditsBody", creditsPanel.transform,
                "Prototipo de game jam · Out of Control\nEquipo y atribuciones: pendientes de completar.",
                20, TextMuted, TextAnchor.MiddleCenter, new Vector2(0f, 20f), new Vector2(620f, 110f));
            CreateButton("CreditsBackButton", creditsPanel.transform, "VOLVER", new Vector2(0f, -120f), new Color(0.16f, 0.22f, 0.31f, 1f), controller.Back);

            EditorSceneManager.SaveScene(scene, MenuScenePath);
            EditorSceneManager.CloseScene(scene, true);
        }

        private static void CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Background;
            camera.orthographic = true;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        private static Canvas CreateCanvas()
        {
            GameObject canvasObject = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        private static void CreateEventSystem()
        {
            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.SetActive(false);
            eventSystemObject.AddComponent<EventSystem>();

            System.Type inputSystemModuleType = System.Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");

            if (inputSystemModuleType != null)
                eventSystemObject.AddComponent(inputSystemModuleType);
            else
                eventSystemObject.AddComponent<StandaloneInputModule>();

            eventSystemObject.SetActive(true);
        }

        private static void CreateBackground(Transform parent)
        {
            GameObject background = CreateUiObject("Background", parent);
            RectTransform rect = background.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            background.AddComponent<Image>().color = Background;

            GameObject accent = CreateUiObject("Accent", background.transform);
            RectTransform accentRect = accent.GetComponent<RectTransform>();
            accentRect.anchorMin = new Vector2(0f, 1f);
            accentRect.anchorMax = new Vector2(1f, 1f);
            accentRect.pivot = new Vector2(0.5f, 1f);
            accentRect.sizeDelta = new Vector2(0f, 8f);
            accentRect.anchoredPosition = Vector2.zero;
            accent.AddComponent<Image>().color = Primary;
        }

        private static GameObject CreatePanel(string name, Transform parent)
        {
            GameObject panel = CreateUiObject(name, parent);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(60f, 55f);
            rect.offsetMax = new Vector2(-60f, 425f);
            return panel;
        }

        private static Button CreateButton(
            string name,
            Transform parent,
            string label,
            Vector2 position,
            Color color,
            UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = CreateUiObject(name, parent);
            SetRect(buttonObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(420f, 62f));
            buttonObject.GetComponent<RectTransform>().anchoredPosition = position;

            Image image = buttonObject.AddComponent<Image>();
            image.color = color;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            button.colors = colors;
            UnityEventTools.AddPersistentListener(button.onClick, action);

            GameObject labelObject = CreateText("Label", buttonObject.transform, label, 18, TextPrimary, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(390f, 55f));
            labelObject.GetComponent<Text>().fontStyle = FontStyle.Bold;
            return button;
        }

        private static void CreateVolumeRow(Transform parent, string label, AudioVolumeChannel channel, float y)
        {
            CreateText(label + "Label", parent, label, 18, TextMuted, TextAnchor.MiddleLeft, new Vector2(-190f, y), new Vector2(180f, 44f));

            GameObject sliderObject = CreateUiObject(label + "Slider", parent);
            SetRect(sliderObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(360f, 30f));
            sliderObject.GetComponent<RectTransform>().anchoredPosition = new Vector2(100f, y);

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;

            GameObject background = CreateUiObject("Background", sliderObject.transform);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            backgroundRect.anchorMin = new Vector2(0f, 0.25f);
            backgroundRect.anchorMax = new Vector2(1f, 0.75f);
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            background.AddComponent<Image>().color = new Color(0.12f, 0.17f, 0.24f, 1f);

            GameObject fillArea = CreateUiObject("Fill Area", sliderObject.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
            fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
            fillAreaRect.offsetMin = new Vector2(6f, 0f);
            fillAreaRect.offsetMax = new Vector2(-6f, 0f);

            GameObject fill = CreateUiObject("Fill", fillArea.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fill.AddComponent<Image>().color = Primary;

            GameObject handleArea = CreateUiObject("Handle Slide Area", sliderObject.transform);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handle = CreateUiObject("Handle", handleArea.transform);
            SetRect(handle.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(24f, 24f));
            handle.AddComponent<Image>().color = TextPrimary;

            slider.fillRect = fillRect;
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();

            VolumeSliderBinding binding = sliderObject.AddComponent<VolumeSliderBinding>();
            SerializedObject serializedBinding = new SerializedObject(binding);
            serializedBinding.FindProperty("slider").objectReferenceValue = slider;
            serializedBinding.FindProperty("channel").enumValueIndex = (int)channel;
            serializedBinding.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject CreateText(
            string name,
            Transform parent,
            string content,
            int fontSize,
            Color color,
            TextAnchor alignment,
            Vector2 position,
            Vector2 size)
        {
            GameObject textObject = CreateUiObject(name, parent);
            SetRect(textObject.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), size);
            textObject.GetComponent<RectTransform>().anchoredPosition = position;

            Text text = textObject.AddComponent<Text>();
            text.raycastTarget = false;
            text.text = content;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            return textObject;
        }

        private static GameObject CreateUiObject(string name, Transform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
        }

        private static void ConfigureNavigator(PanelNavigator navigator, GameObject initialPanel, List<GameObject> panels)
        {
            SerializedObject serializedNavigator = new SerializedObject(navigator);
            serializedNavigator.FindProperty("initialPanel").objectReferenceValue = initialPanel;
            SerializedProperty panelsProperty = serializedNavigator.FindProperty("panels");
            panelsProperty.arraySize = panels.Count;

            for (int i = 0; i < panels.Count; i++)
                panelsProperty.GetArrayElementAtIndex(i).objectReferenceValue = panels[i];

            serializedNavigator.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureMainMenuController(
            MainMenuController controller,
            PanelNavigator navigator,
            GameObject optionsPanel,
            GameObject creditsPanel)
        {
            SerializedObject serializedController = new SerializedObject(controller);
            serializedController.FindProperty("navigator").objectReferenceValue = navigator;
            serializedController.FindProperty("optionsPanel").objectReferenceValue = optionsPanel;
            serializedController.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
            serializedController.FindProperty("gameplaySceneName").stringValue = "e";
            serializedController.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
