using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DefaultExecutionOrder(-15)]
[RequireComponent(typeof(PlayerController), typeof(PlayerStats))]
public sealed class RunOverviewPanel : MonoBehaviour
{
    static RunOverviewPanel current;
    static int closedFrame = -1;
    public static bool IsOpen => current != null && current.open;
    public static bool BlocksGameplayInput => IsOpen || closedFrame == Time.frameCount;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState() { current = null; closedFrame = -1; }

    PlayerController player;
    PlayerStats stats;
    PlayerHealth health;
    PlayerScore score;
    PlayerCamera playerCamera;
    RewardItemEffects rewardEffects;
    RunLoadout loadout;
    RoomManager rooms;
    RoomCurseDirector roomCurse;

    GameObject canvasObject;
    RectTransform canvasRect, itemContent, curseContent, statusContent, tooltip;
    Text inventoryHeader, cursesHeader, statusHeader, tooltipTitle, tooltipBody;
    readonly Dictionary<string, Text> statusLabels = new Dictionary<string, Text>();
    readonly Dictionary<string, Text> statusValues = new Dictionary<string, Text>();
    readonly List<AssetKits.ParticleImage.ParticleImage> pausedUiParticles = new List<AssetKits.ParticleImage.ParticleImage>();
    bool open, displayedEnglish;
    float previousTimeScale = 1f, refreshRemaining;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;

    readonly Color background = new Color(0.025f, 0.022f, 0.03f, 0.97f);
    readonly Color panel = new Color(0.055f, 0.047f, 0.06f, 0.98f);
    readonly Color header = new Color(0.07f, 0.23f, 0.18f, 1f);

    void Awake()
    {
        current = this;
        player = GetComponent<PlayerController>();
        stats = GetComponent<PlayerStats>();
        health = GetComponent<PlayerHealth>();
        score = GetComponent<PlayerScore>();
        playerCamera = GetComponentInChildren<PlayerCamera>(true);
        loadout = RunLoadout.Ensure(player);
        loadout.Changed += OnLoadoutChanged;
        stats.Changed += OnStatsChanged;
        Build();
        canvasObject.SetActive(false);
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.wasPressedThisFrame)
        {
            if (open) Close();
            else Open();
        }

