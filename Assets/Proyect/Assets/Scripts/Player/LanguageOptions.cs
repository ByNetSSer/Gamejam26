using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Scene UI is authored in Spanish. Keep its original strings and fonts intact.
public sealed class LanguageOptions : MonoBehaviour
{
    const string PreferenceKey = "Game.Settings.English";
    static readonly Dictionary<string, string> English = new Dictionary<string, string>
    {
        { "JUGAR", "PLAY" }, { "OPCIONES", "OPTIONS" }, { "Opciones", "Options" },
        { "CREDITOS", "CREDITS" }, { "Créditos", "Credits" }, { "SALIR", "QUIT" },
        { "VOLVER", "BACK" }, { "General", "Master" }, { "Música", "Music" },
        { "Efectos", "Effects" }, { "Sensibilidad", "Sensitivity" },
        { "VOLUMEN GENERAL", "MASTER VOLUME" }, { "MUSICA", "MUSIC" },
        { "EFECTOS", "EFFECTS" }, { "SENSIBILIDAD", "SENSITIVITY" },
        { "FÁCIL", "EASY" }, { "NORMAL", "NORMAL" }, { "IMPOSIBLE", "IMPOSSIBLE" },
        { "Desciende. Rompe. Sobrevive.", "Descend. Break. Survive." },
        { "E  PARA CONTINUAR", "E  TO CONTINUE" }
    };
    readonly List<Action<bool>> translations = new List<Action<bool>>();
    readonly List<Action> buttonLabels = new List<Action>();
    public static bool IsEnglish => PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
    public static string Choose(string spanish, string english) => IsEnglish ? english : spanish;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AddToScene(SceneManager.GetActiveScene());
    }
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AddToScene(scene);
    static void AddToScene(Scene scene)
    {
        if (scene.name != "MainMenu" && scene.name != "e") return;
        var go = new GameObject("Language options");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.AddComponent<LanguageOptions>().Setup(scene);
    }
    void Setup(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) Register(text);
            foreach (var text in root.GetComponentsInChildren<Text>(true)) Register(text);
        }
        foreach (var root in scene.GetRootGameObjects())
            foreach (var rect in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (scene.name == "MainMenu" && rect.name == "OptionsPanel") CreateButton(rect, false);
                if (scene.name == "e" && rect.name == "Papel opciones") CreateButton(rect, true);
            }
        Refresh();
    }
    static string Translation(string original)
    {
        if (English.TryGetValue(original.Trim(), out var value)) return value;
        if (original.StartsWith("<b>FÁCIL</b>")) return "<b>EASY</b>\nFor beginners: enjoy the descent with less pressure. Affordable prices, towers that grow slowly and punches to dish out. Don't get too comfortable, though.";
        if (original.StartsWith("<b>NORMAL</b>")) return "<b>NORMAL</b>\nThe experience we had in mind. Balanced prices and a descent that will test you as you lose control.";
        if (original.StartsWith("<b>IMPOSIBLE</b>")) return "<b>IMPOSSIBLE</b>\nHonestly, we didn't expect you to choose this. You aren't meant to get very far... but if you insist, good luck.";
        if (original.StartsWith("Prototipo de game jam")) return "Game jam prototype · Out of Control\nTeam and credits: pending.";
        return null;
    }
    void Register(TMP_Text text)
    {
        string original = text.text, translated = Translation(original);
        if (translated != null) translations.Add(english => { if (text != null) text.text = english ? translated : original; });
    }
    void Register(Text text)
    {
        string original = text.text, translated = Translation(original);
        if (translated != null) translations.Add(english => { if (text != null) text.text = english ? translated : original; });
    }
    void CreateButton(RectTransform parent, bool pause)
    {
        var go = new GameObject("Language toggle", typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rect = (RectTransform)go.transform;
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.sizeDelta = pause ? new Vector2(235, 40) : new Vector2(330, 54);
        rect.anchoredPosition = pause ? new Vector2(0, -122) : new Vector2(0, -280);
        var image = go.GetComponent<Image>();
        image.color = pause ? new Color(.27f, .16f, .11f, .9f) : new Color(.16f, .12f, .09f, .95f);
        var labelObject = new GameObject("Language label", typeof(RectTransform));
        labelObject.transform.SetParent(go.transform, false);
        var labelRect = (RectTransform)labelObject.transform;
        labelRect.anchorMin = Vector2.zero; labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;
        if (!pause)
        {
            var template = parent.GetComponentInChildren<TMP_Text>(true);
            var label = labelObject.AddComponent<TextMeshProUGUI>();
            if (template != null) label.font = template.font;
            label.fontSize = 25; label.enableAutoSizing = true; label.fontSizeMin = 15; label.fontSizeMax = 25;
            label.alignment = TextAlignmentOptions.Center; label.color = Color.white; label.raycastTarget = false;
            buttonLabels.Add(() => label.text = IsEnglish ? "LANGUAGE: ENGLISH" : "IDIOMA: ESPAÑOL");
        }
        else
        {
            var template = parent.GetComponentInChildren<Text>(true);
            var label = labelObject.AddComponent<Text>();
            label.font = template != null ? template.font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 20; label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
            buttonLabels.Add(() => label.text = IsEnglish ? "LANGUAGE: ENGLISH" : "IDIOMA: ESPAÑOL");
        }
        go.GetComponent<Button>().onClick.AddListener(() => { PlayerPrefs.SetInt(PreferenceKey, IsEnglish ? 0 : 1); PlayerPrefs.Save(); Refresh(); });
    }
    void Refresh()
    {
        foreach (var update in translations) update(IsEnglish);
        foreach (var update in buttonLabels) update();
    }
}
