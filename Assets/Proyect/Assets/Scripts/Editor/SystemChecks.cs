using System;
using UnityEditor;
using UnityEngine;

public static class SystemChecks
{
    [MenuItem("Tools/Runner/Verificar estadísticas y efectos")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Ejecuta esta verificación fuera de Play Mode.");
        TimerScheduler.Clear();
        var stat = new StatValue();
        var first = new object(); var second = new object();
        stat.Add(first, StatModifierType.Flat, 2f);
        stat.Add(first, StatModifierType.Percent, 0.5f);
        stat.Add(second, StatModifierType.PercentMult, 0.2f);
        Check(Mathf.Abs(stat.Calculate(10f) - 21.6f) < 0.001f, "Orden de modificadores");
        stat.RemoveSource(first);
        Check(Mathf.Abs(stat.Calculate(10f) - 12f) < 0.001f, "Eliminar solo la fuente indicada");

        int ticks = 0, completed = 0;
        var timer = new Timer(1f, 0.25f);
        timer.Tick += () => ticks++;
        timer.Completed += () => completed++;
        timer.Start(); TimerScheduler.Advance(1f);
        Check(ticks == 4 && completed == 1 && !timer.IsRunning, "Intervalos y finalización");
        timer.Dispose();
        bool invalidRejected = false;
        try { new Timer(1f, 0.00001f); } catch (ArgumentOutOfRangeException) { invalidRejected = true; }
        Check(invalidRejected, "Intervalo inválido");

        var combo = new ComboState();
        var firstAward = combo.Register("Caja rota", 10, 0f, 2f, 0.5f, 1f);
        var secondAward = combo.Register("Caja rota", 10, 1f, 2f, 0.5f, 1f);
        Check(firstAward.Points == 10 && firstAward.ComboCount == 1 && firstAward.Reason == "Caja rota", "Primer motivo y puntos");
        Check(secondAward.Points == 15 && secondAward.ComboCount == 2, "Multiplicador de combo");
        var afterExpiry = combo.Register("Escotilla", 10, 3f, 2f, 0.5f, 2f);
        Check(afterExpiry.ComboCount == 1 && afterExpiry.Points == 20, "Expiración y amplificación");

        Check(Mathf.Approximately(PlayerController.CalculateDestructibleScale(6.9f, 6.9f, 0.1f, 1.5f), 1f), "Resistencia inicial sin escalado");
        Check(Mathf.Approximately(PlayerController.CalculateDestructibleScale(170f, 6.9f, 0.1f, 1.5f), 1.5f), "Límite de vida a velocidad alta");
        Check(Mathf.Approximately(ImpactTarget.CalculateRequiredImpactSpeed(18f, 170f, 35f), 35f), "Límite de resistencia adaptativa");
        Check(Mathf.Approximately(ImpactTarget.CalculateRequiredImpactSpeed(60f, 170f, 35f), 60f), "Respetar obstáculo diseñado resistente");
        Check(Mathf.Approximately(ImpactTarget.CalculateScaledBreakSpeed(60f, 1.5f, 35f), 60f), "No aumentar resistencia base por encima del límite");

        var comboDuration = new StatValue();
        var durationSource = new object();
        comboDuration.Add(durationSource, StatModifierType.Percent, 0.05f);
        var timedCombo = new ComboState();
        timedCombo.Register("Prueba", 1, 0f, comboDuration.Calculate(4f), 0f, 1f);
        Check(Mathf.Abs(timedCombo.WindowDuration - 4.2f) < 0.001f && !timedCombo.Expire(4f), "Reloj común amplía la ventana");
        Check(timedCombo.Expire(4.2f), "El combo ampliado vence al final exacto");
        comboDuration.RemoveSource(durationSource);
        comboDuration.Add(durationSource, StatModifierType.Percent, 0.2f);
        timedCombo.Register("Prueba", 1, 10f, comboDuration.Calculate(4f), 0f, 1f);
        Check(Mathf.Abs(timedCombo.WindowDuration - 4.8f) < 0.001f && !timedCombo.Expire(14f), "Reloj raro amplía la ventana");

        var go = new GameObject("SystemChecks");
        try
        {
            var stats = go.AddComponent<PlayerStats>();
            var controller = go.AddComponent<PlayerStatEffects>();
            var effect = controller.Apply(PlayerStat.MoveSpeed, StatModifierType.Flat, 3f, 1f);
            Check(stats.Evaluate(PlayerStat.MoveSpeed, 10f) == 13f, "Aplicar efecto");
            TimerScheduler.Advance(1f);
            Check(!effect.IsActive && stats.Evaluate(PlayerStat.MoveSpeed, 10f) == 10f, "Expirar efecto");
            effect.Cancel();
            var permanent = controller.Apply(PlayerStat.MoveSpeed, StatModifierType.Flat, 4f, -1f);
            permanent.Cancel();
            Check(!permanent.IsActive && stats.Evaluate(PlayerStat.MoveSpeed, 10f) == 10f, "Limpiar al cancelar efecto");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); TimerScheduler.Clear(); }
        Debug.Log("Runner: verificaciones de estadísticas, temporizadores, efectos y combos superadas.");
    }
    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Runner: falló la verificación: " + description);
    }
}
