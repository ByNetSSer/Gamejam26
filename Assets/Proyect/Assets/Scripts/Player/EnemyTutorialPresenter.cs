using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public enum TutorialEnemy { Flupin, MaskCharger, Dontsee, CorridorChaser, Guilt }

// A scene-authored sheet on the camera. The queue and seen set last for this run.
[DefaultExecutionOrder(-50)]
public sealed class EnemyTutorialPresenter : MonoBehaviour
{
    [SerializeField] SpriteRenderer sheet;
    [Header("Láminas editables")]
    [SerializeField] Sprite flupin, maskCharger, dontsee, corridorChaser, guilt;
    [Header("Tamaño máximo de la hoja en metros")]
    [SerializeField] Vector2 sheetSize = new Vector2(0.265f, 0.375f);
    [Header("Movimiento local de la hoja")]
    [SerializeField] Vector3 visiblePosition = new Vector3(0f, 0f, 0.4f);
    [SerializeField] Vector3 entrancePosition = new Vector3(0f, 0.4f, 0.4f);
    [SerializeField] Vector3 exitPosition = new Vector3(0f, -0.4f, 0.4f);
    [SerializeField, Min(0.01f)] float entranceDuration = 0.35f, exitDuration = 0.25f;
    readonly Queue<TutorialEnemy> pending = new Queue<TutorialEnemy>();
    readonly HashSet<TutorialEnemy> seen = new HashSet<TutorialEnemy>();
    PlayerController player;
    enum Phase { Idle, Entering, Reading, Exiting }
    Phase phase;
    float elapsed;
    int openedFrame;
    void Awake()
    {
        player = GetComponentInParent<PlayerController>();
        if (sheet != null) sheet.gameObject.SetActive(false);
    }
    public void Enqueue(TutorialEnemy enemy)
    {
        if (sheet == null || !seen.Add(enemy)) return;
        pending.Enqueue(enemy);
        if (player == null) player = GetComponentInParent<PlayerController>();
        if (player != null) player.SetReadingTutorial(true);
    }
    Sprite SpriteFor(TutorialEnemy enemy)
    {
        switch (enemy)
        {
            case TutorialEnemy.Flupin: return flupin;
            case TutorialEnemy.MaskCharger: return maskCharger;
            case TutorialEnemy.Dontsee: return dontsee;
            case TutorialEnemy.CorridorChaser: return corridorChaser;
            case TutorialEnemy.Guilt: return guilt;
            default: return null;
        }
    }
    void Update()
    {
        if (Time.timeScale <= 0f || GameplayPauseMenu.IsPaused) return;
        if (phase == Phase.Idle && pending.Count > 0)
        {
            sheet.sprite = SpriteFor(pending.Dequeue());
            if (sheet.sprite != null)
            {
                Vector2 spriteSize = sheet.sprite.rect.size / sheet.sprite.pixelsPerUnit;
                float scale = Mathf.Min(Mathf.Max(0.001f, sheetSize.x) / Mathf.Max(0.001f, spriteSize.x),
                    Mathf.Max(0.001f, sheetSize.y) / Mathf.Max(0.001f, spriteSize.y));
                sheet.transform.localScale = Vector3.one * scale;
            }
            sheet.transform.localPosition = entrancePosition;
            sheet.gameObject.SetActive(true);
            elapsed = 0f; openedFrame = Time.frameCount; phase = Phase.Entering;
        }
        if (phase == Phase.Entering)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, entranceDuration));
            sheet.transform.localPosition = Vector3.LerpUnclamped(entrancePosition, visiblePosition, 1f - (1f - t) * (1f - t));
            if (t >= 1f) phase = Phase.Reading;
        }
        if ((phase == Phase.Entering || phase == Phase.Reading) && Time.frameCount > openedFrame && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            phase = Phase.Exiting; elapsed = 0f;
            entrancePositionAtClose = sheet.transform.localPosition;
        }
        if (phase == Phase.Exiting)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, exitDuration));
            sheet.transform.localPosition = Vector3.Lerp(entrancePositionAtClose, exitPosition, t * t);
            if (t >= 1f)
            {
                sheet.gameObject.SetActive(false); phase = Phase.Idle;
                if (pending.Count == 0 && player != null) player.SetReadingTutorial(false);
            }
        }
    }
    Vector3 entrancePositionAtClose;
    void OnDisable() { pending.Clear(); phase = Phase.Idle; if (sheet != null) sheet.gameObject.SetActive(false); if (player != null) player.SetReadingTutorial(false); }
}
