using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

// One scheduler in the PlayerLoop; timers themselves are owned by their effects.
public static class TimerScheduler
{
    static readonly List<Timer> timers = new List<Timer>();
    static readonly List<Timer> snapshot = new List<Timer>();
    internal static void Add(Timer timer) { if (!timers.Contains(timer)) timers.Add(timer); }
    internal static void Remove(Timer timer) => timers.Remove(timer);
    public static void Advance(float deltaTime)
    {
        if (deltaTime <= 0f) return;
        snapshot.Clear();
        snapshot.AddRange(timers);
        foreach (var timer in snapshot) if (timers.Contains(timer)) timer.Advance(deltaTime);
        snapshot.Clear();
    }
    static void Update() => Advance(Time.deltaTime);
    public static void Clear()
    {
        var copy = timers.ToArray();
        foreach (var timer in copy) timer.Cancel();
        timers.Clear();
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Initialize()
    {
        Clear();
        var loop = PlayerLoop.GetCurrentPlayerLoop();
        RemoveFromLoop(ref loop);
        if (Insert(ref loop)) PlayerLoop.SetPlayerLoop(loop);
        else Debug.LogError("TimerScheduler: no se encontró Update en el PlayerLoop.");
    }
    static bool Insert(ref PlayerLoopSystem system)
    {
        if (system.type == typeof(Update))
        {
            var list = new List<PlayerLoopSystem>(system.subSystemList ?? Array.Empty<PlayerLoopSystem>());
            list.Insert(0, new PlayerLoopSystem { type = typeof(TimerScheduler), updateDelegate = Update });
            system.subSystemList = list.ToArray();
            return true;
        }
        if (system.subSystemList == null) return false;
        for (int i = 0; i < system.subSystemList.Length; i++)
            if (Insert(ref system.subSystemList[i])) return true;
        return false;
    }
    static void RemoveFromLoop(ref PlayerLoopSystem system)
    {
        if (system.subSystemList == null) return;
        var list = new List<PlayerLoopSystem>(system.subSystemList);
        list.RemoveAll(s => s.type == typeof(TimerScheduler));
        for (int i = 0; i < list.Count; i++) { var child = list[i]; RemoveFromLoop(ref child); list[i] = child; }
        system.subSystemList = list.ToArray();
    }
}

