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