        if (!open) return;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Close(); return; }
        if (displayedEnglish != LanguageOptions.IsEnglish)
        {
            RefreshStaticLanguage();
            RebuildLoadout();
        }

        refreshRemaining -= Time.unscaledDeltaTime;
        if (refreshRemaining <= 0f)
        {
            refreshRemaining = 0.15f;
            RefreshStatus();
        }
        FollowTooltip();
    }

    void Open()
    {
        if (open || GameplayPauseMenu.IsPaused || player == null || !player.enabled || player.IsReadingTutorial || Time.timeScale <= 0f) return;
        ResolveWorldReferences();
        EnsureEventSystem();
        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        PauseUiParticles();
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        open = true;
        canvasObject.SetActive(true);
        HideTooltip();
        RebuildLoadout();
        RefreshStatus();
    }

    public void Close()
    {
        if (!open) return;
        open = false;
        closedFrame = Time.frameCount;
        HideTooltip();
        if (canvasObject != null) canvasObject.SetActive(false);
        Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
        ResumeUiParticles();
        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;
    }

    public static void AbortForEnd()
    {
        if (current != null) current.Close();
    }

    void PauseUiParticles()
    {
        pausedUiParticles.Clear();
        var particles = FindObjectsByType<AssetKits.ParticleImage.ParticleImage>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var particle in particles)
        {
            if (particle == null || !particle.isMain || !particle.isPlaying) continue;
            pausedUiParticles.Add(particle);
            particle.Pause();
        }
    }

    void ResumeUiParticles()
    {
        foreach (var particle in pausedUiParticles)
        {
            if (particle != null && particle.isActiveAndEnabled && particle.isPaused) particle.Play();
        }
        pausedUiParticles.Clear();
    }

    void ResolveWorldReferences()
    {
        if (health == null) health = GetComponent<PlayerHealth>();
        if (score == null) score = GetComponent<PlayerScore>();
        if (playerCamera == null) playerCamera = GetComponentInChildren<PlayerCamera>(true);
        rewardEffects = GetComponent<RewardItemEffects>();
        if (rooms == null) rooms = FindFirstObjectByType<RoomManager>();
        if (rooms != null) roomCurse = rooms.GetComponent<RoomCurseDirector>();
    }

    void OnLoadoutChanged()
    {
        if (!open) return;
        RebuildLoadout();
        RefreshStatus();
    }

    void OnStatsChanged()
    {
        if (open) RefreshStatus();
    }

    void Build()
    {
        canvasObject = new GameObject("Resumen de partida", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 240;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        canvasRect = canvasObject.GetComponent<RectTransform>();

        var dim = Stretch("Fondo", canvasRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var dimImage = dim.gameObject.AddComponent<Image>();
        dimImage.color = background;

        itemContent = CreateGridSection("Inventario", new Vector2(0.015f, 0.515f), new Vector2(0.62f, 0.985f), out inventoryHeader);
        curseContent = CreateGridSection("Maldiciones", new Vector2(0.015f, 0.015f), new Vector2(0.62f, 0.495f), out cursesHeader);
        statusContent = CreateStatusSection(new Vector2(0.635f, 0.015f), new Vector2(0.985f, 0.985f), out statusHeader);
        BuildStatusRows();
        BuildTooltip();
        RefreshStaticLanguage();
    }

    RectTransform CreateGridSection(string name, Vector2 min, Vector2 max, out Text title)
    {
        var root = Stretch(name, canvasRect, min, max, Vector2.zero, Vector2.zero);
        var image = root.gameObject.AddComponent<Image>(); image.color = panel;
        title = Header(root);

        var viewport = Stretch("Área", root, Vector2.zero, Vector2.one, new Vector2(12f, 12f), new Vector2(-12f, -68f));
        var viewportImage = viewport.gameObject.AddComponent<Image>(); viewportImage.color = new Color(0f, 0f, 0f, 0.16f);
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Stretch("Contenido", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f));
        content.pivot = new Vector2(0.5f, 1f);
        var grid = content.gameObject.AddComponent<GridLayoutGroup>();
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.cellSize = new Vector2(92f, 92f);
        grid.spacing = new Vector2(12f, 12f);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 7;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 24f; scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    RectTransform CreateStatusSection(Vector2 min, Vector2 max, out Text title)
    {
        var root = Stretch("Status", canvasRect, min, max, Vector2.zero, Vector2.zero);
        var image = root.gameObject.AddComponent<Image>(); image.color = panel;
        title = Header(root);
        var viewport = Stretch("Área", root, Vector2.zero, Vector2.one, new Vector2(14f, 14f), new Vector2(-14f, -68f));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = Stretch("Contenido", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
        content.pivot = new Vector2(0.5f, 1f);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(8, 8, 8, 8); layout.spacing = 5f;
        layout.childControlHeight = false; layout.childControlWidth = true; layout.childForceExpandWidth = true;
        var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.vertical = true;
        scroll.scrollSensitivity = 22f; scroll.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    Text Header(RectTransform parent)
    {
        var bar = Stretch("Cabecera", parent, new Vector2(0f, 1f), Vector2.one, new Vector2(8f, -56f), new Vector2(-8f, -8f));
        var image = bar.gameObject.AddComponent<Image>(); image.color = header;
        return Label("Título", bar, new Vector2(18f, 0f), new Vector2(-18f, 0f), 30, TextAnchor.MiddleLeft, true);
    }

    void BuildStatusRows()
    {
        StatusGroup("survival_header");
        StatusRow("health");
        StatusRow("regeneration");
        StatusRow("drain");
        StatusRow("environment", true);
        StatusGroup("movement_header");
        StatusRow("speed");
        StatusRow("acceleration");
        StatusRow("turn");
        StatusRow("braking");
        StatusRow("ground_control");
        StatusRow("air_control");
        StatusRow("jump");
        StatusRow("pound");
        StatusRow("impact");
        StatusGroup("score_header");
        StatusRow("combo_duration");
        StatusRow("combo_growth");
        StatusRow("score_bonus");
        StatusRow("discount");
        StatusRow("points");
        StatusRow("coins");
        StatusGroup("progress_header");
        StatusRow("tower");
        StatusRow("completed");
        StatusRow("room");
        StatusRow("time");
    }

    void StatusGroup(string key)
    {
        var rect = Rect(key, statusContent, new Vector2(100f, 38f), Vector2.zero);
        var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 38f;
        var text = Label("Texto", rect, new Vector2(6f, 0f), new Vector2(-6f, 0f), 22, TextAnchor.MiddleLeft, true);
        text.color = new Color(0.38f, 0.9f, 0.69f);
        statusLabels.Add(key, text);
    }

    void StatusRow(string key, bool hover = false)
    {
        var rect = Rect(key, statusContent, new Vector2(100f, 37f), Vector2.zero);
        var element = rect.gameObject.AddComponent<LayoutElement>(); element.preferredHeight = 37f;
        var separator = rect.gameObject.AddComponent<Image>(); separator.color = new Color(1f, 1f, 1f, 0.025f);
        var labelRect = Stretch("Nombre", rect, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(9f, 0f), new Vector2(-4f, 0f));
        var label = ConfigureText(labelRect.gameObject.AddComponent<Text>(), 19, TextAnchor.MiddleLeft, false);
        var valueRect = Stretch("Valor", rect, new Vector2(0.62f, 0f), Vector2.one, new Vector2(4f, 0f), new Vector2(-9f, 0f));
        var value = ConfigureText(valueRect.gameObject.AddComponent<Text>(), 20, TextAnchor.MiddleRight, true);
        value.color = new Color(0.93f, 0.93f, 0.86f);
        statusLabels.Add(key, label); statusValues.Add(key, value);
        if (hover)
        {
            value.raycastTarget = true;
            AddHover(value.gameObject, ShowEnvironmentTooltip, HideTooltip);
        }
    }

    void RebuildLoadout()
    {
        ClearChildren(itemContent);
        ClearChildren(curseContent);
        int items = 0, curses = 0;
        foreach (var entry in loadout.Entries)
        {
            if (entry.Kind == RunLoadoutKind.Item) { CreateSlot(itemContent, entry); items++; }
            else { CreateSlot(curseContent, entry); curses++; }
        }
        if (items == 0) CreateEmptySlot(itemContent, LanguageOptions.Choose("Sin objetos", "No items"));
        if (curses == 0) CreateEmptySlot(curseContent, LanguageOptions.Choose("Sin maldiciones", "No curses"));
    }

    void CreateSlot(RectTransform parent, RunLoadoutEntry entry)
    {
        var slot = Rect(entry.Id, parent, new Vector2(92f, 92f), Vector2.zero);
        var border = slot.gameObject.AddComponent<Image>();
        border.color = entry.Kind == RunLoadoutKind.Curse ? new Color(0.62f, 0.22f, 0.78f) :
            entry.Tier == ItemTier.Tier3 ? new Color(0.95f, 0.65f, 0.18f) :
            entry.Tier == ItemTier.Tier2 ? new Color(0.30f, 0.58f, 0.95f) : new Color(0.25f, 0.72f, 0.48f);
        var inner = Stretch("Fondo", slot, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        var innerImage = inner.gameObject.AddComponent<Image>(); innerImage.color = new Color(0.055f, 0.05f, 0.07f, 1f);
        var iconRect = Stretch("Icono", inner, Vector2.zero, Vector2.one, new Vector2(8f, 8f), new Vector2(-8f, -8f));
        var icon = iconRect.gameObject.AddComponent<Image>(); icon.sprite = entry.Icon; icon.preserveAspect = true; icon.raycastTarget = false;
        if (entry.Icon == null)
        {
            icon.enabled = false;
            var fallback = Label("Símbolo", inner, Vector2.zero, Vector2.zero, 42, TextAnchor.MiddleCenter, true);
            fallback.text = entry.Kind == RunLoadoutKind.Curse ? "!" : "+";
        }
        if (entry.Count > 1)
        {
            var badge = Stretch("Cantidad", slot, new Vector2(0.58f, 0f), new Vector2(1f, 0.32f), Vector2.zero, Vector2.zero);
            var badgeImage = badge.gameObject.AddComponent<Image>(); badgeImage.color = new Color(0f, 0f, 0f, 0.88f);
            var count = Label("Texto", badge, Vector2.zero, Vector2.zero, 19, TextAnchor.MiddleCenter, true);
            count.text = "x" + entry.Count;
        }
        AddHover(slot.gameObject, () => ShowEntryTooltip(entry), HideTooltip);
    }

    void CreateEmptySlot(RectTransform parent, string message)
    {
        var slot = Rect("Vacío", parent, new Vector2(92f, 92f), Vector2.zero);
        var text = Label("Texto", slot, Vector2.zero, Vector2.zero, 17, TextAnchor.MiddleCenter);
        text.text = message; text.color = new Color(1f, 1f, 1f, 0.42f);
    }

    void ShowEntryTooltip(RunLoadoutEntry entry)
    {
        string level = entry.Count > 1 ? $" — {LanguageOptions.Choose("Nivel", "Level")} {entry.Count}" : string.Empty;
        string tier = entry.Kind == RunLoadoutKind.Curse ? LanguageOptions.Choose("Maldición", "Curse") :
            entry.Tier == ItemTier.Tier3 ? LanguageOptions.Choose("Legendario", "Legendary") :
            entry.Tier == ItemTier.Tier2 ? LanguageOptions.Choose("Raro", "Rare") : LanguageOptions.Choose("Común", "Common");
        string body = tier + "\n\n" + GameplayTranslation.Text(entry.Description);
        string accumulatedCurse = AccumulatedCurseText(entry);
        if (!string.IsNullOrEmpty(accumulatedCurse))
            body += "\n\n" + LanguageOptions.Choose("Acumulado: ", "Accumulated: ") + accumulatedCurse;
        if (entry.Effects.Count > 0)
        {
            body += "\n\n" + LanguageOptions.Choose("Impacto total actual:", "Current total impact:");
            for (int i = 0; i < entry.Effects.Count; i++)
            {
                var effect = entry.Effects[i];
                if (!TryEvaluate(effect.Stat, entry.Sources, out float before) || !TryEvaluate(effect.Stat, null, out float after)) continue;
                body += $"\n{StatName(effect.Stat)}: {FormatStat(effect.Stat, before)} → {FormatStat(effect.Stat, after)}";
            }
        }
        ShowTooltip(GameplayTranslation.Text(entry.DisplayName) + level, body);
    }

    static string AccumulatedCurseText(RunLoadoutEntry entry)
    {
        if (entry.Kind != RunLoadoutKind.Curse || entry.EnemyCurse == EnemyCurse.None || entry.Count <= 0) return string.Empty;
        int count = entry.Count;
        float multiplicativeReduction = (1f - Mathf.Pow(0.8f, count)) * 100f;
        return entry.EnemyCurse switch
        {
            EnemyCurse.Flupin => LanguageOptions.Choose($"{count} Flupin seleccionados.", $"{count} selected Flupin."),
            EnemyCurse.MaskCharger => LanguageOptions.Choose($"{count} máscaras seleccionadas.", $"{count} selected masks."),
            EnemyCurse.FlupinHaste => LanguageOptions.Choose($"{multiplicativeReduction:0.##} % menos espera.", $"{multiplicativeReduction:0.##}% less waiting."),
            EnemyCurse.FlupinFury => LanguageOptions.Choose($"+{count * 20} % de agresividad.", $"+{count * 20}% aggressiveness."),
            EnemyCurse.MaskHaste => LanguageOptions.Choose($"{multiplicativeReduction:0.##} % menos aviso, intervalo y recuperación.", $"{multiplicativeReduction:0.##}% shorter warning, interval and recovery."),
            EnemyCurse.MaskFury => LanguageOptions.Choose($"+{count * 20} % de agresividad.", $"+{count * 20}% aggressiveness."),
            EnemyCurse.MaskStalking => LanguageOptions.Choose($"+{count * 30} % de acecho.", $"+{count * 30}% stalking strength."),
            EnemyCurse.DontseeFury => LanguageOptions.Choose($"+{count * 20} % de agresividad.", $"+{count * 20}% aggressiveness."),
            EnemyCurse.CorridorChaserSpeed => LanguageOptions.Choose($"+{count * 20} % de velocidad.", $"+{count * 20}% speed."),
            EnemyCurse.CorridorChaserHaste => LanguageOptions.Choose($"{multiplicativeReduction:0.##} % menos tiempo de aviso.", $"{multiplicativeReduction:0.##}% shorter warning time."),
            _ => string.Empty
        };
    }

    void ShowEnvironmentTooltip()
    {
        ResolveWorldReferences();
        if (roomCurse == null || !roomCurse.IsActive)
        {
            ShowTooltip(LanguageOptions.Choose("Normal", "Normal"), LanguageOptions.Choose(
                "No hay una maldición ambiental activa en esta sala.",
                "There is no active environmental curse in this room."));
            return;
        }
        switch (roomCurse.ActiveCurse)
        {
            case RoomCurseKind.Ice:
                ShowTooltip(LanguageOptions.Choose("Hielo", "Ice"), LanguageOptions.Choose(
                    "El suelo reduce 45 % el frenado y 35 % la aceleración de giro mientras lo pisas.",
                    "The floor reduces braking by 45% and turn acceleration by 35% while touched."));
                break;
            case RoomCurseKind.Tar:
                ShowTooltip(LanguageOptions.Choose("Brea", "Tar"), LanguageOptions.Choose(
                    "El suelo reduce 30 % la velocidad y 25 % la aceleración mientras lo pisas.",
                    "The floor reduces speed by 30% and acceleration by 25% while touched."));
                break;
            case RoomCurseKind.Lava:
                ShowTooltip(LanguageOptions.Choose("Lava", "Lava"), LanguageOptions.Choose(
                    "Recibes 1 punto de daño por segundo mientras permaneces sobre la lava.",
                    "You take 1 damage per second while standing on lava."));
                break;
        }
    }

    void BuildTooltip()
    {
        tooltip = Rect("Información", canvasRect, new Vector2(540f, 330f), Vector2.zero);
        tooltip.anchorMin = tooltip.anchorMax = new Vector2(0f, 0f); tooltip.pivot = new Vector2(0f, 1f);
        var image = tooltip.gameObject.AddComponent<Image>(); image.color = new Color(0.025f, 0.022f, 0.035f, 0.99f);
        var titleRect = Stretch("Título", tooltip, new Vector2(0f, 1f), Vector2.one, new Vector2(20f, -60f), new Vector2(-20f, -10f));
        tooltipTitle = ConfigureText(titleRect.gameObject.AddComponent<Text>(), 26, TextAnchor.MiddleLeft, true);
        tooltipTitle.color = new Color(0.45f, 0.95f, 0.72f);
        tooltipBody = Label("Contenido", tooltip, new Vector2(20f, 18f), new Vector2(-20f, -70f), 19, TextAnchor.UpperLeft);
        tooltipBody.verticalOverflow = VerticalWrapMode.Overflow;
        tooltip.gameObject.SetActive(false);
    }

    void ShowTooltip(string title, string body)
    {
        tooltipTitle.text = title;
        tooltipBody.text = body;
        tooltip.gameObject.SetActive(true);
        FollowTooltip();
    }

    void HideTooltip()
    {
        if (tooltip != null) tooltip.gameObject.SetActive(false);
    }

    void FollowTooltip()
    {
        if (tooltip == null || !tooltip.gameObject.activeSelf || Mouse.current == null) return;
        Vector2 screen = Mouse.current.position.ReadValue();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
        local += canvasRect.rect.size * canvasRect.pivot;
        float scale = canvasObject.GetComponent<Canvas>().scaleFactor;
        Vector2 size = tooltip.rect.size;
        float width = Screen.width / Mathf.Max(0.01f, scale), height = Screen.height / Mathf.Max(0.01f, scale);
        float x = Mathf.Clamp(local.x + 20f, 8f, width - size.x - 8f);
        float y = Mathf.Clamp(local.y - 16f, size.y + 8f, height - 8f);
        tooltip.anchoredPosition = new Vector2(x, y);
        tooltip.SetAsLastSibling();
    }

    void RefreshStatus()
    {
        ResolveWorldReferences();
        SetStatus("health", health != null ? $"{health.CurrentHealth:0.##} / {health.MaxHealth:0.##}" : "—");
        float regen = rewardEffects != null ? rewardEffects.RegenerationFractionPerTenSeconds : 0f;
        float drain = rewardEffects != null ? rewardEffects.PassiveDrainFractionPerThreeSeconds : 0f;
        SetStatus("regeneration", regen > 0f ? $"{regen * 100f:0.##} % / 10 s" : "0");
        SetStatus("drain", drain > 0f ? $"{drain * 100f:0.##} % / 3 s" : "0");
        SetStatus("environment", EnvironmentName());
        SetEvaluated("speed", PlayerStat.MoveSpeed);
        SetEvaluated("acceleration", PlayerStat.Acceleration);
        SetEvaluated("turn", PlayerStat.TurnAcceleration);
        SetEvaluated("braking", PlayerStat.Braking);
        SetEvaluated("ground_control", PlayerStat.GroundControl);
        SetEvaluated("air_control", PlayerStat.AirControl);
        SetEvaluated("jump", PlayerStat.JumpSpeed);
        SetEvaluated("pound", PlayerStat.PoundSpeed);
        SetEvaluated("impact", PlayerStat.ImpactDamage);
        SetEvaluated("combo_duration", PlayerStat.ComboDuration);
        SetEvaluated("combo_growth", PlayerStat.ComboMultiplierGrowth);
        SetEvaluated("score_bonus", PlayerStat.ScoreAmplification);
        SetEvaluated("discount", PlayerStat.ShopDiscount);
        SetStatus("points", score != null ? score.TotalPoints.ToString() : "0");
        SetStatus("coins", score != null ? score.Coins.ToString() : "0");
        if (rooms != null)
        {
            SetStatus("tower", $"{rooms.TowerNumber} / {rooms.TotalTowers}");
            SetStatus("completed", (rooms.IsFinished ? rooms.TowerNumber : Mathf.Max(0, rooms.TowerNumber - 1)).ToString());
            SetStatus("room", $"{rooms.RoomsEntered} / {rooms.RoomsPerTower}");
            int seconds = Mathf.Max(0, Mathf.CeilToInt(rooms.TimeRemaining));
            SetStatus("time", $"{seconds / 60:00}:{seconds % 60:00}");
        }
        else
        {
            SetStatus("tower", "—"); SetStatus("completed", "—"); SetStatus("room", "—"); SetStatus("time", "—");
        }
    }

    string EnvironmentName()
    {
        if (roomCurse == null || !roomCurse.IsActive) return LanguageOptions.Choose("Normal", "Normal");
        return roomCurse.ActiveCurse switch
        {
            RoomCurseKind.Ice => LanguageOptions.Choose("Hielo", "Ice"),
            RoomCurseKind.Tar => LanguageOptions.Choose("Brea", "Tar"),
            RoomCurseKind.Lava => "Lava",
            _ => LanguageOptions.Choose("Normal", "Normal")
        };
    }

    void SetEvaluated(string key, PlayerStat stat)
    {
        SetStatus(key, TryEvaluate(stat, null, out float value) ? FormatStat(stat, value) : "—");
    }

    bool TryEvaluate(PlayerStat stat, IReadOnlyList<object> excluded, out float value)
    {
        if (player != null && player.TryEvaluateDisplayStat(stat, excluded, out value)) return true;
        if (score != null && score.TryEvaluateDisplayStat(stat, excluded, out value)) return true;
        if (playerCamera != null && playerCamera.TryEvaluateDisplayStat(stat, excluded, out value)) return true;
        value = 0f; return false;
    }

    static string FormatStat(PlayerStat stat, float value)
    {
        return stat switch
        {
            PlayerStat.ShopDiscount => $"{value * 100f:0.##} %",
            PlayerStat.ImpactDamage or PlayerStat.ScoreAmplification => $"x{value:0.##}",
            PlayerStat.ComboDuration => $"{value:0.##} s",
            _ => value.ToString("0.##")
        };
    }

    static string StatName(PlayerStat stat)
    {
        if (LanguageOptions.IsEnglish) return stat switch
        {
            PlayerStat.MoveSpeed => "Target speed", PlayerStat.Acceleration => "Acceleration", PlayerStat.Braking => "Braking",
            PlayerStat.AirControl => "Air control", PlayerStat.JumpSpeed => "Jump", PlayerStat.PoundSpeed => "Stomp speed",
            PlayerStat.ImpactDamage => "Impact damage", PlayerStat.CameraControl => "Camera control",
            PlayerStat.ComboDuration => "Combo duration", PlayerStat.ComboMultiplierGrowth => "Combo growth",
            PlayerStat.ScoreAmplification => "Score", PlayerStat.TurnAcceleration => "Turn acceleration",
            PlayerStat.SpeedPerRoom => "Speed per room", PlayerStat.ShopDiscount => "Shop discount",
            PlayerStat.GroundControl => "Ground control", _ => stat.ToString()
        };
        return stat switch
        {
            PlayerStat.MoveSpeed => "Velocidad objetivo", PlayerStat.Acceleration => "Aceleración", PlayerStat.Braking => "Frenado",
            PlayerStat.AirControl => "Control aéreo", PlayerStat.JumpSpeed => "Salto", PlayerStat.PoundSpeed => "Velocidad de pisotón",
            PlayerStat.ImpactDamage => "Daño de impacto", PlayerStat.CameraControl => "Control de cámara",
            PlayerStat.ComboDuration => "Duración del combo", PlayerStat.ComboMultiplierGrowth => "Crecimiento del combo",
            PlayerStat.ScoreAmplification => "Puntos", PlayerStat.TurnAcceleration => "Aceleración de giro",
            PlayerStat.SpeedPerRoom => "Velocidad por sala", PlayerStat.ShopDiscount => "Descuento de tienda",
            PlayerStat.GroundControl => "Control lateral", _ => stat.ToString()
        };
    }

    void RefreshStaticLanguage()
    {
        displayedEnglish = LanguageOptions.IsEnglish;
        inventoryHeader.text = LanguageOptions.Choose("Inventario", "Inventory");
        cursesHeader.text = LanguageOptions.Choose("Maldiciones", "Curses");
        statusHeader.text = LanguageOptions.Choose("Status", "Status");
        SetLabel("survival_header", "SUPERVIVENCIA", "SURVIVAL");
        SetLabel("health", "Vida", "Health"); SetLabel("regeneration", "Regeneración", "Regeneration");
        SetLabel("drain", "Drenaje pasivo", "Passive drain"); SetLabel("environment", "Entorno", "Environment");
        SetLabel("movement_header", "MOVIMIENTO", "MOVEMENT"); SetLabel("speed", "Velocidad objetivo", "Target speed");
        SetLabel("acceleration", "Aceleración", "Acceleration"); SetLabel("turn", "Aceleración de giro", "Turn acceleration");
        SetLabel("braking", "Frenado", "Braking"); SetLabel("ground_control", "Control lateral", "Ground control");
        SetLabel("air_control", "Control aéreo", "Air control"); SetLabel("jump", "Salto", "Jump");
        SetLabel("pound", "Pisotón", "Stomp"); SetLabel("impact", "Daño de impacto", "Impact damage");
        SetLabel("score_header", "COMBO Y ECONOMÍA", "COMBO AND ECONOMY");
        SetLabel("combo_duration", "Duración del combo", "Combo duration"); SetLabel("combo_growth", "Crecimiento del combo", "Combo growth");
        SetLabel("score_bonus", "Multiplicador de puntos", "Score multiplier"); SetLabel("discount", "Descuento de tienda", "Shop discount");
        SetLabel("points", "Puntos", "Points"); SetLabel("coins", "Monedas", "Coins");
        SetLabel("progress_header", "PROGRESO", "PROGRESS"); SetLabel("tower", "Torre actual", "Current tower");
        SetLabel("completed", "Torres superadas", "Completed towers"); SetLabel("room", "Sala", "Room");
        SetLabel("time", "Tiempo restante", "Time remaining");
    }

    void SetLabel(string key, string spanish, string english)
    {
        if (statusLabels.TryGetValue(key, out var text)) text.text = LanguageOptions.Choose(spanish, english);
    }

    void SetStatus(string key, string value)
    {
        if (statusValues.TryGetValue(key, out var text)) text.text = value;
    }

    static void AddHover(GameObject target, Action enter, Action exit)
    {
        var trigger = target.AddComponent<EventTrigger>();
        var enterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enterEntry.callback.AddListener(_ => enter()); trigger.triggers.Add(enterEntry);
        var exitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exitEntry.callback.AddListener(_ => exit()); trigger.triggers.Add(exitEntry);
    }

    static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var events = new GameObject("EventSystem resumen", typeof(EventSystem), typeof(InputSystemUIInputModule));
        events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }

    static void ClearChildren(RectTransform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    static RectTransform Stretch(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin; rect.offsetMax = offsetMax; return rect;
    }

    static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 position)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); rect.sizeDelta = size; rect.anchoredPosition = position; return rect;
    }

    static Text Label(string name, RectTransform parent, Vector2 offsetMin, Vector2 offsetMax, int size, TextAnchor alignment, bool bold = false)
    {
        var rect = Stretch(name, parent, Vector2.zero, Vector2.one, offsetMin, offsetMax);
        return ConfigureText(rect.gameObject.AddComponent<Text>(), size, alignment, bold);
    }

    static Text ConfigureText(Text text, int size, TextAnchor alignment, bool bold)
    {
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size;
        text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; text.alignment = alignment;
        text.color = Color.white; text.supportRichText = false; text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    void OnDestroy()
    {
        if (loadout != null) loadout.Changed -= OnLoadoutChanged;
        if (stats != null) stats.Changed -= OnStatsChanged;
        if (open)
        {
            open = false;
            Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            pausedUiParticles.Clear();
        }
        if (current == this) current = null;
    }
}
