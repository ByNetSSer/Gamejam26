using UnityEngine;

// Temporary debug HUD; can be replaced with the final UI via PlayerScore events.
[RequireComponent(typeof(PlayerScore))]
public sealed class ScoreDebugView : MonoBehaviour
{
    [SerializeField, Tooltip("Mostrar información de depuración durante las pruebas.")] bool showOverlay;
    PlayerScore score;
    PlayerController player;
    PlayerHealth health;
    GroundSurfaceSensor surfaceSensor;
    EnemyDirector enemies;
    ScoreAward lastAward;
    float displayUntil;
    void Awake() { score = GetComponent<PlayerScore>(); player = GetComponent<PlayerController>(); health = GetComponent<PlayerHealth>(); surfaceSensor = GetComponent<GroundSurfaceSensor>(); }
    void OnEnable() { if (score == null) score = GetComponent<PlayerScore>(); score.Awarded += OnAwarded; }
    void OnDisable() { if (score != null) score.Awarded -= OnAwarded; }
    public void SetScore(PlayerScore value)
    {
        if (score == value) return;
        if (score != null && isActiveAndEnabled) score.Awarded -= OnAwarded;
        score = value;
        if (score != null && isActiveAndEnabled) score.Awarded += OnAwarded;
    }
    void OnAwarded(ScoreAward award) { lastAward = award; displayUntil = Time.time + 2f; }
    void OnGUI()
    {
        if (!showOverlay || score == null) return;
        if (surfaceSensor == null) surfaceSensor = GetComponent<GroundSurfaceSensor>();
        if (enemies == null) enemies = GetComponent<EnemyDirector>();
        GUI.Box(new Rect(12, 12, 330, 240), GUIContent.none);
        GUI.Label(new Rect(22, 20, 290, 20), $"Puntos: {score.TotalPoints}   Combo: {score.ComboCount} ({score.ComboRemaining:F1}s)");
        if (Time.time < displayUntil)
            GUI.Label(new Rect(22, 42, 290, 20), $"{lastAward.Reason} +{lastAward.Points}  x{lastAward.Multiplier:F2}");
        if (player != null)
            GUI.Label(new Rect(22, 64, 290, 20), $"Velocidad: {player.HorizontalSpeed:F1} / {player.MaximumRunSpeed:F1} ({player.MaximumRunSpeedFraction:P0})   Salas: {player.RoomsCleared}");
        GUI.Label(new Rect(22, 86, 290, 20), $"Combo: {score.ComboDuration:F1}s  Crecimiento: {score.MultiplierGrowth:F2}");
        GUI.Label(new Rect(22, 108, 290, 20), $"Amplificación: x{score.ScoreAmplification:F1}");
        if (health != null) GUI.Label(new Rect(22, 130, 290, 20), $"Vida: {Mathf.CeilToInt(health.CurrentHealth)}/{Mathf.CeilToInt(health.MaxHealth)}  |  hielo / brea / lava");
        if (surfaceSensor != null) GUI.Label(new Rect(22, 152, 300, 20), $"Suelo: {surfaceSensor.SurfaceNames}");
        GUI.Label(new Rect(22, 174, 290, 20), "Amarillo: velocidad | Azul: combo");
        GUI.Label(new Rect(22, 196, 300, 20), "Verde: puntos | Espacio + Ctrl: pisotón");
        if (enemies != null) GUI.Label(new Rect(22, 218, 300, 20), enemies.IsThreatActive ? "¡Enemigo prepara ataque! Clic derecho" : "Clic derecho: repeler alrededor");
    }
}
